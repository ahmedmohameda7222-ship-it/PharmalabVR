using System;
using System.IO;
using System.Linq;
using PharmaLabVR.Core;
using PharmaLabVR.UI;
using UnityEngine;

namespace PharmaLabVR.Session
{
    public sealed class SessionController : MonoBehaviour
    {
        [SerializeField] private CoreDriver driver;
        [SerializeField] private string saveName = "autosave";
        [SerializeField] private string checkpointId = "student-checkpoint";
        public string LastDurableSavePath { get; private set; }
        public string LastError { get; private set; }
        public RecordedSessionReview RecordedReview { get; private set; }
        private readonly SaveService saves = new();
        public static Rect DesktopHudBounds => new Rect(12f, 12f,
            Mathf.Max(1f, Mathf.Min(370f, Screen.width - 24f)),
            Mathf.Max(1f, Mathf.Min(620f, Screen.height - 24f)));
        private int selectedConcentrationIndex = 2;
        private Vector2 hudScrollPosition;
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

        public static bool PointerOverDesktopHud(Vector2 screenPosition)
        {
            return DesktopHudBounds.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        private void OnGUI()
        {
            if (driver == null || driver.CurrentMode != "Desktop") return;
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
                if (GUILayout.Button("Open recorded review", GUILayout.Height(44f))) panels?.Request(PanelAction.Review);
                if (GUILayout.Button("Settings", GUILayout.Height(44f))) panels?.Request(PanelAction.Settings);
                if (GUILayout.Button("Restart checkpoint", GUILayout.Height(44f))) RestartCheckpoint();
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
                    foreach (var observation in RecordedReview.Observations)
                        GUILayout.Label($"Recorded {observation.vesselId} pH: {observation.DisplayValue} " +
                            $"({observation.freshness}, rev {observation.asOfMaterialRevision})");
                }
                if (GUILayout.Button("Return to lab", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Lab);
                if (GUILayout.Button("Settings", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Settings);
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
            if (File.Exists(savedPath) && GUILayout.Button("Load saved session", GUILayout.Height(30f)))
                ContinueFrom(savedPath);
            if (GUILayout.Button("Create checkpoint", GUILayout.Height(30f))) CreateCheckpoint();
            if (GUILayout.Button("Restart checkpoint", GUILayout.Height(30f))) RestartCheckpoint();
            if (router?.Current?.SupportedActions.Contains("Review") == true &&
                GUILayout.Button("Open recorded review", GUILayout.Height(44f)))
                panels?.Request(PanelAction.Review);
            if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
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

        public void CloseReview() => RecordedReview = null;

        public bool ContinueFrom(string path)
        {
            CoreSession imported = null;
            RecordedReview = null;
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
