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

        private void Awake()
        {
            if (label == null) label = GetComponent<RTLTextMeshPro>();
        }

        public void Bind(LocalizationService localization, string key)
        {
            if (localization == null) throw new System.ArgumentNullException(nameof(localization));
            if (label == null) label = GetComponent<RTLTextMeshPro>();
            var rtl = localization.IsRightToLeft;
            label.ForceFix = rtl;
            label.PreserveNumbers = true;
            label.alignment = rtl ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            label.font = rtl ? arabicFont : latinFont;
            label.text = localization.Get(key);
        }
    }
}
