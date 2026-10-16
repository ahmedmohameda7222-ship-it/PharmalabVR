using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PharmaLabVR.Core;
using PharmaLabVR.Lab;
using PharmaLabVR.UI;
using PharmaLabVR.Tools;
using PharmaLabVR.Input;
using UnityEngine;

namespace PharmaLabVR.Session
{
    public sealed class SessionController : MonoBehaviour
    {
        [SerializeField] private CoreDriver driver;
        [SerializeField] private string saveName = "autosave";
        [SerializeField] private string checkpointId = "student-checkpoint";
        public string LastDurableSavePath { get; private set; }
        public string LastPortableArchivePath { get; private set; }
        public string LastCsvExportPath { get; private set; }
        public string LastError { get; private set; }
        public RecordedSessionReview RecordedReview { get; private set; }
        public RecordedRunComparison CurrentComparison { get; private set; }
        private readonly SaveService saves = new();
        private readonly NotebookService notebook = new();
        public static Rect DesktopHudBounds => new Rect(12f, 12f,
            Mathf.Max(1f, Mathf.Min(370f, Screen.width - 24f)),
            Mathf.Max(1f, Mathf.Min(620f, Screen.height - 24f)));
        private int selectedConcentrationIndex = 2;
        private Vector2 hudScrollPosition;
        private string notebookDraft = string.Empty;
        private ScreenRouter router;
        private LabPanelController panels;
        private SpatialPanelPresenter spatialPanel;

        private void Awake()
        {
            router = GetComponent<ScreenRouter>();
            if (router == null) router = gameObject.AddComponent<ScreenRouter>();
            router.Configure(driver);
            panels = GetComponent<LabPanelController>();
            if (panels == null) panels = gameObject.AddComponent<LabPanelController>();
            panels.Configure(router, driver, this);
            spatialPanel = GetComponent<SpatialPanelPresenter>();
            if (spatialPanel == null) spatialPanel = gameObject.AddComponent<SpatialPanelPresenter>();
            spatialPanel.Configure(router, panels, driver);
        }

        public void Configure(CoreDriver coreDriver, string sessionName = null)
        {
            driver = coreDriver;
            router?.Configure(driver);
            panels?.Configure(router, driver, this);
            spatialPanel?.Configure(router, panels, driver);
            if (!string.IsNullOrEmpty(sessionName)) saveName = sessionName;
        }

        public bool CanReleaseTool
        {
            get
            {
                if (driver == null || driver.CurrentMode != "VR") return false;
                var dock = driver.transform.root.GetComponentInChildren<ToolMount>(true);
                return dock != null && dock.IsMounted;
            }
        }

        public bool CanParkTool
        {
            get
            {
                if (driver?.Session == null || driver.CurrentMode != "VR") return false;
                var dock = driver.transform.root.GetComponentInChildren<ToolMount>(true);
                return dock != null && !dock.IsMounted;
            }
        }

        public bool CanLoadSavedSession
        {
            get
            {
                if (driver?.Session == null) return false;
                // The dock is a VR-only safety prerequisite. Desktop has no
                // physical tool dock, but still loads only via Pause/Hold.
                if (driver.CurrentMode == "VR" && !CanReleaseTool) return false;
                if (driver.CurrentMode != "VR" && driver.CurrentMode != "Desktop")
                    return false;
                var file = Path.Combine(Application.persistentDataPath, "sessions",
                    saveName + ".plv.json");
                return SaveService.HasSaveCandidate(file);
            }
        }

        public bool LoadSavedSession()
        {
            var screen = router?.Current?.ScreenId;
            if (screen != ScreenId.Pause && screen != ScreenId.Hold) return false;
            if (!CanLoadSavedSession)
            {
                LastError = driver?.CurrentMode == "VR"
                    ? "Load requires a parked VR tool and an existing saved session."
                    : "Load requires an existing saved session.";
                router.Show(screen.Value);
                return false;
            }
            var file = Path.Combine(Application.persistentDataPath, "sessions",
                saveName + ".plv.json");
            var result = ContinueFrom(file);
            if (!result && router?.Current != null) router.Show(router.Current.ScreenId);
            return result;
        }

