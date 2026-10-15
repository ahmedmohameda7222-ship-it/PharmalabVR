using System;
using UnityEngine;

namespace PharmaLabVR.UI
{
    // Locale is UI-only: never restore chemistry/mode, or write lab saves.
    public static class LanguagePreferencePersistence
    {
        private const string Key = "PharmaLabVR.UiLocale.v1";
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized) return;
            var restored = PlayerPrefs.GetString(Key, string.Empty);
            if (restored == "ar" || restored == "en")
                LanguageSelection.Select(restored);
            else if (!string.IsNullOrEmpty(restored))
                Debug.LogWarning("Unsupported stored UI locale ignored.");
            LanguageSelection.Changed += Persist;
            initialized = true;
        }

        private static void Persist(string locale)
        {
            try
            {
                PlayerPrefs.SetString(Key, locale);
                PlayerPrefs.Save();
            }
            catch (Exception error)
            {
                Debug.LogWarning("UI language preference storage unavailable: " + error.Message);
            }
        }
    }
}
