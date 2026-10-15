using RTLTMPro;
using TMPro;
using UnityEngine;

namespace PharmaLabVR.UI
{
    [RequireComponent(typeof(RTLTextMeshPro))]
    public sealed class LocalizedLabel : MonoBehaviour
    {
        [SerializeField] private RTLTextMeshPro label;
        [SerializeField] private TMP_FontAsset latinFont;
        [SerializeField] private TMP_FontAsset arabicFont;
        [SerializeField] private string translationKey;

        private void OnEnable()
        {
            LocalizationCatalogHost.CatalogChanged += RebindCurrentCatalog;
            RebindCurrentCatalog();
        }

        private void OnDisable()
        {
            LocalizationCatalogHost.CatalogChanged -= RebindCurrentCatalog;
        }

        private void RebindCurrentCatalog()
        {
            var current = LocalizationCatalogHost.ActiveCatalog;
            if (current != null && !string.IsNullOrEmpty(translationKey))
                Bind(current, translationKey);
        }

        private void Awake()
        {
            if (label == null) label = GetComponent<RTLTextMeshPro>();
        }

        public void Bind(LocalizationService localization, string key)
        {
            if (localization == null) throw new System.ArgumentNullException(nameof(localization));
            if (label == null) label = GetComponent<RTLTextMeshPro>();
            translationKey = key;
            var rtl = localization.IsRightToLeft;
            label.ForceFix = rtl;
            label.PreserveNumbers = true;
            label.alignment = rtl ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            // Never clear a prefab's working TMP font when a locale-specific
            // SDF has not yet been assigned in the generated Unity assets.
            var selectedFont = rtl ? arabicFont : latinFont;
            if (selectedFont != null) label.font = selectedFont;
            label.text = localization.Get(key);
        }
    }
}
