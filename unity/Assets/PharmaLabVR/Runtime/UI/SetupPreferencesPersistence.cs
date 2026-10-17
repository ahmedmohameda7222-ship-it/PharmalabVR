using System;
using UnityEngine;

namespace PharmaLabVR.UI
{
    // Local ergonomic preferences only. The event subscription deliberately
    // spans Boot -> Lab scene unload; no Native session, chemistry or Review.
    public static class SetupPreferencesPersistence
    {
        private const string PrefsKey = "PharmaLabVR.UserSetupPreferences.v1";
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized) return;
            var raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (SetupPreferencesCodec.TryDecode(raw, out var restored))
                SetupPreferencesSelection.Select(restored);
            else if (!string.IsNullOrEmpty(raw))
                Debug.LogWarning("Ignoring invalid stored PharmaLabVR ergonomic preferences.");
            SetupPreferencesSelection.Changed += Store;
            initialized = true;
        }

        private static void Store(LabSetupPreferences selected)
        {
            try
            {
                PlayerPrefs.SetString(PrefsKey, SetupPreferencesCodec.Encode(selected));
                PlayerPrefs.Save();
            }
            catch (Exception error)
            {
                // Failed preference storage cannot fabricate a lab state or
                // stop an accepted Native science interaction.
                Debug.LogWarning("User preference save was unavailable: " + error.Message);
            }
        }
    }
}
