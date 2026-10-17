using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using PharmaLabVR.Session;

namespace PharmaLabVR.Input
{
    public sealed class DesktopCameraController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float pitchLimit = 85f;
        private float pitch;
        private Camera sceneCamera;
        private DesktopInputAdapter desktopInput;
        public DesktopInputAdapter ToolInputAdapter => desktopInput;
        public float PitchDegrees => pitch;

        public void Configure(DesktopInputAdapter owner)
        {
            desktopInput = owner != null ? owner :
                throw new System.ArgumentNullException(nameof(owner));
            // SceneBuilder assigns the authored view rotation after AddComponent
            // (and therefore after Awake). The first right-drag must not snap
            // the camera back to pitch zero.
            pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
        }

        private void Awake()
        {
            pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
            sceneCamera = GetComponent<Camera>();
            desktopInput = GetComponentInParent<DesktopInputAdapter>();
        }

        private void Update()
        {
            if (desktopInput == null) desktopInput = GetComponentInParent<DesktopInputAdapter>();
            if (Keyboard.current == null || Mouse.current == null) return;
            // A UI-focused gesture must never pan or zoom the world beneath it.
            var pointerOverUi =
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                SessionController.PointerOverDesktopHud(Mouse.current.position.ReadValue());
            if (pointerOverUi) return;
            var movement = Vector3.zero;
            if (Keyboard.current.wKey.isPressed) movement += Vector3.forward;
            if (Keyboard.current.sKey.isPressed) movement += Vector3.back;
            if (Keyboard.current.aKey.isPressed) movement += Vector3.left;
            if (Keyboard.current.dKey.isPressed) movement += Vector3.right;
            // Q/E exclusively tilt the held instrument; they never move camera.
            transform.Translate(movement.normalized * (moveSpeed * Time.unscaledDeltaTime), Space.Self);
            var deltaScroll = Mouse.current.scroll.ReadValue().y;
            if (sceneCamera != null &&
                DesktopWheelRouting.Resolve(desktopInput != null && desktopInput.HasHeldTool,
                    false, deltaScroll) == DesktopWheelTarget.CameraZoom)
                sceneCamera.fieldOfView = (float)DesktopWheelRouting.ZoomFieldOfViewDegrees(
                    sceneCamera.fieldOfView, deltaScroll);

            if (!Mouse.current.rightButton.isPressed) return;
            var delta = Mouse.current.delta.ReadValue() * lookSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
            transform.rotation = Quaternion.Euler(pitch, transform.eulerAngles.y + delta.x, 0f);
        }
    }
}
