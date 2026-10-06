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
        public string LastDurableSavePath { get; private set; }
        public string LastError { get; private set; }
        private readonly SaveService saves = new();

        public void Configure(CoreDriver coreDriver) => driver = coreDriver;

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
                imported = saves.Load(path);
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
    }
}
