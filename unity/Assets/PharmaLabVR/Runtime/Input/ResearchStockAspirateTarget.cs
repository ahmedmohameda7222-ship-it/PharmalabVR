using PharmaLabVR.Tools;
using UnityEngine;

namespace PharmaLabVR.Input
{
    // A physical source-bottle mouth, never a liquid or scientific authority.
    // Requires actual Native-driven liquid immersion and stationary bottle pose.
    public sealed class ResearchStockAspirateTarget : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] private float mouthRadiusMetres = 0.025f;
        private ReceiverMotionTracker bottleMotion = new ReceiverMotionTracker();

        public string NativeInventoryId => "source";

        private void OnEnable() => bottleMotion = new ReceiverMotionTracker();

        private void Update()
        {
            // Observe even when not requested. Kinematic XR bodies often report
            // zero linearVelocity while a controller is moving the stock.
            bottleMotion.Observe(Time.frameCount, Time.realtimeSinceStartupAsDouble,
                transform.position.x, transform.position.y, transform.position.z);
        }

        public bool IsImmersed(Transform pipette)
        {
            if (!isActiveAndEnabled || pipette == null || !pipette.gameObject.activeInHierarchy)
                return false;
            var tip = pipette.Find("OutletExit");
            var lip = transform.Find("OutletExit");
            var liquid = transform.Find("NativeBottleLiquid");
            var display = GetComponent<NativeMeteredInstrumentVisual>();
            var filter = liquid != null ? liquid.GetComponent<MeshFilter>() : null;
            if (tip == null || lip == null || liquid == null || filter == null ||
                filter.sharedMesh == null || display == null ||
                display.DisplayedVolumeM3 <= 0.0 || !liquid.gameObject.activeInHierarchy)
                return false;

            // Read the actual Native-snapshot-driven fill mesh, not a guessed
            // constant 25 mL level. No fluid passes unless Native later accepts.
            var up = transform.up;
            var top = liquid.TransformPoint(new Vector3(0f, filter.sharedMesh.bounds.max.y, 0f));
            var bottom = liquid.TransformPoint(new Vector3(0f, filter.sharedMesh.bounds.min.y, 0f));
            var offset = tip.position - lip.position;
            var depth = -Vector3.Dot(offset, up);
            var lateral = offset + up * depth;
            var speed = bottleMotion.Observe(Time.frameCount, Time.realtimeSinceStartupAsDouble,
                transform.position.x, transform.position.y, transform.position.z);
            return ResearchSourceImmersionAdmission.MayAspirate(
                Vector3.Dot(up, Vector3.up),
                Vector3.Dot(tip.TransformDirection(Vector3.down), -up),
                depth,
                Vector3.Dot(lip.position - top, up),
                Vector3.Dot(lip.position - bottom, up),
                lateral.sqrMagnitude,
                mouthRadiusMetres,
                speed);
        }
    }
}
