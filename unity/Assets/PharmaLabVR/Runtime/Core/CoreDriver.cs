using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PharmaLabVR.Input;
using PharmaLabVR.Lab;
using PharmaLabVR.Tools;
using PharmaLabVR.UI;
using UnityEngine;

namespace PharmaLabVR.Core
{
    public sealed class CoreDriver : MonoBehaviour
    {
        [SerializeField] private string branchId = "local-session";
        [SerializeField] private bool automaticNativeCheckpointing = true;
        public string AutomaticCheckpointPath { get; private set; }
        [SerializeField] private ScienceBudgetProfile scienceBudgetProfile = ScienceBudgetProfile.Precision;
        [SerializeField] private MonoBehaviour[] inputAdapterBehaviours;
        public enum ScienceBudgetProfile { Precision, Bulk }
        public event Action<string> SnapshotChanged;
        // Asynchronous Android package preparation may fail after the
        // initial XR panel has bound. Notify the view to fail closed.
        public event Action<string> StartupFailed;
        public event Action<LabViewModel> LabViewChanged;
        public event Action<string> NativeEventReceived;
        public event Action NativeSessionRestored;
        // Local presentation epoch only. Not a Native command sequence,
        // proof of scientific origin, persisted clock or sync receipt.
        // Detects a restoration missed while a Unity panel was disabled.
        public ulong NativeRestorationEpoch { get; private set; }
        // A local presentation marker. Advanced only after a Native
        // snapshot has been read and converted for the current restored
        // authority. NOT a signed scientific receipt or chemistry revision.
        public ulong NativePublishedRestorationEpoch { get; private set; }
        public event Action<bool> HoldChanged;
        public CoreSession Session { get; private set; }
        public string CurrentNativeBranchId => Session?.BranchId ?? string.Empty;
        public string StartupError { get; private set; }
        public string ScientificDatabasePath { get; private set; }
        public MeasurementView Measurement { get; private set; }
        public LabViewModel CurrentLabView { get; private set; }
        public string CurrentMode { get; private set; } = "Desktop";
        public string HoldReason { get; private set; } = string.Empty;
        public bool NativeRecoveryReady { get; private set; }
        public bool IsTimeHeld { get; private set; }
        public bool IsNativePaused { get; private set; }
        public bool SourceNeedsPreparation { get; private set; }
        public string SelectedMeasurementVesselId { get; private set; } = "source";
        public IReadOnlyList<string> AvailableStockIds => stockIds;
        public IReadOnlyList<double> ResearchConcentrations => stockCatalog?.researchMatrixMolPerL ?? Array.Empty<double>();
        private readonly List<string> stockIds = new();
        private StockCatalogEnvelope stockCatalog;
        private double accumulator;
        private bool establishFrameClock;
        private Func<string> packagePathResolver = () => ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
        private const double TickSeconds = 0.020;

        public void ConfigureInputs(params MonoBehaviour[] adapters)
        {
            inputAdapterBehaviours = adapters;
            SynchronizeInputSequences();
        }
        public void ConfigurePackagePathResolver(Func<string> resolver) =>
            packagePathResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

        private void OnEnable()
        {
            // Requested interaction mode is presentation-only until Native
            // publishes an authoritative snapshot. Even a failed XR load
            // must remain visible inside the tracked headset, not an invisible
            // Desktop-default error panel.
            CurrentMode = BootMenu.RequestedMode == ApplicationMode.VirtualReality ?
                "VR" : "Desktop";
            if (Application.platform == RuntimePlatform.Android)
            {
                StartCoroutine(ScientificPackage.PrepareAndroid(InitializeCore, FailStartup));
                return;
            }
            try
            {
                InitializeCore(packagePathResolver());
            }
            catch (Exception exception) { FailStartup(exception); }
        }

