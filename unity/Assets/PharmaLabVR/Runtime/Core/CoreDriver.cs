using System;
using System.Collections.Generic;
using System.Globalization;
using PharmaLabVR.Input;
using PharmaLabVR.Lab;
using PharmaLabVR.UI;
using UnityEngine;

namespace PharmaLabVR.Core
{
    public sealed class CoreDriver : MonoBehaviour
    {
        [SerializeField] private string branchId = "local-session";
        [SerializeField] private MonoBehaviour[] inputAdapterBehaviours;
        public event Action<string> SnapshotChanged;
        public event Action<string> NativeEventReceived;
        public event Action<bool> HoldChanged;
        public CoreSession Session { get; private set; }
        public string StartupError { get; private set; }
        public string ScientificDatabasePath { get; private set; }
        public MeasurementView Measurement { get; private set; }
        public string CurrentMode { get; private set; } = "Desktop";
        public string HoldReason { get; private set; } = string.Empty;
        public bool IsTimeHeld { get; private set; }
        public bool SourceNeedsPreparation { get; private set; }
        public IReadOnlyList<string> AvailableStockIds => stockIds;
        public IReadOnlyList<double> ResearchConcentrations => stockCatalog?.researchMatrixMolPerL ?? Array.Empty<double>();
        private readonly List<string> stockIds = new();
        private StockCatalogEnvelope stockCatalog;
        private double accumulator;
        private const double TickSeconds = 0.020;

        public void ConfigureInputs(params MonoBehaviour[] adapters) => inputAdapterBehaviours = adapters;

        private void OnEnable()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                StartCoroutine(ScientificPackage.PrepareAndroid(InitializeCore, FailStartup));
                return;
            }
            try
            {
                InitializeCore(ScientificPackage.PrepareLocal(Application.streamingAssetsPath));
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
                Session = new CoreSession(branchId, initialMode, databasePath, ScientificPackage.DatabaseSha256);
                ScientificDatabasePath = databasePath;
                BootstrapResearchSession();
                Measurement = gameObject.AddComponent<MeasurementView>();
                gameObject.AddComponent<NativeLabVisuals>().Configure(this);
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        if (transform.name == "PerformanceRecorder")
                            transform.gameObject.AddComponent<PlayerPerformanceRecorder>().Configure(this);
                StartupError = null;
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
            ScientificDatabasePath = null;
            enabled = false;
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
            if (Session == null) return false;
            var nowNs = NowNs();
            Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(CollectSamples(nowNs)));
            Session.Step(0.0, nowNs);
            var continueNowNs = NowNs();
            var outcomeJson = Session.SubmitOrdered("Continue", $"{{\"monotonicNowNs\":\"{continueNowNs}\"}}");
            var outcome = JsonUtility.FromJson<CommandOutcome>(outcomeJson);
            if (outcome == null || !outcome.accepted) return false;
            if (inputAdapterBehaviours != null)
                foreach (var behaviour in inputAdapterBehaviours)
                    if (behaviour is ILabInputAdapter adapter) adapter.ResetBaselines();
            accumulator = 0.0;
            IsTimeHeld = false;
            HoldChanged?.Invoke(false);
            PublishSnapshot();
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

        public bool RestartCheckpoint(string checkpointId)
        {
            if (Session == null) return false;
            var outcome = JsonUtility.FromJson<CommandOutcome>(
                Session.SubmitOrdered("RestartCheckpoint", $"{{\"checkpointId\":\"{EscapeJson(checkpointId)}\"}}"));
            if (outcome == null || !outcome.accepted) return false;
            IsTimeHeld = true;
            accumulator = 0.0;
            HoldChanged?.Invoke(true);
            PublishSnapshot();
            return true;
        }

        public void ReplaceSession(CoreSession replacement)
        {
            if (replacement == null || !replacement.IsOpen) throw new ArgumentException("Replacement session is not open.", nameof(replacement));
            var previous = Session;
            Session = replacement;
            accumulator = 0.0;
            previous?.Dispose();
            SynchronizeInputSequences();
            PublishSnapshot();
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

        private void BootstrapResearchSession()
        {
            RequireAccepted(Session.SubmitOrdered("CreateVessel", "{\"id\":\"source\",\"capacityM3\":0.00005}"));
            RequireAccepted(Session.SubmitOrdered("CreateVessel", "{\"id\":\"receiver\",\"capacityM3\":0.0001}"));
            RequireAccepted(Session.SubmitOrdered("CreateSink", "{\"id\":\"spill\"}"));
            RequireAccepted(Session.SubmitOrdered(
                "PlaceTool",
                "{\"toolId\":\"research-tool\",\"sourceInventoryId\":\"source\",\"overflowSinkId\":\"spill\",\"coordinateFrame\":\"lab\",\"geometryProfileHash\":\"burette-50ml-research-v1\",\"profileRevision\":\"1\"}"));
            RequireAccepted(Session.SubmitOrdered("Pause"));
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

        private void PublishSnapshot()
        {
            foreach (var nativeEvent in Session.PollEvents())
                NativeEventReceived?.Invoke(nativeEvent);
            var snapshot = Session.ReadSnapshot();
            var state = JsonUtility.FromJson<SnapshotEnvelope>(snapshot);
            if (state != null) CurrentMode = state.mode;
            SourceNeedsPreparation = false;
            if (state?.vessels != null)
                foreach (var vessel in state.vessels)
                    if (vessel.id == "source" && vessel.materialRevision == "0" && state.paused)
                        SourceNeedsPreparation = true;
            HoldReason = state?.hold?.reason ?? string.Empty;
            if (state?.observations != null && Measurement != null)
                foreach (var observation in state.observations)
                    if (observation.vesselId == "source" && observation.observableId == "pH")
                    {
                        var availability = observation.support != "Supported" ? MeasurementAvailability.Unsupported :
                            observation.computationState == "Failed" ? MeasurementAvailability.Failed :
                            observation.freshness == "Stale" ? MeasurementAvailability.Stale :
                            observation.freshness == "Current" && observation.computationState == "Ready" ?
                                MeasurementAvailability.Current : MeasurementAvailability.Pending;
                        Measurement.Present(availability,
                            observation.value.ToString("0.00", CultureInfo.InvariantCulture),
                            $"pH {availability} · {observation.maturity} · {observation.modelId}");
                        break;
                    }
            var held = state?.hold != null && state.hold.active;
            if (held != IsTimeHeld)
            {
                IsTimeHeld = held;
                HoldChanged?.Invoke(held);
            }
            SnapshotChanged?.Invoke(snapshot);
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
            public VesselEnvelope[] vessels;
            public InputWatermark[] inputWatermarks;
            public ObservationEnvelope[] observations;
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
            public double value;
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
        }

        private void OnDisable()
        {
            Session?.Dispose();
            Session = null;
        }
    }
}
