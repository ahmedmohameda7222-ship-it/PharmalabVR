using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PharmaLabVR.Tools
{
    // Physical ownership only. Native PlaceTool and material inventories are
    // registered independently by CoreDriver; docking never changes chemistry.
    public sealed class ToolMount : MonoBehaviour
    {
        [SerializeField] private Transform dockAnchor;
        [SerializeField] private Rigidbody body;
        [SerializeField] private XRGrabInteractable grabbable;
        public bool IsMounted { get; private set; }
        public Transform DockAnchor => dockAnchor;
        private bool previousKinematic;

        public void Configure(Transform anchor, Rigidbody rigidbody, XRGrabInteractable grab)
        {
            if (anchor == null || rigidbody == null || grab == null)
                throw new ArgumentNullException(nameof(anchor), "The dock requires a pose, rigidbody and XR grip.");
            dockAnchor = anchor;
            body = rigidbody;
            grabbable = grab;
        }

        public bool TryMount()
        {
            if (IsMounted || dockAnchor == null || body == null || grabbable == null ||
                grabbable.isSelected) return false;
            // Do not teleport released apparatus across the laboratory.
            // Only a deliberately returned tool, unselected by either hand,
            // may be snapped the final few centimetres into its rack.
            var distance = Vector3.Distance(transform.position, dockAnchor.position);
            var angle = Quaternion.Angle(transform.rotation, dockAnchor.rotation);
            if (!ToolMountAdmission.CanMount(distance, angle, grabbable.isSelected))
                return false;
            previousKinematic = body.isKinematic;
            body.isKinematic = true;
            transform.SetPositionAndRotation(dockAnchor.position, dockAnchor.rotation);
            grabbable.enabled = false;
            IsMounted = true;
            return true;
        }

        public bool ReleaseFromDock()
        {
            if (!IsMounted || body == null || grabbable == null) return false;
            grabbable.enabled = true;
            body.isKinematic = previousKinematic;
            IsMounted = false;
            return true;
        }
    }
}
