using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using PharmaLabVR.Core;

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
        [SerializeField] private CoreDriver coreDriver;
        [SerializeField] private string heldToolId = "desktop-tool";
        [SerializeField] private string geometryProfileHash = "unassigned";
        private ulong sequence;
        private float actuator;
        private bool inputEnabled = true;
        private bool awaitingFocusContinue;
        public bool AwaitingFocusContinue => awaitingFocusContinue;

        private void Awake() => RefreshRegisteredToolId();

        public void Configure(Transform frame, Camera camera, Transform anchor, Transform tool, LabCaptureTarget target, string profileHash, CoreDriver driver = null)
        {
            labFrame = frame;
            viewCamera = camera;
            holdAnchor = anchor;
            registeredTool = tool;
            captureTarget = target;
            geometryProfileHash = profileHash;
            coreDriver = driver;
            RefreshRegisteredToolId();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                inputEnabled = false;
                awaitingFocusContinue = true;
                actuator = 0f;
                Debug.LogWarning("InputHold: focus was lost; explicit Continue is required.");
            }
        }

        public bool ContinueAfterFocusRecovery()
        {
            if (coreDriver != null && coreDriver.IsTimeHeld && !coreDriver.ResumeAfterTimeHold()) return false;
            ResetBaselines();
            awaitingFocusContinue = false;
            inputEnabled = true;
            return true;
        }

        private void OnGUI()
        {
            if (!awaitingFocusContinue || !Application.isFocused) return;
            GUI.Box(new Rect(20f, 20f, 330f, 92f), "Input paused after focus loss");
            if (GUI.Button(new Rect(45f, 60f, 280f, 36f), "Continue lab input")) ContinueAfterFocusRecovery();
        }

        public LabInputSample[] SampleInputs(ulong nowNs)
        {
            RefreshRegisteredToolId();
            HandlePickOrPlace();
            if (!inputEnabled || registeredTool == null) return System.Array.Empty<LabInputSample>();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { inputEnabled = false; awaitingFocusContinue = true; actuator = 0f; return System.Array.Empty<LabInputSample>(); }
            var pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Mouse.current != null && ShouldAdjustActuator(pointerOverUi, Mouse.current.leftButton.isPressed))
                actuator = Mathf.Clamp01(actuator + Mouse.current.delta.ReadValue().x * 0.005f);
            ManipulateHeldTool();
            var sampledTool = heldTool != null ? heldTool : registeredTool;
            var sampledActuator = heldTool != null ? actuator : 0f;
            return new[] { LabGeometryAdapter.BuildSample(
                heldToolId, ++sequence, nowNs, labFrame, sampledTool, sampledActuator,
                geometryProfileHash, true, captureTarget != null ? captureTarget.Estimate(sampledTool) : null) };
        }

        public void ResetBaselines() { actuator = 0f; sequence = 0; }
        public void SetEnabled(bool value) { inputEnabled = value && !awaitingFocusContinue; if (!value) actuator = 0f; }

        private static bool ShouldAdjustActuator(bool pointerOverUi, bool leftButtonPressed) => leftButtonPressed && !pointerOverUi;

        private void RefreshRegisteredToolId()
        {
            if (registeredTool != null && registeredTool.TryGetComponent<DesktopGrabbable>(out var grabbable))
                heldToolId = grabbable.ToolId;
        }

        private void HandlePickOrPlace()
        {
            if (!inputEnabled || Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            actuator = 0f;
            if (heldTool != null)
            {
                if (heldTool.TryGetComponent<Rigidbody>(out var heldBody)) heldBody.isKinematic = false;
                heldTool = null;
                return;
            }
            if (viewCamera == null || holdAnchor == null || !Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out var hit, 3f)) return;
            var grabbable = hit.collider.GetComponentInParent<DesktopGrabbable>();
            if (grabbable == null) return;
            BeginHold(grabbable.transform);
            heldToolId = grabbable.ToolId;
        }

        private void BeginHold(Transform tool)
        {
            heldTool = tool;
            if (heldTool != null && heldTool.TryGetComponent<Rigidbody>(out var body)) body.isKinematic = true;
        }

        private void ManipulateHeldTool()
        {
            if (heldTool == null || Keyboard.current == null || labFrame == null) return;
            var translation = Vector3.zero;
            if (Keyboard.current.leftArrowKey.isPressed) translation += Vector3.left;
            if (Keyboard.current.rightArrowKey.isPressed) translation += Vector3.right;
            if (Keyboard.current.upArrowKey.isPressed) translation += Vector3.forward;
            if (Keyboard.current.downArrowKey.isPressed) translation += Vector3.back;
            if (Keyboard.current.pageUpKey.isPressed) translation += Vector3.up;
            if (Keyboard.current.pageDownKey.isPressed) translation += Vector3.down;
            heldTool.position += labFrame.TransformDirection(translation.normalized) * (0.20f * Time.unscaledDeltaTime);
            var tilt = 0f;
            if (Keyboard.current.zKey.isPressed) tilt += 1f;
            if (Keyboard.current.xKey.isPressed) tilt -= 1f;
            if (!Mathf.Approximately(tilt, 0f)) heldTool.Rotate(labFrame.forward, tilt * 45f * Time.unscaledDeltaTime, Space.World);
        }
    }

    public sealed class DesktopGrabbable : MonoBehaviour
    {
        [SerializeField] private string toolId = "research-tool";
        public string ToolId => toolId;
    }
}
