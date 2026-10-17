using UnityEngine;
using Unity.XR.CoreUtils;

namespace PharmaLabVR.Input
{
    // Moves only the XR origin's horizontal position relative to the parked
    // tool. Never scales calibrated instruments, updates material or resumes
    // the Native Core. Explicit UI request only; no automatic head teleport.
    public sealed class RecoveryAlignment : MonoBehaviour
    {
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private Transform dockAnchor;
        [SerializeField] private float reachDistanceMetres = 0.78f;

        public void Configure(XROrigin origin, Transform dock)
        {
            xrOrigin = origin;
            dockAnchor = dock;
        }

        public bool TrySnapTurn(int direction)
        {
            if (direction != -1 && direction != 1 ||
                xrOrigin == null || xrOrigin.Camera == null || dockAnchor == null ||
                !xrOrigin.gameObject.activeInHierarchy) return false;
            var headPivot = xrOrigin.Camera.transform.position;
            xrOrigin.transform.RotateAround(headPivot, Vector3.up,
                RecoveryAlignmentPolicy.SnapDegrees(direction));
            return true;
        }

        public bool TryRecenter()
        {
            if (xrOrigin == null || xrOrigin.Camera == null || dockAnchor == null ||
                !xrOrigin.gameObject.activeInHierarchy) return false;
            var camera = xrOrigin.Camera.transform;
            var horizontalForward = Vector3.ProjectOnPlane(camera.forward, Vector3.up);
            if (horizontalForward.sqrMagnitude < 0.0001f) return false;
            var desiredEye = dockAnchor.position -
                horizontalForward.normalized * reachDistanceMetres;
            var move = desiredEye - camera.position;
            move.y = 0f; // Floor origin and tracked eye height remain untouched.
            if (float.IsNaN(move.x) || float.IsInfinity(move.x) ||
                float.IsNaN(move.z) || float.IsInfinity(move.z) ||
                move.sqrMagnitude > 4f) return false; // Refuse out-of-reach teleports.
            xrOrigin.transform.position += move;
            return true;
        }
    }
}
