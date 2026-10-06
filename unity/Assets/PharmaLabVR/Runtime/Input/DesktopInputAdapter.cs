using UnityEngine;
using UnityEngine.InputSystem;

namespace PharmaLabVR.Input
{
    public sealed class DesktopInputAdapter : MonoBehaviour, ILabInputAdapter
    {
        [SerializeField] private Transform labFrame;
        [SerializeField] private Transform heldTool;
        [SerializeField] private string heldToolId = "desktop-tool";
        [SerializeField] private string geometryProfileHash = "unassigned";
        private ulong sequence;
        private float actuator;
        private bool inputEnabled = true;

        private void OnApplicationFocus(bool focused)
        {
            inputEnabled = false;
            actuator = 0f;
            if (focused) Debug.Log("InputHold: explicit Continue is required after focus recovery.");
        }

        public LabInputSample[] SampleInputs(ulong nowNs)
        {
            if (!inputEnabled || heldTool == null) return System.Array.Empty<LabInputSample>();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { inputEnabled = false; actuator = 0f; return System.Array.Empty<LabInputSample>(); }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) actuator = Mathf.Clamp01(actuator + Mouse.current.delta.ReadValue().x * 0.005f);
            return new[] { LabGeometryAdapter.BuildSample(heldToolId, ++sequence, nowNs, labFrame, heldTool, actuator, geometryProfileHash, true) };
        }

        public void ResetBaselines() { actuator = 0f; sequence = 0; }
        public void SetEnabled(bool value) { inputEnabled = value; if (!value) actuator = 0f; }
    }
}
