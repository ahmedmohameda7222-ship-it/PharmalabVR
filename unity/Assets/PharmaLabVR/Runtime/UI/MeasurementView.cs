using TMPro;
using UnityEngine;

namespace PharmaLabVR.UI
{
    public enum MeasurementAvailability { Current, Pending, Stale, Unsupported, Failed }

    public sealed class MeasurementView : MonoBehaviour
    {
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private TMP_Text statusLabel;
        public MeasurementAvailability Availability { get; private set; }
        public string DisplayValue { get; private set; } = "--";
        public string Explanation { get; private set; } = string.Empty;

        public void Present(MeasurementAvailability availability, string value, string explanation)
        {
            Availability = availability;
            DisplayValue = availability == MeasurementAvailability.Current ? value : "--";
            Explanation = explanation ?? string.Empty;
            if (valueLabel != null) valueLabel.text = DisplayValue;
            if (statusLabel != null) statusLabel.text = Explanation;
        }
    }
}
