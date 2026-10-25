using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PharmaLabVR.Core;
using PharmaLabVR.Lab;
using UnityEngine;

namespace PharmaLabVR.UI
{
    public enum ScreenId
    {
        Home, Setup, Stations, Lab, CompoundLibrary, Prepare, Inspect, Measure, Learn,
        Notebook, Review, Compare, Pause, Hold, Settings, LessonBuilder, ResearchProject
    }


    // Session-scoped presentation choices. No native inventory or scientific data is stored here.
    public sealed class LabSetupPreferences
    {
        public string Posture { get; }
        public string DominantHand { get; }
        public float BenchHeightMeters { get; }
        public float TextScale { get; }
        public bool CaptionsEnabled { get; }
        public bool ReducedMotion { get; }
        public bool HighContrast { get; }
        public float AudioVolume { get; }

        public LabSetupPreferences(string posture, string dominantHand, float benchHeightMeters,
            float textScale, bool captionsEnabled, bool reducedMotion, bool highContrast,
            float audioVolume = 1f)
        {
            if (posture != "Seated" && posture != "Standing")
                throw new ArgumentException("Unsupported posture.", nameof(posture));
            if (dominantHand != "Left" && dominantHand != "Right")
                throw new ArgumentException("Unsupported dominant hand.", nameof(dominantHand));
            if (float.IsNaN(benchHeightMeters) || benchHeightMeters < 0.65f || benchHeightMeters > 1.05f)
                throw new ArgumentOutOfRangeException(nameof(benchHeightMeters));
            if (float.IsNaN(textScale) || textScale < 0.85f || textScale > 1.35f)
                throw new ArgumentOutOfRangeException(nameof(textScale));
            if (float.IsNaN(audioVolume) || audioVolume < 0f || audioVolume > 1f)
                throw new ArgumentOutOfRangeException(nameof(audioVolume));
            Posture = posture;
            DominantHand = dominantHand;
            BenchHeightMeters = benchHeightMeters;
            TextScale = textScale;
            CaptionsEnabled = captionsEnabled;
            ReducedMotion = reducedMotion;
            HighContrast = highContrast;
            AudioVolume = audioVolume;
        }

        public LabSetupPreferences With(string posture = null, string dominantHand = null,
            float? benchHeightMeters = null, float? textScale = null, bool? captionsEnabled = null,
            bool? reducedMotion = null, bool? highContrast = null, float? audioVolume = null)
        {
            return new LabSetupPreferences(posture ?? Posture, dominantHand ?? DominantHand,
                benchHeightMeters ?? BenchHeightMeters, textScale ?? TextScale,
                captionsEnabled ?? CaptionsEnabled, reducedMotion ?? ReducedMotion,
                highContrast ?? HighContrast, audioVolume ?? AudioVolume);
        }
    }

    // The Boot scene is unloaded before the Lab scene is activated; this selection
    // survives that scene transition without writing a chemistry save or PlayerPrefs.
    public static class SetupPreferencesSelection
    {
        public static LabSetupPreferences Current { get; private set; } =
            new LabSetupPreferences("Standing", "Right", 0.85f, 1f, true, false, false);

        public static event Action<LabSetupPreferences> Changed;

        public static void Select(LabSetupPreferences preferences)
        {
            Current = preferences ?? throw new ArgumentNullException(nameof(preferences));
            Changed?.Invoke(Current);
        }
    }

