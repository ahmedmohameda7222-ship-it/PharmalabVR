using UnityEngine;

namespace PharmaLabVR.Input
{
    public sealed class LabCaptureTarget : MonoBehaviour
    {
        [SerializeField] private string inventoryId = "receiver";
        [SerializeField, Min(0.001f)] private float captureRadiusMetres = 0.08f;

        public void Configure(string destinationInventoryId, float radiusMetres)
        {
            inventoryId = destinationInventoryId;
            captureRadiusMetres = Mathf.Max(0.001f, radiusMetres);
        }

        public LabCaptureEstimate[] Estimate(Transform outlet)
        {
            if (outlet == null || string.IsNullOrWhiteSpace(inventoryId))
                return System.Array.Empty<LabCaptureEstimate>();
            var distance = Vector3.Distance(outlet.position, transform.position);
            if (!float.IsFinite(distance) || distance > captureRadiusMetres)
                return System.Array.Empty<LabCaptureEstimate>();
            return new[] { new LabCaptureEstimate { destinationInventoryId = inventoryId, fraction = 1f } };
        }
    }
}
