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

    public sealed class PanelState
    {
        public ScreenId ScreenId { get; }
        public string Mode { get; }
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

        internal PanelState(ScreenId screenId, string mode, string workspace, string selectedEntityId,
            string nativeRevision, string modelIdentity, string observationValidity,
            string observationFreshness, string maturity, string saveState, bool dirty,
            string holdReason, string[] supportedActions)
        {
            ScreenId = screenId;
            Mode = mode;
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
            screen = id;
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

        private void OnEnable() { Attach(); Refresh(); }
        private void OnDisable() => Detach();
        private void OnDestroy() => Detach();

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
                ScreenId.Hold => driver != null && driver.NativeRecoveryReady ?
                    new[] { "Save", "Review", "Settings", "Continue" } :
                    new[] { "Save", "Review", "Settings" },
                ScreenId.Lab => new[] { "Inspect", "Measure", "Pause", "Review", "Save" },
                ScreenId.Inspect => new[] { "Lab", "Measure", "Pause", "Review", "Save" },
                ScreenId.Measure => new[] { "Lab", "Inspect", "Pause", "Review", "Save" },
                ScreenId.Pause => new[] { "Lab", "Save", "Settings", "Review" },
                ScreenId.Review => new[] { "Lab", "Settings", "Save" },
                ScreenId.Settings => new[] { "Lab", "Save" },
                _ => Array.Empty<string>()
            };
            Current = new PanelState(screen, driver?.CurrentMode ?? pendingMode, workspace,
                selectedEntityId, nativeRevision, observation?.ModelId ?? string.Empty,
                observation?.Support ?? "Unknown", observation?.Freshness ?? "Absent",
                observation?.Maturity ?? string.Empty, saveState, dirty,
                driver?.HoldReason ?? string.Empty, actions);
            Changed?.Invoke(Current);
        }
    }
}