        public bool ParkToolAtDock()
        {
            var screen = router?.Current?.ScreenId;
            if (!CanParkTool || (screen != ScreenId.Lab &&
                screen != ScreenId.Pause && screen != ScreenId.Hold)) return false;
            var mount = driver.transform.root.GetComponentInChildren<ToolMount>(true);
            if (mount == null || !mount.TryMount()) return false;
            // Presentation/physics only. Do not Save, submit Native PlaceTool,
            // update observation revisions or silently release a Native hold.
            router.Show(screen.Value);
            return true;
        }

        public bool ReleaseToolFromDock()
        {
            if (!CanReleaseTool || router?.Current?.ScreenId != ScreenId.Lab) return false;
            var dock = driver.transform.root.GetComponentInChildren<ToolMount>(true);
            if (dock == null || !dock.ReleaseFromDock()) return false;
            router.Show(ScreenId.Lab);
            return true;
        }

        public bool TrySnapTurn(int direction)
        {
            if (direction != -1 && direction != 1 || !CanAlignXrView(out var alignment))
                return false;
            return alignment.TrySnapTurn(direction);
        }

        public bool TryRecenterView()
        {
            if (!CanAlignXrView(out var alignment)) return false;
            // Same Native-held safety boundary as snap-turn.
            return alignment.TryRecenter();
        }

        private bool CanAlignXrView(out RecoveryAlignment alignment)
        {
            alignment = null;
            if (driver?.Session == null || router?.Current == null) return false;
            var root = driver.transform.root;
            var dock = root.GetComponentInChildren<ToolMount>(true);
            alignment = root.GetComponentInChildren<RecoveryAlignment>(true);
            var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            var tracked = head.isValid &&
                head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracking) && tracking;
            if (!RecoveryAlignmentPolicy.CanRecenter(driver.CurrentMode == "VR",
                    driver.IsNativePaused, driver.IsTimeHeld, tracked,
                    dock != null && dock.IsMounted)) return false;
            // Neither view operation submits Native Continue or changes material.
            // Continue still needs fresh neutral input through the Native authority.
            return alignment != null;
        }

        public bool RequestDesktopPause()
        {
            if (driver?.Session == null || driver.CurrentMode != "Desktop" ||
                router?.Current == null || panels == null) return false;
            return panels.Request(PanelAction.Pause);
        }

