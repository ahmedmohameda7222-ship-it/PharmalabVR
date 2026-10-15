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
        private string selectedEntityId = "source";
        private string workspace = "Student";
        private string pendingMode = "Desktop";
        private string savedRevision;
        private string saveState = "Unsaved";
        private bool attached;

        public void Configure(CoreDriver core)
        {
            if (driver != core) Detach();
            driver = core;
            Attach();
            Refresh();
        }

        public void Show(ScreenId id, string entityId = null)
        {
            if (driver != null && driver.IsTimeHeld && id != ScreenId.Hold &&
                id != ScreenId.Review && id != ScreenId.Settings) return;
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
            Show(beforeSettings);
            return screen == beforeSettings;
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
            attached = true;
        }

        private void Detach()
        {
            if (!attached || driver == null) return;
            driver.LabViewChanged -= OnLabViewChanged;
            driver.HoldChanged -= OnHoldChanged;
            attached = false;
        }

        private void OnLabViewChanged(LabViewModel _) => Refresh();

        private void OnHoldChanged(bool held)
        {
            if (held)
            {
                if (screen != ScreenId.Hold) beforeHold = screen;
                screen = ScreenId.Hold;
            }
            else if (screen == ScreenId.Hold) screen = beforeHold;
            Refresh();
        }

        private void Refresh()
        {
            var view = driver?.CurrentLabView;
            var observation = view?.Observations.FirstOrDefault(item =>
                item.VesselId == selectedEntityId && item.ObservableId == "pH");
            var nativeRevision = view?.EventSequence ?? string.Empty;
            var dirty = savedRevision == null || savedRevision != nativeRevision;
            var actions = screen switch
            {
                ScreenId.Home => new[] { "EnterDesktop", "EnterVR" },
                ScreenId.Setup => new[] { "Home", "ConfirmSetup" },
                ScreenId.Hold => driver != null && driver.CurrentMode == "VR" ?
                    (driver.NativeRecoveryReady ?
                        new[] { "Save", "Load", "Review", "Settings", "ParkTool", "Recenter", "SnapLeft", "SnapRight", "Continue" } :
                        new[] { "Save", "Load", "Review", "Settings", "ParkTool", "Recenter", "SnapLeft", "SnapRight" }) :
                    (driver != null && driver.NativeRecoveryReady ?
                        new[] { "Save", "Review", "Settings", "Continue" } :
                        new[] { "Save", "Review", "Settings" }),
                ScreenId.Lab => driver != null && driver.CurrentMode == "VR" ?
                    new[] { "Inspect", "Measure", "Pause", "Review", "Save", "ReleaseTool", "ParkTool" } :
                    new[] { "Inspect", "Measure", "Pause", "Review", "Save" },
                ScreenId.Inspect => new[] { "Lab", "Measure", "Pause", "Review", "Save" },
                ScreenId.Measure => new[] { "Lab", "Inspect", "Pause", "Review", "Save" },
                ScreenId.Pause => driver != null && driver.CurrentMode == "VR" ?
                    new[] { "Lab", "Save", "Load", "Settings", "Review", "ParkTool", "Recenter", "SnapLeft", "SnapRight" } :
                    new[] { "Lab", "Save", "Settings", "Review" },
                ScreenId.Review => new[] { "Lab", "Settings", "Save" },
                ScreenId.Settings => new[] { "Back", "Save" },
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
