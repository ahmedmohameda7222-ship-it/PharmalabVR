using UnityEngine;

namespace PharmaLabVR.Input
{
    public sealed class LabCaptureTarget : MonoBehaviour
    {
        [SerializeField] private string inventoryId = "receiver";
        [SerializeField, Min(0.001f)] private float captureRadiusMetres = 0.08f;
        [SerializeField] private float apertureHeightMetres;
        [SerializeField] private Vector3 buretteOutletLocalMetres = new(0f, -0.02f, 0f);
        [SerializeField, Min(0.001f)] private float maxFallMetres = 0.15f;

        public void Configure(string destinationInventoryId, float radiusMetres, float apertureHeight = 0f)
        {
            inventoryId = destinationInventoryId;
            captureRadiusMetres = Mathf.Max(0.001f, radiusMetres);
            apertureHeightMetres = Mathf.Max(0f, apertureHeight);
        }

        public LabCaptureEstimate[] Estimate(Transform outlet)
        {
            if (outlet == null || string.IsNullOrWhiteSpace(inventoryId))
                return System.Array.Empty<LabCaptureEstimate>();
            var apertureUp = transform.up;
            if (Vector3.Dot(apertureUp, Vector3.up) < 0.9f ||
                Vector3.Dot(outlet.TransformDirection(Vector3.down), -apertureUp) < 0.85f)
                return System.Array.Empty<LabCaptureEstimate>();
            var apertureCenter = transform.position + apertureUp * apertureHeightMetres;
            var outletPosition = outlet.TransformPoint(buretteOutletLocalMetres);
            var offset = outletPosition - apertureCenter;
            var height = Vector3.Dot(offset, apertureUp);
            var lateral = offset - apertureUp * height;
            if (!float.IsFinite(height) || !float.IsFinite(lateral.sqrMagnitude) ||
                height <= 0f || height > maxFallMetres ||
                lateral.sqrMagnitude > captureRadiusMetres * captureRadiusMetres)
                return System.Array.Empty<LabCaptureEstimate>();
            return new[] { new LabCaptureEstimate { destinationInventoryId = inventoryId, fraction = 1f } };
        }
    }
}
