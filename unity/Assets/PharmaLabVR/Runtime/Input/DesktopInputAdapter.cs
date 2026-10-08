using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace PharmaLabVR.Input
{
    public sealed class DesktopInputAdapter : MonoBehaviour, ILabInputAdapter
    {
        [SerializeField] private Transform labFrame;
        [SerializeField] private Transform heldTool;
        [SerializeField] private Transform registeredTool;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform holdAnchor;
        [SerializeField] private LabCaptureTarget captureTarget;
        [SerializeField] private string heldToolId = "desktop-tool";
        [SerializeField] private string geometryProfileHash = "unassigned";
        private ulong sequence;
        private float actuator;
        private bool inputEnabled = true;

        public void Configure(Transform frame, Camera camera, Transform anchor, Transform tool, LabCaptureTarget target, string profileHash)
        {
            labFrame = frame;
            viewCamera = camera;
            holdAnchor = anchor;
            registeredTool = tool;
            captureTarget = target;
            geometryProfileHash = profileHash;
        }

        private void OnApplicationFocus(bool focused)
        {
            inputEnabled = false;
            actuator = 0f;
            if (focused) Debug.Log("InputHold: explicit Continue is required after focus recovery.");
        }

        public LabInputSample[] SampleInputs(ulong nowNs)
        {
            HandlePickOrPlace();
            if (!inputEnabled || registeredTool == null) return System.Array.Empty<LabInputSample>();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { inputEnabled = false; actuator = 0f; return System.Array.Empty<LabInputSample>(); }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) actuator = Mathf.Clamp01(actuator + Mouse.current.delta.ReadValue().x * 0.005f);
            var sampledTool = heldTool != null ? heldTool : registeredTool;
            var sampledActuator = heldTool != null ? actuator : 0f;
            return new[] { LabGeometryAdapter.BuildSample(
                heldToolId, ++sequence, nowNs, labFrame, sampledTool, sampledActuator,
                geometryProfileHash, true, captureTarget != null ? captureTarget.Estimate(sampledTool) : null) };
        }

        public void ResetBaselines() { actuator = 0f; sequence = 0; }
        public void SetEnabled(bool value) { inputEnabled = value; if (!value) actuator = 0f; }

        private void HandlePickOrPlace()
        {
            if (!inputEnabled || Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            actuator = 0f;
            if (heldTool != null)
            {
                heldTool.SetParent(null, true);
                heldTool = null;
                return;
            }
            if (viewCamera == null || holdAnchor == null || !Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out var hit, 3f)) return;
            var grabbable = hit.collider.GetComponentInParent<DesktopGrabbable>();
            if (grabbable == null) return;
            heldTool = grabbable.transform;
            heldTool.SetParent(holdAnchor, false);
            heldTool.localPosition = Vector3.zero;
            heldTool.localRotation = Quaternion.identity;
            heldToolId = grabbable.ToolId;
        }
    }

    public sealed class DesktopGrabbable : MonoBehaviour
    {
        [SerializeField] private string toolId = "research-tool";
        public string ToolId => toolId;
    }
}
