using System;
using System.IO;
using PharmaLabVR.Core;
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
        private readonly SaveService saves = new();
        private static readonly Rect DesktopHudRect = new(12f, 12f, 370f, 620f);
        private int selectedConcentrationIndex = 2;

        public void Configure(CoreDriver coreDriver, string sessionName = null)
        {
            driver = coreDriver;
            if (!string.IsNullOrEmpty(sessionName)) saveName = sessionName;
        }

        public static bool PointerOverDesktopHud(Vector2 screenPosition)
        {
            return DesktopHudRect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        private void OnGUI()
        {
            if (driver == null || driver.CurrentMode != "Desktop") return;
            GUILayout.BeginArea(DesktopHudRect, GUI.skin.box);
            GUILayout.Label("PharmaLabVR · Research Lab");
            if (!string.IsNullOrEmpty(driver.StartupError))
            {
                GUILayout.Label("Lab unavailable: " + driver.StartupError);
                GUILayout.EndArea();
                return;
            }
            if (driver.SourceNeedsPreparation)
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
            GUILayout.Label(measurement == null ? "pH: pending" :
                $"pH: {measurement.DisplayValue} ({measurement.Availability})");
            if (measurement != null) GUILayout.Label(measurement.Explanation);
            if (driver.IsTimeHeld)
            {
                GUILayout.Label("Paused: " + driver.HoldReason + ". Close the valve and restore tracking.");
                if (GUILayout.Button("Continue", GUILayout.Height(34f))) ContinueHeldSession();
            }
            if (GUILayout.Button("Save session", GUILayout.Height(30f))) Save();
            var savedPath = Path.Combine(Application.persistentDataPath, "sessions", saveName + ".plv.json");
            if (File.Exists(savedPath) && GUILayout.Button("Load saved session", GUILayout.Height(30f)))
                ContinueFrom(savedPath);
            if (GUILayout.Button("Create checkpoint", GUILayout.Height(30f))) CreateCheckpoint();
            if (GUILayout.Button("Restart checkpoint", GUILayout.Height(30f))) RestartCheckpoint();
            if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
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
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }

        public bool ContinueFrom(string path)
        {
            CoreSession imported = null;
            try
            {
                if (driver == null) throw new InvalidOperationException("Core driver is not assigned.");
                imported = saves.Load(path, driver.ScientificDatabasePath);
                driver.ReplaceSession(imported);
                imported = null;
                LastError = null;
                return true;
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