        private void InitializeCore(string databasePath)
        {
            try
            {
                var catalogAsset = Resources.Load<TextAsset>("aqueous-six-research-v1-stocks");
                stockCatalog = catalogAsset == null ? null : JsonUtility.FromJson<StockCatalogEnvelope>(catalogAsset.text);
                if (stockCatalog == null || stockCatalog.schemaVersion != 1 || stockCatalog.stocks == null ||
                    stockCatalog.stocks.Length != 6 || stockCatalog.researchMatrixMolPerL == null)
                    throw new InvalidOperationException("The six-stock Research catalog is missing or invalid.");
                stockIds.Clear();
                foreach (var stock in stockCatalog.stocks) stockIds.Add(stock.id);
                var initialMode = BootMenu.RequestedMode == ApplicationMode.VirtualReality ? "VR" : "Desktop";
                var checkpointPath = automaticNativeCheckpointing ? NativeCheckpointPathFor(branchId) : "";
                Session = new CoreSession(branchId, initialMode, databasePath, ScientificPackage.DatabaseSha256,
                    scienceBudgetProfile.ToString(), checkpointPath);
                AutomaticCheckpointPath = checkpointPath;
                ScientificDatabasePath = databasePath;
                BootstrapResearchSession();
                Measurement = GetComponent<MeasurementView>();
                if (Measurement == null) Measurement = gameObject.AddComponent<MeasurementView>();
                var visuals = GetComponent<NativeLabVisuals>();
                if (visuals == null) visuals = gameObject.AddComponent<NativeLabVisuals>();
                visuals.Configure(this);
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        if (transform.name == "PerformanceRecorder")
                        {
                            var recorder = transform.GetComponent<PlayerPerformanceRecorder>();
                            if (recorder == null) recorder = transform.gameObject.AddComponent<PlayerPerformanceRecorder>();
                            recorder.Configure(this);
                        }
                StartupError = null;
                establishFrameClock = true;
                accumulator = 0.0;
                PublishSnapshot();
            }
            catch (Exception exception) { FailStartup(exception); }
        }

        private void FailStartup(Exception exception)
        {
            StartupError = exception.Message;
            Debug.LogError($"Native scientific core unavailable: {StartupError}");
            Session?.Dispose();
            Session = null;
            CurrentLabView = null;
            ScientificDatabasePath = null;
            enabled = false;
            StartupFailed?.Invoke(StartupError);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && Session != null) BeginInputHold("FocusLoss");
        }

        public bool BeginInputHold(string reason)
        {
            if (Session == null || (reason != "FocusLoss" && reason != "TrackingLoss")) return false;
            var outcome = JsonUtility.FromJson<CommandOutcome>(
                Session.SubmitOrdered("BeginInputHold", $"{{\"reason\":\"{reason}\"}}"));
            if (outcome == null || !outcome.accepted) return false;
            IsTimeHeld = true;
            accumulator = 0.0;
            HoldChanged?.Invoke(true);
            PublishSnapshot();
            return true;
        }

