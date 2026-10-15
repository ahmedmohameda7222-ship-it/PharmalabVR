using UnityEngine;

namespace PharmaLabVR.Input
{
    public sealed class LabCaptureTarget : MonoBehaviour
    {
        [SerializeField] private string inventoryId = "receiver";
        [SerializeField, Min(0.001f)] private float captureRadiusMetres = 0.08f;
        [SerializeField] private float apertureHeightMetres;
        [SerializeField, Min(0.001f)] private float maxFallMetres = 0.15f;

        public void Configure(string destinationInventoryId, float radiusMetres, float apertureHeight = 0f)
        {
            inventoryId = destinationInventoryId;
            captureRadiusMetres = Mathf.Max(0.001f, radiusMetres);
            apertureHeightMetres = Mathf.Max(0f, apertureHeight);
        }

        public LabCaptureEstimate[] Estimate(Transform tool)
        {
            // A dynamic receiver cannot be treated as a static cup.
            // Handheld/kinematic teleports without a measured velocity remain
            // an explicit deferred geometry-validation limitation.
            var body = GetComponentInParent<Rigidbody>();
            return Estimate(tool, body != null ? body.linearVelocity : Vector3.zero);
        }

        // Deterministic tracked-motion overload used in Unity regression tests.
        public LabCaptureEstimate[] Estimate(Transform tool, Vector3 receiverVelocityMetresPerSecond)
        {
            var outlet = tool != null ? tool.Find("OutletExit") : null;
            if (outlet == null || string.IsNullOrWhiteSpace(inventoryId))
                return System.Array.Empty<LabCaptureEstimate>();
            var apertureUp = transform.up;
            var apertureCenter = transform.position + apertureUp * apertureHeightMetres;
            var offset = outlet.position - apertureCenter;
            var height = Vector3.Dot(offset, apertureUp);
            var lateral = offset - apertureUp * height;
            var movingSpeed = receiverVelocityMetresPerSecond.magnitude;
            if (!CaptureAdmission.MayCapture(
                    Vector3.Dot(apertureUp, Vector3.up),
                    Vector3.Dot(outlet.TransformDirection(Vector3.down), -apertureUp),
                    height, lateral.sqrMagnitude, captureRadiusMetres,
                    maxFallMetres, movingSpeed))
                return System.Array.Empty<LabCaptureEstimate>();
            return new[] { new LabCaptureEstimate { destinationInventoryId = inventoryId, fraction = 1f } };
        }
    }
}