    // Same real IMGUI preference controls on Boot Setup and Desktop Settings.
    // Placement and comfort simulation do not replace P07.03 device calibration.
    internal static class SetupPreferenceControls
    {
        public static void Draw(ScreenRouter router)
        {
            if (router?.Current == null) return;
            var selected = router.Current.Setup;
            GUILayout.Label("Posture");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(selected.Posture == "Seated" ? "● Seated" : "Seated"))
                router.UpdateSetupPreferences(selected.With(posture: "Seated"));
            if (GUILayout.Button(selected.Posture == "Standing" ? "● Standing" : "Standing"))
                router.UpdateSetupPreferences(selected.With(posture: "Standing"));
            GUILayout.EndHorizontal();
            selected = router.Current.Setup;
            GUILayout.Label("Dominant hand");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(selected.DominantHand == "Left" ? "● Left" : "Left"))
                router.UpdateSetupPreferences(selected.With(dominantHand: "Left"));
            if (GUILayout.Button(selected.DominantHand == "Right" ? "● Right" : "Right"))
                router.UpdateSetupPreferences(selected.With(dominantHand: "Right"));
            GUILayout.EndHorizontal();
            selected = router.Current.Setup;
            GUILayout.Label($"Bench viewing height: {selected.BenchHeightMeters:0.00} m (0.65–1.05 m)");
            var height = GUILayout.HorizontalSlider(selected.BenchHeightMeters, 0.65f, 1.05f);
            if (!Mathf.Approximately(height, selected.BenchHeightMeters))
                router.UpdateSetupPreferences(selected.With(benchHeightMeters: height));
            selected = router.Current.Setup;
            GUILayout.Label($"Text size: {selected.TextScale:0.00}×");
            var scale = GUILayout.HorizontalSlider(selected.TextScale, 0.85f, 1.35f);
            if (!Mathf.Approximately(scale, selected.TextScale))
                router.UpdateSetupPreferences(selected.With(textScale: scale));
            selected = router.Current.Setup;
            var captions = GUILayout.Toggle(selected.CaptionsEnabled, "Captions / textual action hints");
            if (captions != selected.CaptionsEnabled)
                router.UpdateSetupPreferences(selected.With(captionsEnabled: captions));
            selected = router.Current.Setup;
            var motion = GUILayout.Toggle(selected.ReducedMotion, "Reduced UI motion");
            if (motion != selected.ReducedMotion)
                router.UpdateSetupPreferences(selected.With(reducedMotion: motion));
            selected = router.Current.Setup;
            var contrast = GUILayout.Toggle(selected.HighContrast, "High-contrast panel");
            if (contrast != selected.HighContrast)
                router.UpdateSetupPreferences(selected.With(highContrast: contrast));
            selected = router.Current.Setup;
            GUILayout.Label($"Audio volume: {selected.AudioVolume:0.00}");
            var volume = GUILayout.HorizontalSlider(selected.AudioVolume, 0f, 1f);
            if (!Mathf.Approximately(volume, selected.AudioVolume))
                router.UpdateSetupPreferences(selected.With(audioVolume: volume));
        }
    }

    public sealed class PanelState
    {
        public ScreenId ScreenId { get; }
        public string Mode { get; }
        public LabSetupPreferences Setup { get; }
        public string Workspace { get; }
        public string SelectedEntityId { get; }
        public string NativeRevision { get; }
        public string ModelIdentity { get; }
        public string ObservationValidity { get; }
        public string ObservationFreshness { get; }
        public string Maturity { get; }
        public string SaveState { get; }
        public bool Dirty { get; }
        public string HoldReason { get; }
        public IReadOnlyList<string> SupportedActions { get; }

        internal PanelState(ScreenId screenId, string mode, LabSetupPreferences setup, string workspace, string selectedEntityId,
            string nativeRevision, string modelIdentity, string observationValidity,
            string observationFreshness, string maturity, string saveState, bool dirty,
            string holdReason, string[] supportedActions)
        {
            ScreenId = screenId;
            Mode = mode;
            Setup = setup;
            Workspace = workspace;
            SelectedEntityId = selectedEntityId;
            NativeRevision = nativeRevision;
            ModelIdentity = modelIdentity;
            ObservationValidity = observationValidity;
            ObservationFreshness = observationFreshness;
            Maturity = maturity;
            SaveState = saveState;
            Dirty = dirty;
            HoldReason = holdReason;
            SupportedActions = new ReadOnlyCollection<string>(supportedActions);
        }
    }

    public sealed class ScreenRouter : MonoBehaviour
    {
        [SerializeField] private CoreDriver driver;
        public PanelState Current { get; private set; }
        public event Action<PanelState> Changed;
        private ScreenId screen = ScreenId.Lab;
        private ScreenId beforeHold = ScreenId.Lab;
        private ScreenId beforeSettings = ScreenId.Lab;
        private ScreenId beforeInterruptedSettings = ScreenId.Lab;
        private string selectedEntityId = "source";
        private string workspace = "Student";
        private string pendingMode = "Desktop";
        private string savedRevision;
        private string savedBranchId;
        private string saveState = "Unsaved";
        private ulong observedRestorationEpoch;
        private bool awaitingRestoredNativeSnapshot;
        private bool attached;

        public void Configure(CoreDriver core)
        {
            if (!ReferenceEquals(driver, core))
            {
                // Unity's destroyed MonoBehaviour compares equal to null
                // while its managed reference still belongs to an old session.
                var hadAuthority = !ReferenceEquals(driver, null);
                Detach();
                driver = core;
                // OnEnable/Configure may attach between a restore event
                // and publication. The previous session's recoveryReady
                // value must not authorize Continue in this interval.
                awaitingRestoredNativeSnapshot = core != null &&
                    core.NativePublishedRestorationEpoch != core.NativeRestorationEpoch;
                observedRestorationEpoch = core != null
                    ? core.NativeRestorationEpoch : 0UL;
                if (hadAuthority)
                {
                    // A replaced CoreDriver is a new authority even if its
                    // branch ID and event sequence happen to be identical.
                    // Never inherit the old saved label, vessel selection,
                    // Settings history, Review or time-hold presentation.
                    savedRevision = null;
                    savedBranchId = null;
                    saveState = "Unsaved";
                    selectedEntityId = "source";
                    beforeSettings = ScreenId.Lab;
                    beforeInterruptedSettings = ScreenId.Lab;
                    beforeHold = core != null && core.IsNativePaused &&
                        !core.SourceNeedsPreparation ? ScreenId.Pause : ScreenId.Lab;
                    screen = core != null && core.IsTimeHeld
                        ? ScreenId.Hold : core != null && core.IsNativePaused &&
                          !core.SourceNeedsPreparation ? ScreenId.Pause : ScreenId.Lab;
                }
                else if (core != null && screen == ScreenId.Lab)
                {
                    // Initial session binding must also respect Native
                    // pause/hold state, rather than assuming a running Lab.
                    if (core.IsTimeHeld)
                    {
                        beforeHold = core.IsNativePaused && !core.SourceNeedsPreparation
                            ? ScreenId.Pause : ScreenId.Lab;
                        screen = ScreenId.Hold;
                    }
                    else if (core.IsNativePaused && !core.SourceNeedsPreparation)
                        screen = ScreenId.Pause;
                }
            }
            Attach();
            Refresh();
        }

        public void Show(ScreenId id, string entityId = null)
        {
            if (driver != null && driver.IsTimeHeld && id != ScreenId.Hold &&
                id != ScreenId.Review && id != ScreenId.Settings) return;
            // Initial paused stock preparation is a controlled setup workspace,
            // NOT permission to simulate or transfer material. Real subsequent
            // Native Pause/SessionLoad still requires explicit Continue.
            if (driver != null && driver.IsNativePaused &&
                !driver.SourceNeedsPreparation && id == ScreenId.Lab)
            {
                if (screen == ScreenId.Lab)
                {
                    screen = ScreenId.Pause;
                    Refresh();
                }
                return;
            }
            if (!string.IsNullOrEmpty(entityId))
            {
                if (driver?.CurrentLabView == null ||
                    !driver.CurrentLabView.Vessels.Any(vessel => vessel.Id == entityId)) return;
                selectedEntityId = entityId;
            }
            if (id == ScreenId.Settings && screen != ScreenId.Settings) beforeSettings = screen;
            screen = id;
            Refresh();
        }

        public bool ReturnFromSettings()
        {
            if (screen != ScreenId.Settings) return false;
            var destination = beforeSettings;
            // Settings can remain open after the asynchronous Native Hold
            // resolves. Never restore a stale Hold just because it was the
            // screen underneath Settings when it opened.
            if (destination == ScreenId.Hold && driver != null && !driver.IsTimeHeld)
            {
                destination = beforeHold == ScreenId.Settings
                    ? beforeInterruptedSettings : beforeHold;
                // A recovered Hold must not revive obsolete Hold/Settings,
                // and a still-paused Native session must remain in Pause.
                if (destination == ScreenId.Settings || destination == ScreenId.Hold)
                    destination = driver.IsNativePaused && !driver.SourceNeedsPreparation ?
                        ScreenId.Pause : ScreenId.Lab;
            }
            // Settings opened over a saved Native Pause, then input recovery
            // completed while Settings was visible. Never restore the stale
            // Pause after an accepted Native Continue; setup may remain
            // intentionally paused only when it has a real stock chooser.
            if (destination == ScreenId.Pause && driver != null &&
                (!driver.IsNativePaused || driver.SourceNeedsPreparation))
                destination = ScreenId.Lab;
            // A late Native Pause while Settings is visible must not
            // restore a live Lab solely from previously captured UI history.
            if (destination == ScreenId.Lab && driver != null &&
                driver.IsNativePaused && !driver.SourceNeedsPreparation)
                destination = ScreenId.Pause;
            Show(destination);
            return screen == destination;
        }

        public void UpdateSetupPreferences(LabSetupPreferences preferences)
        {
            SetupPreferencesSelection.Select(preferences);
            Refresh();
        }

        public void SetWorkspace(string value)
        {
            if (value != "Student" && value != "Instructor" && value != "Researcher" && value != "Expert")
                throw new ArgumentException("Unsupported workspace.", nameof(value));
            workspace = value;
            Refresh();
        }

        public void SetBootMode(string value)
        {
            if (driver != null || (value != "Desktop" && value != "VR"))
                throw new ArgumentException("Unsupported boot mode.", nameof(value));
            pendingMode = value;
            Refresh();
        }

        public void MarkSaved()
        {
            savedRevision = driver?.CurrentLabView?.EventSequence;
            savedBranchId = driver?.CurrentNativeBranchId;
            saveState = "Saved";
            Refresh();
        }

        public void MarkSaveFailed()
        {
            saveState = "Failed";
            Refresh();
        }

        private void OnEnable()
        {
            SetupPreferencesSelection.Changed += OnSetupChanged;
            Attach();
            Refresh();
        }
        private void OnDisable()
        {
            SetupPreferencesSelection.Changed -= OnSetupChanged;
            Detach();
        }
        private void OnDestroy() => Detach();
        private void OnSetupChanged(LabSetupPreferences _) => Refresh();

        private void Attach()
        {
            if (attached || driver == null) return;
            driver.LabViewChanged += OnLabViewChanged;
            driver.HoldChanged += OnHoldChanged;
            driver.NativeSessionRestored += OnNativeSessionRestored;
            attached = true;
        }

        private void Detach()
        {
            if (!attached || driver == null) return;
            driver.LabViewChanged -= OnLabViewChanged;
            driver.HoldChanged -= OnHoldChanged;
            driver.NativeSessionRestored -= OnNativeSessionRestored;
            attached = false;
        }

        private void OnLabViewChanged(LabViewModel _)
        {
            // In CoreDriver.PublishSnapshot the Native view is populated
            // before LabViewChanged. This is the first authoritative point
            // where its recoveryReady flag may admit a Continue action.
            if (driver != null &&
                observedRestorationEpoch != driver.NativeRestorationEpoch)
                OnNativeSessionRestored();
            awaitingRestoredNativeSnapshot = false;
            Refresh();
        }

        private void OnNativeSessionRestored()
        {
            // Record the epoch before Refresh to avoid recursion and to
            // recognize a missed signal on later OnEnable/Configure.
            if (driver != null)
                observedRestorationEpoch = driver.NativeRestorationEpoch;
            // CoreDriver fires this after an accepted Native checkpoint
            // restart or CoreSession replacement, but BEFORE publishing
            // the replacement snapshot. The same MonoBehaviour is not the
            // same session authority. Never reuse any old saved label,
            // inspected vessel, or Settings/Review/Back history.
            savedRevision = null;
            savedBranchId = null;
            saveState = "Unsaved";
            selectedEntityId = "source";
            beforeSettings = ScreenId.Lab;
            beforeInterruptedSettings = ScreenId.Lab;
            beforeHold = ScreenId.Pause;
            // A disabled panel may miss both events. If the replacement
            // snapshot was published while this component was detached,
            // the local publication epoch allows current recovery actions
            // without waiting indefinitely for a second LabViewChanged.
            awaitingRestoredNativeSnapshot = driver == null ||
                driver.NativePublishedRestorationEpoch != driver.NativeRestorationEpoch;
            // SessionLoad/CheckpointRestart must remain held until Native
            // explicitly publishes its recovery state. The caller can
            // MarkSaved only after a separately validated save import.
            screen = ScreenId.Hold;
            Refresh();
        }

        private void OnHoldChanged(bool held)
        {
            if (held)
            {
                if (screen != ScreenId.Hold)
                {
                    if (screen == ScreenId.Settings)
                        beforeInterruptedSettings = beforeSettings;
                    beforeHold = screen;
                }
                screen = ScreenId.Hold;
            }
            else if (screen == ScreenId.Hold)
            {
                // The Native snapshot has already been published at this
                // point. A saved PAUSED checkpoint may have genuinely resumed
                // running: the old Pause screen is then stale and unsafe.
                // ResumeSetup deliberately stays paused, but only to let the
                // learner prepare the as-yet-unprepared Native stock.
                if (driver != null && driver.SourceNeedsPreparation)
                    screen = ScreenId.Lab;
                else if (driver != null && driver.IsNativePaused)
                    screen = ScreenId.Pause;
                else
                    screen = beforeHold == ScreenId.Pause ?
                        ScreenId.Lab : beforeHold;
            }
            Refresh();
        }

        private void Refresh()
        {
            if (driver != null &&
                observedRestorationEpoch != driver.NativeRestorationEpoch)
            {
                // When OnDisable detached event listeners, OnEnable still
                // must invalidate stale Saved/Settings/Inspect after a
                // restoration completed while this panel was inactive.
                OnNativeSessionRestored();
                return;
            }
            if (awaitingRestoredNativeSnapshot && driver != null &&
                driver.NativePublishedRestorationEpoch == driver.NativeRestorationEpoch)
            {
                // This router may have received NativeSessionRestored and
                // detached before LabViewChanged. The authoritative Native
                // snapshot was read while inactive: do not permanently
                // suppress an otherwise approved recovery action.
                awaitingRestoredNativeSnapshot = false;
            }
            // A focus/system adapter can pause or hold the Native authority
            // without a UI PanelAction. LabViewChanged and reactivation must
            // reconcile this truth before publishing live Lab actions.
            if (driver != null && screen == ScreenId.Lab)
            {
                if (driver.IsTimeHeld)
                {
                    beforeHold = driver.IsNativePaused && !driver.SourceNeedsPreparation
                        ? ScreenId.Pause : ScreenId.Lab;
                    screen = ScreenId.Hold;
                }
                else if (driver.IsNativePaused && !driver.SourceNeedsPreparation)
                    screen = ScreenId.Pause;
            }
            var view = driver?.CurrentLabView;
            var observation = view?.Observations.FirstOrDefault(item =>
                item.VesselId == selectedEntityId && item.ObservableId == "pH");
            var nativeRevision = view?.EventSequence ?? string.Empty;
            var nativeBranch = driver?.CurrentNativeBranchId;
            var dirty = savedRevision == null || savedRevision != nativeRevision ||
                        savedBranchId != nativeBranch;
            // Never inherit the outgoing session's cached recovery permission
            // between NativeSessionRestored and the replacement snapshot.
            var confirmedRecoveryReady = driver != null &&
                !awaitingRestoredNativeSnapshot && driver.NativeRecoveryReady;
            var actions = screen switch
            {
                ScreenId.Home => new[] { "EnterDesktop", "EnterVR" },
                ScreenId.Setup => new[] { "Home", "ConfirmSetup" },
                ScreenId.Hold => driver != null && driver.CurrentMode == "VR" ?
                    (confirmedRecoveryReady ?
                        new[] { "Save", "SaveCopy", "Load", "ChooseLocalSave", "LoadSelectedSave", "RestorePortable", "ChoosePortableArchive", "Review", "Settings", "ParkTool", "Recenter", "SnapLeft", "SnapRight", "Continue" } :
                        new[] { "Save", "SaveCopy", "Load", "ChooseLocalSave", "LoadSelectedSave", "RestorePortable", "ChoosePortableArchive", "Review", "Settings", "ParkTool", "Recenter", "SnapLeft", "SnapRight" }) :
                    (confirmedRecoveryReady ?
                        new[] { "Save", "SaveCopy", "Load", "ChooseLocalSave", "LoadSelectedSave", "RestorePortable", "ChoosePortableArchive", "Review", "Settings", "Continue" } :
                        new[] { "Save", "SaveCopy", "Load", "ChooseLocalSave", "LoadSelectedSave", "RestorePortable", "ChoosePortableArchive", "Review", "Settings" }),
                ScreenId.Lab when driver != null && driver.SourceNeedsPreparation =>
                    new[] { "PrepareResearchStock", "PrepareReserveStock", "PrepareRinseWater",
                        "SelectResearchLesson", "RecordResearchPrediction", "Learn", "Settings" },
                ScreenId.Lab => driver != null && driver.CurrentMode == "VR" ?
                    new[] { "Inspect", "Measure", "Pause", "Review", "Notebook", "Save", "ReleaseTool", "ParkTool",
                        "PrepareResearchStock", "PrepareRinseWater", "PipetteAspirate", "PipetteDispense",
                        "PipetteRinse", "BottlePour", "BottleRinse", "PrepareReserveStock", "PipetteSwitchReserve", "PipetteSwitchPrimary", "BottleRefill", "FillBurette", "SelectResearchLesson", "RecordResearchPrediction", "Learn" } :
                    new[] { "Inspect", "Measure", "Pause", "Review", "Notebook", "Save",
                        "PrepareResearchStock", "PrepareRinseWater", "PipetteAspirate", "PipetteDispense",
                        "PipetteRinse", "BottlePour", "BottleRinse", "PrepareReserveStock", "PipetteSwitchReserve", "PipetteSwitchPrimary", "BottleRefill", "FillBurette", "SelectResearchLesson", "RecordResearchPrediction", "Learn" },
                ScreenId.Learn => new[] { "Lab", "Inspect", "Measure", "Save" },
                ScreenId.Inspect => new[] { "Lab", "Measure", "Pause", "Review", "Save", "RecordCurrentPH" },
                ScreenId.Measure => new[] { "Lab", "Inspect", "Pause", "Review", "Save", "RecordCurrentPH" },
                ScreenId.Pause => driver != null && driver.CurrentMode == "VR" ?
                    new[] { "Lab", "Save", "SaveCopy", "NewStudy", "Load", "ChooseLocalSave", "LoadSelectedSave", "RestorePortable", "ChoosePortableArchive", "ExportPortable", "Settings", "Review", "ParkTool", "Recenter", "SnapLeft", "SnapRight" } :
                    new[] { "Lab", "Save", "SaveCopy", "NewStudy", "Load", "ChooseLocalSave", "LoadSelectedSave", "RestorePortable", "ChoosePortableArchive", "ExportPortable", "Settings", "Review" },
                ScreenId.Review => driver != null && driver.IsTimeHeld ?
                    new[] { "Hold", "Settings", "Save", "ExportCsv", "RecordInstructorFeedback" } :
                    new[] { "Lab", "Settings", "Save", "Notebook", "ExportCsv",
                        "CompareSaved", "ChooseLocalSave", "PreviewPortableArchive", "ChoosePortableArchive", "NextInstructorAnnotation", "RecordInstructorFeedback" },
                ScreenId.Notebook => new[] { "Lab", "AddNotebookNote", "Save", "Review",
                    "ExportPortable", "RecordResearchExplanation" },
                ScreenId.Compare => new[] { "Review", "Lab", "ExportComparisonCsv" },
                ScreenId.Settings => new[] { "Back", "Save", "SelectWorkspace" },
                _ => Array.Empty<string>()
            };
            Current = new PanelState(screen, driver?.CurrentMode ?? pendingMode, SetupPreferencesSelection.Current, workspace,
                selectedEntityId, nativeRevision, observation?.ModelId ?? string.Empty,
                observation?.Support ?? "Unknown", observation?.Freshness ?? "Absent",
                observation?.Maturity ?? string.Empty, saveState, dirty,
                driver?.HoldReason ?? string.Empty, actions);
            Changed?.Invoke(Current);
        }
    }
}