        private void Update()
        {
            if (Session == null) return;
            if (establishFrameClock)
            {
                establishFrameClock = false;
                accumulator = 0.0;
                var baselineNowNs = NowNs();
                Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(CollectSamples(baselineNowNs)));
                Session.Step(0.0, baselineNowNs);
                PublishSnapshot();
                return;
            }
            if (IsNativePaused)
            {
                accumulator = Math.Min(TickSeconds, accumulator + Time.unscaledDeltaTime);
                if (accumulator < TickSeconds) return;
                var pausedNowNs = NowNs();
                Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(CollectSamples(pausedNowNs)));
                Session.Step(0.0, pausedNowNs);
                accumulator = 0.0;
                PublishSnapshot();
                return;
            }
            accumulator += Time.unscaledDeltaTime;
            if (IsTimeHeld)
            {
                if (accumulator < TickSeconds) return;
                var heldNowNs = NowNs();
                Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(CollectSamples(heldNowNs)));
                Session.Step(0.0, heldNowNs);
                accumulator = 0.0;
                PublishSnapshot();
                return;
            }
            if (accumulator > TickSeconds * 2.0)
            {
                var nowNs = NowNs();
                Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(CollectSamples(nowNs)));
                if (IsTimeHeld) Session.Step(0.0, nowNs);
                else
                {
                    Session.Step(accumulator, nowNs);
                    Debug.LogWarning("TimeDiscontinuityHold: more than two transport ticks accumulated.");
                }
                accumulator = 0.0;
                PublishSnapshot();
                return;
            }
            while (accumulator >= TickSeconds)
            {
                var nowNs = NowNs();
                Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(CollectSamples(nowNs)));
                if (IsTimeHeld)
                {
                    Session.Step(0.0, nowNs);
                    accumulator = 0.0;
                    PublishSnapshot();
                    break;
                }
                Session.Step(TickSeconds, nowNs);
                accumulator -= TickSeconds;
                PublishSnapshot();
            }
        }

        public bool ResumeAfterTimeHold()
        {
            // This is exclusively a Native Hold recovery operation. Without
            // a Hold, submitting neutral samples followed by Continue could
            // close an unprepared Native source or insert a command receipt.
            if (Session == null || !IsTimeHeld) return false;
            var nowNs = NowNs();
            Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(CollectSamples(nowNs)));
            Session.Step(0.0, nowNs);
            var continueNowNs = NowNs();
            // Loading a deliberately blank new research branch must not
            // close its Native setup before the learner chooses stock.
            // ResumeSetup has exactly the same fresh-neutral Hold gate.
            var requiresSetupRecovery = IsTimeHeld && SourceNeedsPreparation;
            // An input/tracking interruption must not permanently CLOSE a
            // still-paused, blank research source. ResumeSetup preserves the
            // authorized preparation window after fresh-neutral recovery.
            // Compute/Storage holds are not eligible for this shortcut.
            var recoverableSetupHold =
                HoldReason == "SessionLoad" ||
                HoldReason == "CheckpointRestart" ||
                HoldReason == "FocusLoss" ||
                HoldReason == "TrackingLoss" ||
                HoldReason == "Input" ||
                HoldReason == "TimeDiscontinuity" ||
                HoldReason == "Recenter" ||
                HoldReason == "ModeChange";
            if (requiresSetupRecovery && !recoverableSetupHold)
                return false;
            var recoveryType = requiresSetupRecovery ? "ResumeSetup" : "Continue";
            var outcomeJson = Session.SubmitOrdered(recoveryType, $"{{\"monotonicNowNs\":\"{continueNowNs}\"}}");
            var outcome = JsonUtility.FromJson<CommandOutcome>(outcomeJson);
            if (outcome == null || !outcome.accepted) return false;
            if (inputAdapterBehaviours != null)
                foreach (var behaviour in inputAdapterBehaviours)
                    if (behaviour is ILabInputAdapter adapter) adapter.ResetBaselines();
            accumulator = 0.0;
            establishFrameClock = true;
            IsTimeHeld = false;
            // The accepted Native Continue may UNPAUSE the lab, whereas a
            // genuine unprepared SessionLoad ResumeSetup stays Native-paused.
            // Publish the authoritative post-command paused flag BEFORE the
            // router receives a HoldChanged(false) and decides where to go.
            PublishSnapshot();
            HoldChanged?.Invoke(false);
            return true;
        }

        public bool BeginModeChange(ApplicationMode mode)
        {
            if (Session == null) return false;
            var nativeMode = mode == ApplicationMode.VirtualReality ? "VR" : "Desktop";
            var outcomeJson = Session.SubmitOrdered("BeginModeChange", $"{{\"mode\":\"{nativeMode}\"}}");
            var outcome = JsonUtility.FromJson<CommandOutcome>(outcomeJson);
            if (outcome == null || !outcome.accepted) return false;
            IsTimeHeld = true;
            accumulator = 0.0;
            HoldChanged?.Invoke(true);
            PublishSnapshot();
            return true;
        }

        public bool CreateCheckpoint(string checkpointId)
        {
            if (Session == null) return false;
            var outcome = JsonUtility.FromJson<CommandOutcome>(
                Session.SubmitOrdered("CreateCheckpoint", $"{{\"checkpointId\":\"{EscapeJson(checkpointId)}\"}}"));
            return outcome != null && outcome.accepted;
        }

        public bool PauseSession()
        {
            // Native Hold is a distinct recovery authority. A direct Pause
            // must not generate a receipt or change that authority while
            // fresh-neutral input baselines are being established.
            if (Session == null || IsTimeHeld) return false;
            var outcome = JsonUtility.FromJson<CommandOutcome>(Session.SubmitOrdered("Pause"));
            if (outcome == null || !outcome.accepted) return false;
            accumulator = 0.0;
            PublishSnapshot();
            return true;
        }

        public bool ContinueSession()
        {
            // Ordinary Continue lacks the recovery-neutral input and baseline
            // reset from ResumeAfterTimeHold. Never let a recovered-ready
            // held session bypass that explicit recovery contract.
            if (Session == null || IsTimeHeld) return false;
            var outcome = JsonUtility.FromJson<CommandOutcome>(Session.SubmitOrdered("Continue"));
            if (outcome == null || !outcome.accepted) return false;
            accumulator = 0.0;
            establishFrameClock = true;
            PublishSnapshot();
            return true;
        }

        public bool SelectMeasurementVessel(string vesselId)
        {
            if (Session == null || string.IsNullOrWhiteSpace(vesselId)) return false;
            var state = JsonUtility.FromJson<SnapshotEnvelope>(Session.ReadSnapshot());
            if (state?.vessels == null) return false;
            foreach (var vessel in state.vessels)
                if (vessel.id == vesselId)
                {
                    SelectedMeasurementVesselId = vesselId;
                    PublishSnapshot();
                    return true;
                }
            return false;
        }

        public bool RestartCheckpoint(string checkpointId)
        {
            if (Session == null) return false;
            var outcome = JsonUtility.FromJson<CommandOutcome>(
                Session.SubmitOrdered("RestartCheckpoint", $"{{\"checkpointId\":\"{EscapeJson(checkpointId)}\"}}"));
            if (outcome == null || !outcome.accepted) return false;
            IsTimeHeld = true;
            accumulator = 0.0;
            // A Native checkpoint is a replacement observation authority.
            // A previously selected receiver cannot stay selected while the
            // UI resets to the saved source and asks for fresh inspection.
            SelectedMeasurementVesselId = "source";
            ResetPhysicalInteractionAfterNativeRestoration();
            HoldChanged?.Invoke(true);
            NativeRestorationEpoch++;
            NativeSessionRestored?.Invoke();
            PublishSnapshot();
            return true;
        }

        public void ReplaceSession(CoreSession replacement)
        {
            if (replacement == null || !replacement.IsOpen)
                throw new ArgumentException("Replacement session is not open.", nameof(replacement));
            if (Session == null || !Session.IsOpen)
                throw new InvalidOperationException("A live Native session is required for a replacement.");
            // Import forces SessionLoad Hold in Native. Before swapping handles,
            // refuse mode mismatch or an undocked tool in VR. The old Native
            // session remains fully owned and open on all admission failures.
            var imported = JsonUtility.FromJson<SnapshotEnvelope>(replacement.ReadSnapshot());
            var mount = transform.root.GetComponentInChildren<ToolMount>(true);
            if (!SessionLoadAdmission.MayReplace(CurrentMode, imported?.mode,
                    mount != null, mount != null && mount.IsMounted))
                throw new InvalidOperationException(
                    "Load refused: saved mode must match current mode and VR tool must be docked.");
            // Attach the imported Native authority to its own branch's writer
            // before disposing the previous session. A failure preserves the old
            // active context and never silently disables periodic persistence.
            var checkpointPath = automaticNativeCheckpointing ?
                NativeCheckpointPathFor(replacement.BranchId) : "";
            if (automaticNativeCheckpointing)
                replacement.ConfigureAutomaticCheckpoint(checkpointPath);
            var previous = Session;
            Session = replacement;
            SelectedMeasurementVesselId = "source";
            AutomaticCheckpointPath = checkpointPath;
            accumulator = 0.0;
            establishFrameClock = true;
            previous?.Dispose();
            SynchronizeInputSequences();
            ResetPhysicalInteractionAfterNativeRestoration();
            NativeRestorationEpoch++;
            NativeSessionRestored?.Invoke();
            PublishSnapshot();
        }

        // The Native session was already accepted. Every old physical
        // selection/gesture now belongs to the wrong scientific authority.
        // Keep the lab pose non-authoritative but block gestures until the
        // student releases inputs and establishes a fresh neutral state.
        private void ResetPhysicalInteractionAfterNativeRestoration()
        {
            if (inputAdapterBehaviours == null) return;
            foreach (var behaviour in inputAdapterBehaviours)
                if (behaviour is INativeRestorationInputAdapter adapter)
                    adapter.ResetForNativeRestoration();
        }

        public void SynchronizeInputSequences()
        {
            if (Session == null || inputAdapterBehaviours == null) return;
            var state = JsonUtility.FromJson<SnapshotEnvelope>(Session.ReadSnapshot());
            if (state?.inputWatermarks == null) return;
            foreach (var behaviour in inputAdapterBehaviours)
                if (behaviour is ILabInputAdapter adapter)
                    foreach (var watermark in state.inputWatermarks)
                        if (ulong.TryParse(watermark.sampleSequence, out var sequence))
                            adapter.AdvanceSequence(watermark.toolId, sequence);
        }

        private static string NativeCheckpointPathFor(string branch)
        {
            if (string.IsNullOrEmpty(branch) || branch.Length > 64)
                throw new ArgumentException("Native session branch identity is invalid.");
            foreach (var value in branch)
                if (!((value >= 'a' && value <= 'z') ||
                      (value >= 'A' && value <= 'Z') ||
                      (value >= '0' && value <= '9') ||
                       value == '.' || value == '_' || value == '-'))
                    throw new ArgumentException("Native checkpoint branch path is unsafe.");
            var directory = Path.Combine(Application.persistentDataPath, "sessions");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "native-" + branch + ".plv-checkpoint");
        }

        private void BootstrapResearchSession() => BootstrapResearchSession(Session);

        private static void BootstrapResearchSession(CoreSession target)
        {
            RequireAccepted(target.SubmitOrdered("CreateVessel", "{\"id\":\"source\",\"capacityM3\":0.00005}"));
            RequireAccepted(target.SubmitOrdered("CreateVessel", "{\"id\":\"receiver\",\"capacityM3\":0.0001}"));
            RequireAccepted(target.SubmitOrdered("CreateVessel", "{\"id\":\"burette\",\"capacityM3\":0.00005}"));
            RequireAccepted(target.SubmitOrdered("CreateSink", "{\"id\":\"spill\"}"));
            RequireAccepted(target.SubmitOrdered("PlaceTool",
                "{\"toolId\":\"research-tool\",\"sourceInventoryId\":\"burette\",\"overflowSinkId\":\"spill\",\"coordinateFrame\":\"lab\",\"geometryProfileHash\":\"burette-50ml-research-v1\",\"profileRevision\":\"1\"}"));
            RequireAccepted(target.SubmitOrdered("Pause"));
        }

        // Another trial needs a genuinely new Native identity, not a saved
        // copy of the same scientific branch. No active session is swapped.
        // IMPORTANT: this is an inert pre-import bootstrap. Import happens
        // ONLY after the full fresh save/Notebook pair reaches durable files.
        public CoreSession PrepareIndependentStudy(string branch)
        {
            if (Session?.IsOpen != true || string.IsNullOrEmpty(ScientificDatabasePath) ||
                string.IsNullOrWhiteSpace(branch) || branch == Session.BranchId)
                throw new InvalidOperationException("Open Native science and a distinct branch are required.");
            var staged = new CoreSession(branch, CurrentMode,
                ScientificDatabasePath, ScientificPackage.DatabaseSha256,
                scienceBudgetProfile.ToString(), "", stagedResearchBootstrap: true);
            try
            {
                BootstrapResearchSession(staged);
                var snapshot = JsonUtility.FromJson<SnapshotEnvelope>(staged.ReadSnapshot());
                if (snapshot?.hold == null || snapshot.hold.active ||
                    !snapshot.paused || !string.Equals(snapshot.mode, CurrentMode,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Unprepared staged Native branch must be paused before handover.");
                return staged;
            }
            catch
            {
                staged.Dispose();
                throw;
            }
        }

        // User-requested setup water is real Native inventory, never a
        // hidden procedural refill after setup has closed.
        public bool RinseWaterPrepared
        {
            get
            {
                if (CurrentLabView == null) return false;
                foreach (var vessel in CurrentLabView.Vessels)
                    if (vessel.Id == "rinse-water" && vessel.CommittedVolumeM3 > 0.0)
                        return true;
                return false;
            }
        }

        public bool TryPrepareRinseWater()
        {
            if (Session == null || !SourceNeedsPreparation) return false;
            var state = JsonUtility.FromJson<RinseSetupState>(Session.ReadSnapshot());
            if (state?.vessels == null || state.sinks == null)
                throw new InvalidOperationException("Native rinse setup inventory is unavailable.");
            var vesselExists = false;
            var waterPresent = false;
            foreach (var vessel in state.vessels)
                if (vessel.id == "rinse-water")
                {
                    vesselExists = true;
                    waterPresent = vessel.inventory != null &&
                                   vessel.inventory.researchAdditiveVolumeM3 > 0.0;
                }
            if (waterPresent) return true;
            var wasteExists = false;
            foreach (var sink in state.sinks)
                if (sink.id == "waste") wasteExists = true;
            if (!vesselExists)
                RequireAccepted(Session.SubmitOrdered("CreateVessel",
                    "{\"id\":\"rinse-water\",\"capacityM3\":0.00005}"));
            if (!wasteExists)
                RequireAccepted(Session.SubmitOrdered("CreateSink", "{\"id\":\"waste\"}"));
            var commands = new LabCommandService(Session);
            var receipt = commands.Submit("PrepareStock",
                "{\"vesselId\":\"rinse-water\",\"stockKind\":\"Water\",\"concentrationMolPerL\":0.0,\"referenceVolumeM3\":0.000001}",
                commands.CaptureTouchedRevisions("rinse-water"));
            if (!receipt.Accepted) return false;
            PublishSnapshot();
            return true;
        }

        public bool ReserveStockPrepared
        {
            get
            {
                if (CurrentLabView == null) return false;
                foreach (var vessel in CurrentLabView.Vessels)
                    if (vessel.Id == "reserve-stock" && vessel.CommittedVolumeM3 > 0.0)
                        return true;
                return false;
            }
        }

        // An alternate source must be deliberately prepared while still in
        // setup. Later switching/refilling consumes this represented inventory.
        public bool TryPrepareReserveStock(string stockId, double concentrationMolPerL)
        {
            if (Session == null || !SourceNeedsPreparation ||
                stockCatalog?.stocks == null || ReserveStockPrepared) return false;
            StockChoice choice = null;
            foreach (var stock in stockCatalog.stocks)
                if (stock.id == stockId) { choice = stock; break; }
            if (choice == null) return false;
            if (choice.kind == "Water")
            {
                if (concentrationMolPerL != 0.0) return false;
            }
            else
            {
                var supported = false;
                foreach (var value in stockCatalog.researchMatrixMolPerL)
                    if (Math.Abs(value - concentrationMolPerL) < 1e-12) supported = true;
                if (!supported) return false;
            }
            var snapshot = JsonUtility.FromJson<RinseSetupState>(Session.ReadSnapshot());
            if (snapshot?.vessels == null)
                throw new InvalidOperationException("Native reserve-stock setup is unavailable.");
            var exists = false;
            foreach (var vessel in snapshot.vessels)
                if (vessel.id == "reserve-stock")
                {
                    exists = true;
                    if (vessel.inventory != null &&
                        vessel.inventory.researchAdditiveVolumeM3 > 0.0) return false;
                }
            if (!exists)
                RequireAccepted(Session.SubmitOrdered("CreateVessel",
                    "{\"id\":\"reserve-stock\",\"capacityM3\":0.00005}"));
            var commands = new LabCommandService(Session);
            var payload = $"{{\"vesselId\":\"reserve-stock\",\"stockKind\":\"{EscapeJson(choice.kind)}\"," +
                          $"\"concentrationMolPerL\":{concentrationMolPerL.ToString("R", CultureInfo.InvariantCulture)}," +
                          "\"referenceVolumeM3\":0.000025}";
            var prepared = commands.Submit("PrepareStock", payload,
                commands.CaptureTouchedRevisions("reserve-stock"));
            if (!prepared.Accepted) return false;
            PublishSnapshot();
            return true;
        }

        public bool TryPrepareSourceStock(string stockId, double concentrationMolPerL)
        {
            if (Session == null || stockCatalog?.stocks == null || !SourceNeedsPreparation) return false;
            StockChoice choice = null;
            foreach (var stock in stockCatalog.stocks)
                if (stock.id == stockId) { choice = stock; break; }
            if (choice == null) return false;
            if (choice.kind == "Water")
            {
                if (concentrationMolPerL != 0.0) return false;
            }
            else
            {
                var supported = false;
                foreach (var value in stockCatalog.researchMatrixMolPerL)
                    if (Math.Abs(value - concentrationMolPerL) < 1e-12) supported = true;
                if (!supported) return false;
            }
            var payload = $"{{\"vesselId\":\"source\",\"stockKind\":\"{EscapeJson(choice.kind)}\",\"concentrationMolPerL\":{concentrationMolPerL.ToString("R", CultureInfo.InvariantCulture)},\"referenceVolumeM3\":0.000025}}";
            var commands = new LabCommandService(Session);
            var prepared = commands.Submit("PrepareStock", payload, commands.CaptureTouchedRevisions("source"));
            if (!prepared.Accepted) return false;
            RequireAccepted(Session.SubmitOrdered("Continue"));
            establishFrameClock = true;
            accumulator = 0.0;
            PublishSnapshot();
            return true;
        }

        private static void RequireAccepted(string outcomeJson)
        {
            var outcome = JsonUtility.FromJson<CommandOutcome>(outcomeJson);
            if (outcome == null || !outcome.accepted)
                throw new InvalidOperationException($"Native setup rejected: {outcome?.code ?? "invalid outcome"}");
        }

        private List<LabInputSample> CollectSamples(ulong nowNs)
        {
            var samples = new List<LabInputSample>();
            if (inputAdapterBehaviours != null)
                foreach (var behaviour in inputAdapterBehaviours)
                    if (behaviour != null && behaviour.isActiveAndEnabled && behaviour is ILabInputAdapter adapter)
                        samples.AddRange(adapter.SampleInputs(nowNs));
            if (!IsTimeHeld && samples.Exists(sample => !sample.trackingValid))
                BeginInputHold("TrackingLoss");
            return samples;
        }

        // Called after accepted main-thread user commands; read back the Native
        // receipt/snapshot rather than predicting any displayed material.
        public void RefreshAfterInstrumentCommand()
        {
            if (Session == null || !Session.IsOpen) return;
            PublishSnapshot();
        }

        private void PublishSnapshot()
        {
            foreach (var nativeEvent in Session.PollEvents())
                NativeEventReceived?.Invoke(nativeEvent);
            var snapshot = Session.ReadSnapshot();
            CurrentLabView = LabViewModel.FromNativeSnapshot(snapshot);
            var state = JsonUtility.FromJson<SnapshotEnvelope>(snapshot);
            if (state != null) CurrentMode = state.mode;
            IsNativePaused = state?.paused ?? false;
            SourceNeedsPreparation = false;
            // An empty source is NOT permission to reopen Native setup:
            // a continued and subsequently paused experiment can have no
            // stock yet still have setupClosed=true. Respect Native's own
            // preparation boundary before offering any PrepareStock UI.
            if (state?.vessels != null && !state.setupClosed)
                foreach (var vessel in state.vessels)
                    if (vessel.id == "source" && vessel.materialRevision == "0" &&
                        state.paused)
                        SourceNeedsPreparation = true;
            HoldReason = state?.hold?.reason ?? string.Empty;
            NativeRecoveryReady = state?.hold != null && state.hold.recoveryReady;
            if (state?.observations != null && Measurement != null)
            {
                var selectedFound = false;
                foreach (var observation in state.observations)
                    if (observation.vesselId == SelectedMeasurementVesselId && observation.observableId == "pH")
                    {
                        selectedFound = true;
                        // Native alone owns chemistry; this presentation policy
                        // cannot expose a non-finite or unsupported value as Current.
                        var availability = ScientificReadoutPolicy.Resolve(observation.support,
                            observation.computationState, observation.freshness,
                            observation.value, observation.valueAvailable);
                        Measurement.Present(availability,
                            observation.value.ToString("0.00", CultureInfo.InvariantCulture),
                            $"pH {SelectedMeasurementVesselId} · rev {observation.asOfMaterialRevision} · {availability} · {observation.maturity} · {observation.modelId} · package {observation.databaseIdentity}");
                        break;
                    }
                if (!selectedFound) Measurement.Present(MeasurementAvailability.Unsupported, "",
                    $"pH {SelectedMeasurementVesselId} · observation unavailable");
            }
            // Publish this local epoch after the authoritative Native
            // recovery flag and observation view have been read, before
            // notifying panels. A restore event itself is NOT publication.
            NativePublishedRestorationEpoch = NativeRestorationEpoch;
            var held = state?.hold != null && state.hold.active;
            if (held != IsTimeHeld)
            {
                IsTimeHeld = held;
                HoldChanged?.Invoke(held);
            }
            SnapshotChanged?.Invoke(snapshot);
            LabViewChanged?.Invoke(CurrentLabView);
        }

        private static ulong NowNs() => (ulong)(Time.realtimeSinceStartupAsDouble * 1_000_000_000.0);
        private static string EscapeJson(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private sealed class CommandOutcome
        {
            public bool accepted;
            public string code;
        }

        [Serializable]
        private sealed class SnapshotEnvelope
        {
            public HoldEnvelope hold;
            public string mode;
            public bool paused;
            public bool setupClosed;
            public VesselEnvelope[] vessels;
            public InputWatermark[] inputWatermarks;
            public ObservationEnvelope[] observations;
        }

        [Serializable] private sealed class RinseSetupState
        {
            public RinseSetupVessel[] vessels;
            public RinseSetupSink[] sinks;
        }

        [Serializable] private sealed class RinseSetupVessel
        {
            public string id;
            public RinseMaterial inventory;
        }

        [Serializable] private sealed class RinseSetupSink { public string id; }

        [Serializable] private sealed class RinseMaterial
        {
            public double researchAdditiveVolumeM3;
        }

        [Serializable] private sealed class VesselEnvelope
        {
            public string id;
            public string materialRevision;
        }

        [Serializable] private sealed class StockCatalogEnvelope
        {
            public int schemaVersion;
            public double[] researchMatrixMolPerL;
            public StockChoice[] stocks;
        }

        [Serializable] private sealed class StockChoice
        {
            public string id;
            public string kind;
        }

        [Serializable]
        private sealed class ObservationEnvelope
        {
            public string vesselId;
            public string observableId;
            public string support;
            public string freshness;
            public string computationState;
            public string maturity;
            public string modelId;
            public string asOfMaterialRevision;
            public string databaseIdentity;
            public double value;
            public bool valueAvailable;
        }

        [Serializable]
        private sealed class InputWatermark
        {
            public string toolId;
            public string sampleSequence;
        }

        [Serializable]
        private sealed class HoldEnvelope
        {
            public bool active;
            public string reason;
            public bool recoveryReady;
        }

        private void OnDisable()
        {
            Session?.Dispose();
            Session = null;
            CurrentLabView = null;
            IsNativePaused = false;
        }
    }
}
