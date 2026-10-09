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
        private static readonly Rect DesktopHudRect = new(12f, 12f, 350f, 328f);

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