        public static bool PointerOverDesktopHud(Vector2 screenPosition)
        {
            return DesktopHudBounds.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        private void OnGUI()
        {
            if (driver == null || driver.CurrentMode != "Desktop") return;
            var previousLabelSize = GUI.skin.label.fontSize;
            var previousButtonSize = GUI.skin.button.fontSize;
            try
            {
                var textScale = router?.Current?.Setup.TextScale ?? 1f;
                GUI.skin.label.fontSize = Mathf.RoundToInt(14f * textScale);
                GUI.skin.button.fontSize = Mathf.RoundToInt(14f * textScale);
                GUILayout.BeginArea(DesktopHudBounds, GUI.skin.box);
            hudScrollPosition = GUILayout.BeginScrollView(hudScrollPosition);
            GUILayout.Label("PharmaLabVR · Research Lab");
            var screen = router?.Current?.ScreenId ?? ScreenId.Lab;
            GUILayout.Label(screen.ToString());
            if (!string.IsNullOrEmpty(driver.StartupError))
            {
                GUILayout.Label("Lab unavailable: " + driver.StartupError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Hold)
            {
                GUILayout.Label("Paused: " + driver.HoldReason + ". Close the valve and restore tracking.");
                if (!driver.NativeRecoveryReady)
                    GUILayout.Label(driver.HoldReason == "Compute" ?
                        "Waiting for the scientific result; saving and review remain available." :
                        "Waiting for a fresh neutral input baseline.");
                GUI.enabled = driver.NativeRecoveryReady;
                if (GUILayout.Button("Continue", GUILayout.Height(44f))) panels?.Request(PanelAction.Continue);
                GUI.enabled = true;
                if (GUILayout.Button("Save session", GUILayout.Height(44f))) panels?.Request(PanelAction.Save);
                GUI.enabled = CanLoadSavedSession;
                if (GUILayout.Button("Load saved session", GUILayout.Height(44f))) panels?.Request(PanelAction.Load);
                GUI.enabled = true;
                if (GUILayout.Button("Open recorded review", GUILayout.Height(44f))) panels?.Request(PanelAction.Review);
                if (GUILayout.Button("Settings", GUILayout.Height(44f))) panels?.Request(PanelAction.Settings);
                if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Pause)
            {
                GUILayout.Label("Session paused. Material and simulation time are frozen.");
                if (GUILayout.Button("Return to lab", GUILayout.Height(44f))) panels?.Request(PanelAction.Lab);
                if (GUILayout.Button("Save session", GUILayout.Height(44f))) panels?.Request(PanelAction.Save);
                GUI.enabled = CanLoadSavedSession;
                if (GUILayout.Button("Load saved session", GUILayout.Height(44f))) panels?.Request(PanelAction.Load);
                GUI.enabled = true;
                if (GUILayout.Button("Open recorded review", GUILayout.Height(44f))) panels?.Request(PanelAction.Review);
                if (GUILayout.Button("Settings", GUILayout.Height(44f))) panels?.Request(PanelAction.Settings);
                if (GUILayout.Button("Restart checkpoint", GUILayout.Height(44f))) RestartCheckpoint();
                if (GUILayout.Button("Export portable session + notebook", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.ExportPortable);
                if (!string.IsNullOrEmpty(LastPortableArchivePath))
                    GUILayout.Label("Portable archive written: " + LastPortableArchivePath);
                if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Review)
            {
                if (RecordedReview == null) GUILayout.Label("No recorded review is open.");
                else
                {
                    GUILayout.Label("Recorded-only review · no model recomputation");
                    GUILayout.Label("Saved branch: " + RecordedReview.BranchId);
                    GUILayout.Label("Saved model identity: " + RecordedReview.ModelIdentity);
                    GUILayout.Label("Native command receipts (ordered; no synthetic timestamps): " +
                        RecordedReview.CommandReceipts.Count);
                    foreach (var receipt in RecordedReview.CommandReceipts.Skip(
                        Math.Max(0, RecordedReview.CommandReceipts.Count - 8)))
                        GUILayout.Label("#" + receipt.CommandSequence + " " + receipt.CommandType +
                            " · " + (receipt.Accepted ? "Accepted" : receipt.OutcomeCode));
                    foreach (var observation in RecordedReview.Observations)
                        GUILayout.Label($"Recorded {observation.vesselId} pH: {observation.DisplayValue} " +
                            $"({observation.freshness}, rev {observation.asOfMaterialRevision})");
                }
                if (GUILayout.Button("Return to lab", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Lab);
                if (GUILayout.Button("Settings", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Settings);
                if (GUILayout.Button("Export recorded observations + commands (CSV)", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.ExportCsv);
                var comparisonEnabled = GUI.enabled;
                GUI.enabled = comparisonEnabled && CanCompareSaved;
                var requestComparison = GUILayout.Button("Compare with last saved Native checkpoint",
                    GUILayout.Height(44f));
                GUI.enabled = comparisonEnabled;
                if (requestComparison) panels?.Request(PanelAction.CompareSaved);
                if (!string.IsNullOrEmpty(LastCsvExportPath))
                    GUILayout.Label("CSV written: " + LastCsvExportPath);
                if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Compare)
            {
                GUILayout.Label("Two recorded Native runs · no recomputation");
                if (CurrentComparison == null) GUILayout.Label("Comparison unavailable.");
                else
                {
                    GUILayout.Label("Saved baseline branch: " + CurrentComparison.BaselineBranchId);
                    GUILayout.Label("Review candidate branch: " + CurrentComparison.CandidateBranchId);
                    GUILayout.Label("Baseline package: " + CurrentComparison.BaselineModelIdentity);
                    GUILayout.Label("Candidate package: " + CurrentComparison.CandidateModelIdentity);
                    foreach (var row in CurrentComparison.Rows)
                    {
                        var difference = row.DeltaAvailable ?
                            row.Delta.ToString("+0.00;-0.00;0.00",
                                System.Globalization.CultureInfo.InvariantCulture) : "--";
                        GUILayout.Label(row.VesselId + " " + row.ObservableId + " · saved " +
                            row.BaselineDisplay + " (" + row.BaselineStatus + ") vs review " +
                            row.CandidateDisplay + " (" + row.CandidateStatus + ") · Δ " +
                            difference + "\n" + row.DeltaReason);
                    }
                }
                if (GUILayout.Button("Back to recorded review", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Review);
                if (GUILayout.Button("Return to lab", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Lab);
                if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Notebook)
            {
                GUILayout.Label("Research notebook · notes reference actual Native events.");
                var notes = ReadNotebookEntries();
                foreach (var note in notes.Skip(Math.Max(0, notes.Count - 5)))
                    GUILayout.Label("Event " + note.EventSequence + " · " + note.VesselId +
                        " · " + note.RecordedAtUtc + "\n" + note.Text);
                GUILayout.Label("Native event revision: " + router.Current.NativeRevision +
                    ". Notes do not change the scientific state.");
                notebookDraft = GUILayout.TextArea(notebookDraft, GUILayout.Height(96f));
                if (GUILayout.Button("Record note for current event", GUILayout.Height(44f)) &&
                    panels?.Request(PanelAction.AddNotebookNote, notebookDraft) == true)
                    notebookDraft = string.Empty;
                if (GUILayout.Button("Return to lab", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Lab);
                if (GUILayout.Button("Save Native session", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Save);
                if (GUILayout.Button("Export portable session + notebook", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.ExportPortable);
                if (!string.IsNullOrEmpty(LastPortableArchivePath))
                    GUILayout.Label("Portable archive written: " + LastPortableArchivePath);
                if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Settings)
            {
                SetupPreferenceControls.Draw(router);
                if (GUILayout.Button("Back", GUILayout.Height(44f))) panels?.Request(PanelAction.Back);
                if (GUILayout.Button("Save session", GUILayout.Height(44f))) panels?.Request(PanelAction.Save);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Lab && driver.SourceNeedsPreparation)
            {
                GUILayout.Label("Prepare the source stock · Research aqueous profile");
                var concentrations = driver.ResearchConcentrations;
                if (concentrations.Count > 0)
                {
                    selectedConcentrationIndex = Mathf.Clamp(selectedConcentrationIndex, 0, concentrations.Count - 1);
                    GUILayout.BeginHorizontal();
                    for (var index = 0; index < concentrations.Count; index++)
                        if (GUILayout.Toggle(selectedConcentrationIndex == index,
                            concentrations[index].ToString("0.00") + " mol/L", GUI.skin.button))
                            selectedConcentrationIndex = index;
                    GUILayout.EndHorizontal();
                }
                foreach (var stockId in driver.AvailableStockIds)
                    if (GUILayout.Button("Prepare " + stockId, GUILayout.Height(28f)))
                        SelectStock(stockId, stockId == "water" ? 0.0 : concentrations[selectedConcentrationIndex]);
                GUILayout.Label("Stock volume: 25 ml. Prepared quantities come from the native model.");
            }
            if (screen == ScreenId.Lab && driver.CurrentMode == "Desktop")
                GUILayout.Label("Desktop controls: F select/place on the bench; Shift+left-drag = held tool plane; " +
                    "left-drag = held burette valve; mouse wheel = held height / empty-hand zoom; " +
                    "Q/E = held tilt; right-drag = camera; WASD = camera; Esc = Native Pause.");
            if (screen == ScreenId.Lab && driver.SourceNeedsPreparation && !driver.ReserveStockPrepared)
            {
                GUILayout.Label("Optional: prepare reserve stock BEFORE primary stock (no material created after setup).");
                var concentrations = driver.ResearchConcentrations;
                foreach (var id in driver.AvailableStockIds)
                {
                    var stock = id;
                    if (GUILayout.Button("Prepare reserve " + stock, GUILayout.Height(27f)))
                        TryPrepareResearchReserveStock(stock,
                            stock == "water" ? 0.0 : concentrations[Mathf.Clamp(
                                selectedConcentrationIndex, 0, concentrations.Count - 1)]);
                }
            }
            if (screen == ScreenId.Lab && driver.SourceNeedsPreparation && !driver.RinseWaterPrepared)
            {
                GUILayout.Label("Optional: prepare measured clean water before entering the live lab.");
                if (GUILayout.Button("Prepare 1 ml rinse water (Native setup)", GUILayout.Height(32f)))
                    TryPrepareResearchRinseWater();
            }
            if (screen == ScreenId.Lab && !driver.SourceNeedsPreparation)
            {
                GUILayout.Label("Native Research instruments · no calibrated physical flow");
                if (GUILayout.Button("Pipette: aspirate 0.05 ml", GUILayout.Height(32f)))
                    TryPipetteAspirate();
                GUILayout.Label($"Pipette tip: {PipetteTipVolumeM3 * 1e6:0.00} ml (Research retained tip 0.10 ml). " +
                    "A 0.05 ml dispense requires at least 0.15 ml staged over repeated Native aspiration intents.");
                var previousGuiEnabled = GUI.enabled;
                GUI.enabled = previousGuiEnabled && CanPipetteDispense;
                var requestedPipetteDispense = GUILayout.Button("Pipette: dispense 0.05 ml", GUILayout.Height(32f));
                GUI.enabled = previousGuiEnabled;
                if (requestedPipetteDispense) TryPipetteDispense();
                if (GUILayout.Button("Bottle: pour 0.01 ml", GUILayout.Height(32f)))
                    TryBottleMeasuredPour();
                if (driver.ReserveStockPrepared)
                {
                    if (GUILayout.Button("Pipette: switch to prepared reserve", GUILayout.Height(32f)))
                        TryPipetteSwitchReserve();
                    if (GUILayout.Button("Bottle: refill from prepared reserve 0.01 ml", GUILayout.Height(32f)))
                        TryBottleRefillFromReserve();
                }
                if (driver.RinseWaterPrepared)
                {
                    if (GUILayout.Button("Pipette: water rinse 0.01 ml", GUILayout.Height(32f)))
                        TryPipetteWaterRinse();
                    if (GUILayout.Button("Bottle: water rinse 0.01 ml", GUILayout.Height(32f)))
                        TryBottleWaterRinse();
                }
                else GUILayout.Label("Rinse unavailable: no clean water was prepared during setup.");
            }
            var measurement = driver.Measurement;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Inspect source", GUILayout.Height(28f))) panels?.Request(PanelAction.Inspect, "source");
            if (GUILayout.Button("Inspect receiver", GUILayout.Height(28f))) panels?.Request(PanelAction.Inspect, "receiver");
            GUILayout.EndHorizontal();
            if (screen == ScreenId.Inspect || screen == ScreenId.Measure)
            {
                GUILayout.Label(measurement == null ? "pH: pending" :
                    $"pH: {measurement.DisplayValue} ({measurement.Availability})");
                if (measurement != null) GUILayout.Label(measurement.Explanation);
                if (screen == ScreenId.Inspect && GUILayout.Button("Measure selected vessel", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Measure, router.Current.SelectedEntityId);
                if (GUILayout.Button("Return to lab", GUILayout.Height(44f))) panels?.Request(PanelAction.Lab);
            }
            if (GUILayout.Button("Pause", GUILayout.Height(44f))) panels?.Request(PanelAction.Pause);
            if (GUILayout.Button("Save session", GUILayout.Height(30f))) panels?.Request(PanelAction.Save);
            var savedPath = Path.Combine(Application.persistentDataPath, "sessions", saveName + ".plv.json");
            // Live Lab never performs an unguarded Native context replacement.
            // Saved-session restore is an explicit Pause/Hold panel action, where
            // recovery baselines and (for VR) parked tool admission are enforced.
            if (SaveService.HasSaveCandidate(savedPath))
                GUILayout.Label("To load a saved session, pause the lab first.");
            if (GUILayout.Button("Create checkpoint", GUILayout.Height(30f))) CreateCheckpoint();
            if (GUILayout.Button("Restart checkpoint", GUILayout.Height(30f))) RestartCheckpoint();
            if (router?.Current?.SupportedActions.Contains("Review") == true &&
                GUILayout.Button("Open recorded review", GUILayout.Height(44f)))
                panels?.Request(PanelAction.Review);
            if (router?.Current?.SupportedActions.Contains("Notebook") == true &&
                GUILayout.Button("Open research notebook", GUILayout.Height(44f)))
                panels?.Request(PanelAction.Notebook);
            if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            }
            finally
            {
                GUI.skin.label.fontSize = previousLabelSize;
                GUI.skin.button.fontSize = previousButtonSize;
            }
        }

        // Actions are public so Desktop and future world-space XR panels can use
        // the same transaction/receipt boundary. They do not fabricate chemistry.
        private PipetteActuator ResearchPipette
        {
            get
            {
                var tool = GetComponent<PipetteActuator>();
                if (tool == null && driver != null)
                    tool = driver.transform.root.GetComponentInChildren<PipetteActuator>(true);
                if (tool == null) tool = gameObject.AddComponent<PipetteActuator>();
                tool.Configure(driver);
                return tool;
            }
        }

        private BottlePourActuator ResearchBottle
        {
            get
            {
                var tool = GetComponent<BottlePourActuator>();
                if (tool == null && driver != null)
                    tool = driver.transform.root.GetComponentInChildren<BottlePourActuator>(true);
                if (tool == null) tool = gameObject.AddComponent<BottlePourActuator>();
                tool.Configure(driver);
                return tool;
            }
        }

        private bool TryResearchCommand(Func<LabCommandReceipt> command)
        {
            try
            {
                if (driver?.Session == null || driver.SourceNeedsPreparation ||
                    driver.IsNativePaused || driver.IsTimeHeld)
                    throw new InvalidOperationException("Prepare stock and leave Native Pause/Hold before transfer.");
                var receipt = command();
                if (!receipt.Accepted)
                    throw new InvalidOperationException("Native instrument refused: " + receipt.Code);
                driver.RefreshAfterInstrumentCommand();
                LastError = null;
                return true;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                return false;
            }
        }

        public double PipetteTipVolumeM3
        {
            get
            {
                if (driver?.Session == null) return 0.0;
                try { return ResearchPipette.RepresentedTipVolumeM3; }
                catch (InvalidOperationException) { return 0.0; }
            }
        }

        public bool CanPipetteDispense =>
            driver?.Session != null && !driver.SourceNeedsPreparation &&
            !driver.IsNativePaused && !driver.IsTimeHeld &&
            ResearchInstrumentPayloads.CanDispensePipette(PipetteTipVolumeM3,
                ResearchInstrumentPayloads.DefaultPipetteDispenseM3);

        public bool TryPipetteAspirate() => TryResearchCommand(() => ResearchPipette.Aspirate(5e-8));

        public bool TryPipetteDispense()
        {
            if (!CanPipetteDispense)
            {
                LastError = "Pipette retained tip is 0.10 ml (Research). " +
                    "Aspirate additional real prepared stock before dispensing 0.05 ml.";
                return false;
            }
            return TryResearchCommand(() => ResearchPipette.Dispense(
                ResearchInstrumentPayloads.DefaultPipetteDispenseM3));
        }
        public bool TryBottleMeasuredPour() => TryResearchCommand(() => ResearchBottle.Pour(1e-8));
        public bool TryPipetteWaterRinse() => TryResearchCommand(() => ResearchPipette.Rinse(1e-8));
        public bool TryBottleWaterRinse() => TryResearchCommand(() => ResearchBottle.Rinse(1e-8));
        public bool TryPipetteSwitchReserve() => TryResearchCommand(() =>
            ResearchPipette.SwitchPreparedSource("reserve-stock"));
        public bool TryBottleRefillFromReserve() => TryResearchCommand(() =>
            ResearchBottle.RefillFromPreparedStock("reserve-stock", 1e-8));

        public bool TryPrepareResearchReserveStock(string stockId, double concentrationMolPerL)
        {
            try
            {
                if (driver == null || !driver.TryPrepareReserveStock(stockId, concentrationMolPerL))
                    throw new InvalidOperationException(
                        "Reserve stock must be deliberately prepared before leaving Native setup.");
                LastError = null;
                return true;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                return false;
            }
        }


        public bool TryPrepareResearchRinseWater()
        {
            try
            {
                if (driver == null || !driver.TryPrepareRinseWater())
                    throw new InvalidOperationException("Clean Native rinse water can only be prepared during setup.");
                LastError = null;
                return true;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                return false;
            }
        }

        public bool SelectStock(string stockId, double concentrationMolPerL)
        {
            try
            {
                if (driver == null || !driver.TryPrepareSourceStock(stockId, concentrationMolPerL))
                    throw new InvalidOperationException("Stock selection was rejected by the native session or Research catalog.");
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }


        private string NotebookFilePath()
        {
            if (string.IsNullOrWhiteSpace(saveName) || Path.GetFileName(saveName) != saveName ||
                saveName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidOperationException("Notebook session filename is not safe.");
            return Path.Combine(Application.persistentDataPath, "sessions", saveName + ".plv-notes");
        }

        public IReadOnlyList<NotebookEntry> ReadNotebookEntries()
        {
            try
            {
                if (driver?.Session == null || !driver.Session.IsOpen)
                    throw new InvalidOperationException("No open Native session.");
                // Reading a healthy notebook must not erase an earlier failed
                // note-commit error before Desktop/XR has rendered it.
                return notebook.Read(NotebookFilePath(), driver.Session.BranchId);
            }
            catch (Exception exception)
            {
                LastError = "Research notebook unavailable: " + exception.Message;
                return Array.Empty<NotebookEntry>();
            }
        }

        public string NotebookSummary
        {
            get
            {
                var entries = ReadNotebookEntries();
                if (entries.Count == 0) return "Research notebook: no recorded annotations.";
                var latest = entries[entries.Count - 1];
                var preview = latest.Text.Replace('\r', ' ').Replace('\n', ' ');
                if (preview.Length > 105) preview = preview.Substring(0, 105) + "...";
                return "Research notebook · " + entries.Count + " event-linked notes\n" +
                    "Latest · event " + latest.EventSequence + " · " + latest.VesselId + "\n" +
                    preview + "\nDesktop: write a note; XR: read recorded notes.";
            }
        }

        public bool AppendNotebookNote(string value)
        {
            try
            {
                if (router?.Current?.ScreenId != ScreenId.Notebook ||
                    driver?.Session == null || !driver.Session.IsOpen ||
                    driver.CurrentLabView == null || driver.IsTimeHeld)
                    throw new InvalidOperationException("Notes require an active Notebook and Native snapshot.");
                var nativeRevision = driver.CurrentLabView.EventSequence;
                if (nativeRevision != router.Current.NativeRevision)
                    throw new InvalidOperationException("Native event revision changed before note commit.");
                notebook.Append(NotebookFilePath(), driver.Session.BranchId, nativeRevision,
                    router.Current.SelectedEntityId, value);
                LastError = null;
                router.Show(ScreenId.Notebook);
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }

        public bool ExportRecordedCsv()
        {
            LastCsvExportPath = null;
            try
            {
                if (router?.Current?.ScreenId != ScreenId.Review || RecordedReview == null)
                    throw new InvalidOperationException("CSV export requires an open recorded Native Review.");
                var notesPath = NotebookFilePath(); // Enforces safe session file naming.
                var target = Path.Combine(Path.GetDirectoryName(notesPath), saveName + ".plv-results.csv");
                SessionExportService.WriteCsv(target, RecordedReview);
                LastCsvExportPath = target;
                LastError = null;
                return true;
            }
            catch (Exception error)
            {
                LastError = "Recorded CSV export failed: " + error.Message;
                return false;
            }
        }

        public bool ExportPortableSession()
        {
            LastPortableArchivePath = null;
            try
            {
                if (router?.Current == null ||
                    (router.Current.ScreenId != ScreenId.Pause &&
                     router.Current.ScreenId != ScreenId.Notebook) ||
                    driver?.Session == null || !driver.Session.IsOpen ||
                    driver.CurrentLabView == null)
                    throw new InvalidOperationException("Portable export requires Pause/Notebook and an open Native session.");
                // The Native export and lab view must identify the same revision.
                // The archive validates exact Native branch and revision before writing.
                var branch = driver.Session.BranchId;
                var revision = driver.CurrentLabView.EventSequence;
                if (revision != router.Current.NativeRevision)
                    throw new InvalidOperationException("Native revision changed before archive export.");
                var notesPath = NotebookFilePath();
                var notes = notebook.ExportBranch(notesPath, branch, revision);
                var target = Path.Combine(Path.GetDirectoryName(notesPath), saveName + ".plvpack");
                PortableSessionArchive.Write(target, driver.Session.ExportSession(),
                    notes, branch, revision);
                LastPortableArchivePath = target;
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = "Portable export failed: " + exception.Message;
                return false;
            }
        }

        public bool Save()
        {
            try
            {
                if (driver == null) throw new InvalidOperationException("Core driver is not assigned.");
                LastDurableSavePath = saves.Save(driver.Session, Path.Combine(Application.persistentDataPath, "sessions"), saveName);
                LastError = null;
                router?.MarkSaved();
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                router?.MarkSaveFailed();
                return false;
            }
        }

        private string SavedNativePath()
        {
            var notesPath = NotebookFilePath(); // Validate safe session naming.
            return Path.Combine(Path.GetDirectoryName(notesPath), saveName + ".plv.json");
        }

        public bool CanCompareSaved
        {
            get
            {
                if (RecordedReview == null) return false;
                try { return SaveService.HasSaveCandidate(SavedNativePath()); }
                catch (ArgumentException) { return false; }
                catch (InvalidOperationException) { return false; }
            }
        }

        public string ComparisonSummary
        {
            get
            {
                var comparison = CurrentComparison;
                if (comparison == null) return "No recorded run comparison is open.";
                var summary = "Saved " + comparison.BaselineBranchId + " vs review " +
                    comparison.CandidateBranchId + " (recorded; no recomputation)";
                var shown = 0;
                foreach (var row in comparison.Rows)
                {
                    if (shown++ >= 2) break;
                    var difference = row.DeltaAvailable ?
                        row.Delta.ToString("+0.00;-0.00;0.00",
                            System.Globalization.CultureInfo.InvariantCulture) : "--";
                    summary += "\n" + row.VesselId + " pH: " + row.BaselineDisplay +
                        " (" + row.BaselineStatus + ") / " + row.CandidateDisplay +
                        " (" + row.CandidateStatus + ") · Δ " + difference;
                }
                if (comparison.Rows.Count == 0) summary += "\nNo common or recorded observations.";
                return summary;
            }
        }

        public bool OpenSavedComparison()
        {
            CurrentComparison = null;
            try
            {
                if (router?.Current?.ScreenId != ScreenId.Review || RecordedReview == null)
                    throw new InvalidOperationException("Compare requires an open recorded Review.");
                var path = SavedNativePath();
                if (!SaveService.HasSaveCandidate(path))
                    throw new InvalidDataException("No confirmed saved Native checkpoint is available.");
                RecordedSessionReview baseline;
                try { baseline = RecordedSessionReview.Open(path); }
                catch (Exception error) when (error is InvalidDataException ||
                                              error is IOException || error is ArgumentException)
                {
                    // A saved checkpoint backup is a real second recorded source.
                    // Never substitute a fabricated baseline or alter live Native.
                    baseline = RecordedSessionReview.Open(path + ".bak");
                }
                CurrentComparison = RecordedRunComparison.Compare(baseline, RecordedReview);
                LastError = null;
                return true;
            }
            catch (Exception error)
            {
                LastError = "Saved recorded comparison unavailable: " + error.Message;
                return false;
            }
        }

        public bool OpenReview()
        {
            if (RecordedReview != null) return true;
            try
            {
                if (driver?.Session == null) throw new InvalidOperationException("No open native session.");
                RecordedReview = RecordedSessionReview.FromJson(driver.Session.ExportSession());
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = "Recorded review unavailable: " + exception.Message;
                return false;
            }
        }

        public void CloseReview()
        {
            RecordedReview = null;
            CurrentComparison = null;
        }

        public bool ContinueFrom(string path)
        {
            CoreSession imported = null;
            RecordedReview = null;
            CurrentComparison = null;
            try
            {
                if (driver == null) throw new InvalidOperationException("Core driver is not assigned.");
                imported = saves.Load(path, driver.ScientificDatabasePath);
                driver.ReplaceSession(imported);
                imported = null;
                RecordedReview = null;
                LastError = null;
                router?.MarkSaved();
                return true;
            }
            catch (ScientificPackageMismatchException exception)
            {
                imported?.Dispose();
                try { RecordedReview = RecordedSessionReview.Open(path); }
                catch (Exception reviewError)
                {
                    LastError = "Live resume blocked and recorded review unavailable: " + reviewError.Message;
                    return false;
                }
                LastError = "Live resume blocked: " + exception.Message + " Viewing saved observations only.";
                router?.Show(ScreenId.Review);
                return false;
            }
            catch (Exception exception)
            {
                imported?.Dispose();
                LastError = exception.Message;
                return false;
            }
        }

        public bool CreateCheckpoint()
        {
            try
            {
                if (driver == null) throw new InvalidOperationException("Core driver is not assigned.");
                if (!driver.CreateCheckpoint(checkpointId)) throw new InvalidOperationException("Native checkpoint was rejected.");
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }

        public bool RestartCheckpoint()
        {
            try
            {
                if (driver == null) throw new InvalidOperationException("Core driver is not assigned.");
                if (!driver.RestartCheckpoint(checkpointId)) throw new InvalidOperationException("Native checkpoint restart was rejected.");
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }

        public bool ContinueHeldSession()
        {
            try
            {
                if (driver == null || !driver.ResumeAfterTimeHold())
                    throw new InvalidOperationException("Fresh neutral tracked baselines are required before continuing.");
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }
    }
}
