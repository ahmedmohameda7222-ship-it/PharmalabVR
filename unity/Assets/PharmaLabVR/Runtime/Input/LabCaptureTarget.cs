using System.Collections.Generic;
using UnityEngine;

namespace PharmaLabVR.Input
{
    public sealed class LabCaptureTarget : MonoBehaviour
    {
        [SerializeField] private string inventoryId = "receiver";
        [SerializeField, Min(0.001f)] private float captureRadiusMetres = 0.08f;
        [SerializeField] private float apertureHeightMetres;
        [SerializeField, Min(0.001f)] private float maxFallMetres = 0.15f;
        private readonly ReceiverMotionTracker trackedMotion = new ReceiverMotionTracker();
        // Registered only while active, so two overlapping receivers cannot
        // silently double-credit the same physical parcel or win by order.
        private static readonly HashSet<LabCaptureTarget> ActiveTargets =
            new HashSet<LabCaptureTarget>();

        private void OnEnable() => ActiveTargets.Add(this);
        private void OnDisable() => ActiveTargets.Remove(this);
        private void OnDestroy() => ActiveTargets.Remove(this);

        public static LabCaptureEstimate[] EstimateExclusive(Transform tool, LabCaptureTarget sceneAnchor)
        {
            if (tool == null || sceneAnchor == null || !sceneAnchor.isActiveAndEnabled)
                return System.Array.Empty<LabCaptureEstimate>();
            // Source and receiver may be in differently loaded scenes, but
            // only targets in the configured anchor's lab scene are eligible.
            var scene = sceneAnchor.gameObject.scene;
            var inventoryIds = new List<string>();
            LabCaptureEstimate soleCapture = default;
            foreach (var target in ActiveTargets)
            {
                if (target == null || !target.isActiveAndEnabled || target.gameObject.scene != scene)
                    continue;
                var estimates = target.Estimate(tool);
                if (estimates.Length == 0) continue;
                // This research model requires one unambiguous full capture
                // target; any additional hit is a spill/uncaptured input.
                for (var i = 0; i < estimates.Length; ++i)
                {
                    inventoryIds.Add(estimates[i].destinationInventoryId);
                    soleCapture = estimates[i];
                }
                if (inventoryIds.Count > 1)
                    return System.Array.Empty<LabCaptureEstimate>();
            }
            var resolved = CaptureTargetSelection.ResolveUnique(inventoryIds);
            return resolved == null ? System.Array.Empty<LabCaptureEstimate>() :
                new[] { soleCapture };
        }

        public void Configure(string destinationInventoryId, float radiusMetres, float apertureHeight = 0f)
        {
            inventoryId = destinationInventoryId;
            captureRadiusMetres = Mathf.Max(0.001f, radiusMetres);
            apertureHeightMetres = Mathf.Max(0f, apertureHeight);
        }

        public LabCaptureEstimate[] Estimate(Transform tool)
        {
            // Both physical velocity and actual tracked transform motion
            // must satisfy the researched static-aperture speed bound.
            // XRI-held/kinematic bodies often report zero linearVelocity.
            var point = transform.position;
            var trackedSpeed = trackedMotion.Observe(Time.frameCount,
                Time.realtimeSinceStartupAsDouble, point.x, point.y, point.z);
            if (trackedSpeed > CaptureAdmission.MaximumReceiverSpeedMetresPerSecond)
                return System.Array.Empty<LabCaptureEstimate>();
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
