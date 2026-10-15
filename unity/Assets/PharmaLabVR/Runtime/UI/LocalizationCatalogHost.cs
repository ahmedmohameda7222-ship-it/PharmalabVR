using System;
using UnityEngine;

namespace PharmaLabVR.UI
{
    // Serialized pinned catalog references are owned by the generated Boot and
    // Lab roots; no downloaded or injected translations can affect Native state.
    public sealed class LocalizationCatalogHost : MonoBehaviour
    {
        [SerializeField] private TextAsset english;
        [SerializeField] private TextAsset arabic;
        private readonly LocalizationService catalog = new LocalizationService();
        private static LocalizationCatalogHost active;
        public static event Action CatalogChanged;
        public static LocalizationService ActiveCatalog => active != null ? active.catalog : null;

        public void Configure(TextAsset en, TextAsset ar)
        {
            if (en == null || ar == null)
                throw new ArgumentException("Both pinned locale tables must be present.");
            english = en;
            arabic = ar;
            if (isActiveAndEnabled) Reload(LanguageSelection.Current);
        }

        private void OnEnable()
        {
            active = this;
            LanguageSelection.Changed += OnLocaleChanged;
            if (english != null && arabic != null)
                Reload(LanguageSelection.Current);
        }

        private void OnDisable()
        {
            LanguageSelection.Changed -= OnLocaleChanged;
            if (active == this) active = null;
        }

        private void OnLocaleChanged(string locale) => Reload(locale);

        private void Reload(string locale)
        {
            var data = locale == "ar" ? arabic : english;
            if (data == null)
            {
                Debug.LogError("Missing pinned localization TextAsset for " + locale);
                return;
            }
            try
            {
                catalog.Load(locale, data);
                CatalogChanged?.Invoke();
            }
            catch (Exception error)
            {
                // Retain last good catalog without altering language preference
                // or interrupting the Native science session.
                Debug.LogError("Localization catalog was rejected: " + error.Message);
            }
        }

        public static string Get(string key, string englishFallback)
        {
            if (active == null) return englishFallback;
            var translated = active.catalog.Get(key);
            return translated == "[" + key + "]" ? englishFallback : translated;
        }
    }
}
