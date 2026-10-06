using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PharmaLabVR.Input
{
    public sealed class DesktopCameraController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float pitchLimit = 85f;
        private float pitch;

        private void Update()
        {
            if (Keyboard.current == null || Mouse.current == null) return;
            var movement = Vector3.zero;
            if (Keyboard.current.wKey.isPressed) movement += Vector3.forward;
            if (Keyboard.current.sKey.isPressed) movement += Vector3.back;
            if (Keyboard.current.aKey.isPressed) movement += Vector3.left;
            if (Keyboard.current.dKey.isPressed) movement += Vector3.right;
            if (Keyboard.current.qKey.isPressed) movement += Vector3.down;
            if (Keyboard.current.eKey.isPressed) movement += Vector3.up;
            transform.Translate(movement.normalized * (moveSpeed * Time.unscaledDeltaTime), Space.Self);

            if (!Mouse.current.rightButton.isPressed || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
            var delta = Mouse.current.delta.ReadValue() * lookSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
            transform.rotation = Quaternion.Euler(pitch, transform.eulerAngles.y + delta.x, 0f);
        }
    }
}
