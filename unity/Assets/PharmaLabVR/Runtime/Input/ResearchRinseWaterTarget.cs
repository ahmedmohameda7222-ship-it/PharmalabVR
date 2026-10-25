using PharmaLabVR.Tools;
using UnityEngine;

namespace PharmaLabVR.Input
{
    // A physical gesture admission only. Native RinseTool owns all material
    // consumption, waste, event sequences and command receipts.
    public sealed class ResearchRinseWaterTarget : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] private float openMouthRadiusMetres = 0.010f;
        private ReceiverMotionTracker motion = new ReceiverMotionTracker();

        public bool IsImmersed(Transform pipette)
        {
            if (!isActiveAndEnabled || pipette == null ||
                !pipette.gameObject.activeInHierarchy) return false;
            var tip = pipette.Find("OutletExit");
            var mouth = transform.Find("Opening");
            var fill = transform.Find("NativeRinseWaterLiquid");
            var visual = GetComponent<NativeMeteredInstrumentVisual>();
            var mesh = fill != null ? fill.GetComponent<MeshFilter>() : null;
            if (tip == null || mouth == null || fill == null || mesh == null ||
                mesh.sharedMesh == null || visual == null ||
                visual.DisplayedVolumeM3 <= 0.0 || !fill.gameObject.activeInHierarchy)
                return false;
            var up = transform.up;
            var offset = tip.position - mouth.position;
            var depth = -Vector3.Dot(offset, up);
            var lateral = offset + up * depth;
            var surface = fill.TransformPoint(
                new Vector3(0f, mesh.sharedMesh.bounds.max.y, 0f));
            var bottom = fill.TransformPoint(
                new Vector3(0f, mesh.sharedMesh.bounds.min.y, 0f));
            var speed = motion.Observe(Time.frameCount,
                Time.realtimeSinceStartupAsDouble,
                transform.position.x, transform.position.y, transform.position.z);
            // The visible cup rim is NOT the whole wetted region.
            // Always read the radial width of the actual Native-driven fill
            // mesh before admitting the physical cleaning gesture.
            var effectiveRadius = ResearchRinseApertureAdmission.EffectiveRadiusMeters(
                openMouthRadiusMetres,
                mesh.sharedMesh.bounds.extents.x, fill.lossyScale.x,
                mesh.sharedMesh.bounds.extents.z, fill.lossyScale.z);
            return ResearchSourceImmersionAdmission.MayAspirate(
                Vector3.Dot(up, Vector3.up),
                Vector3.Dot(tip.TransformDirection(Vector3.down), -up),
                depth, Vector3.Dot(mouth.position - surface, up),
                Vector3.Dot(mouth.position - bottom, up),
                lateral.sqrMagnitude, effectiveRadius, speed);
        }

        private void OnEnable() => motion = new ReceiverMotionTracker();
        private void Update() => motion.Observe(Time.frameCount,
            Time.realtimeSinceStartupAsDouble,
            transform.position.x, transform.position.y, transform.position.z);
    }
}
