using UnityEngine;

namespace PharmaLabVR.Input
{
    public static class LabGeometryAdapter
    {
        public static LabInputSample BuildSample(string toolId, ulong sequence, ulong nowNs, Transform labFrame, Transform tool, float actuator01, string profileHash, bool trackingValid)
        {
            var position = labFrame.InverseTransformPoint(tool.position);
            var rotation = Quaternion.Inverse(labFrame.rotation) * tool.rotation;
            if (!IsFinite(position) || !IsFinite(rotation) || Mathf.Abs(1f - Quaternion.Dot(rotation, rotation)) > 0.001f) trackingValid = false;
            return new LabInputSample { toolId = toolId, sampleSequence = sequence, captureMonotonicNs = nowNs, positionMetres = position, rotation = rotation.normalized, actuator01 = Mathf.Clamp01(actuator01), geometryProfileHash = profileHash, trackingValid = trackingValid };
        }

        private static bool IsFinite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        private static bool IsFinite(Quaternion value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z) && float.IsFinite(value.w);
    }
}
