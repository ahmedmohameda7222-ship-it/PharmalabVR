using System;
using PharmaLabVR.Core;
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

        [SerializeField] private string nativeInventoryId = "source";
        public string NativeInventoryId => nativeInventoryId;
        public void ConfigureInventory(string value)
        {
            if (value != "source" && value != "reserve-stock")
                throw new ArgumentException("A supported physical stock inventory is required.", nameof(value));
            nativeInventoryId = value;
        }

        // Each actual bottle represents its serialized Native inventory.
        // Source selection is read fresh at both gesture edges; reserve
        // switches cannot reuse a source-bottle immersion gesture.
        public bool NativeSourceMatches(CoreSession session, PipetteActuator pipette)
        {
            if (session == null || !session.IsOpen || pipette == null)
                return false;
            try
            {
                var current = JsonUtility.FromJson<ToolBindings>(session.ReadSnapshot());
                if (current?.tools == null) return false;
                foreach (var tool in current.tools)
                    if (tool != null && tool.id == pipette.NativeToolId)
                        return ResearchPhysicalSourceBinding.Matches(
                            pipette.ConfiguredSourceInventoryId, NativeInventoryId,
                            tool.sourceInventoryId, true);
                // Before its first Native PlaceTool command the authored
                // pipette still declares a source, but no reserve switch exists.
                return ResearchPhysicalSourceBinding.Matches(
                    pipette.ConfiguredSourceInventoryId, NativeInventoryId, null, false);
            }
            catch (Exception)
            {
                // Missing/malformed scientific authority is never a physical permit.
                return false;
            }
        }

        [Serializable] private sealed class ToolBindings { public ToolBinding[] tools; }
        [Serializable] private sealed class ToolBinding
        {
            public string id;
            public string sourceInventoryId;
        }

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
