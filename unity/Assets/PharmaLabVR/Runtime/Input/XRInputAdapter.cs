using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PharmaLabVR.Input
{
    public sealed class XRInputAdapter : MonoBehaviour, ILabInputAdapter
    {
        [SerializeField] private Transform labFrame;
        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;
        [SerializeField] private ActionBasedController leftActionController;
        [SerializeField] private ActionBasedController rightActionController;
        [SerializeField] private Transform registeredTool;
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private LabCaptureTarget captureTarget;
        [SerializeField] private string toolId = "research-tool";
        [SerializeField] private string geometryProfileHash = "unassigned";
        private ulong sequence;
        private bool inputEnabled;

        public void Configure(
            Transform frame,
            ActionBasedController left,
            ActionBasedController right,
            Transform tool,
            XRGrabInteractable grab,
            LabCaptureTarget target,
            string profileHash)
        {
            labFrame = frame;
            leftActionController = left;
            rightActionController = right;
            leftController = left != null ? left.transform : null;
            rightController = right != null ? right.transform : null;
            registeredTool = tool;
            grabInteractable = grab;
            captureTarget = target;
            geometryProfileHash = profileHash;
        }

        public LabInputSample[] SampleInputs(ulong nowNs)
        {
            if (!inputEnabled || labFrame == null || leftController == null || rightController == null || registeredTool == null)
                return System.Array.Empty<LabInputSample>();
            var selected = grabInteractable != null && grabInteractable.isSelected;
            var actuator = selected ? Mathf.Max(Actuator(leftActionController), Actuator(rightActionController)) : 0f;
            return new[] {
                LabGeometryAdapter.BuildSample(
                    toolId, ++sequence, nowNs, labFrame, registeredTool, actuator, geometryProfileHash,
                    !selected || Tracked(leftActionController) || Tracked(rightActionController),
                    captureTarget != null ? captureTarget.Estimate(registeredTool) : null)
            };
        }

        private static float Actuator(ActionBasedController controller)
        {
            var action = controller != null ? controller.selectActionValue.action : null;
            return action != null ? Mathf.Clamp01(action.ReadValue<float>()) : 0f;
        }

        private static bool Tracked(ActionBasedController controller)
        {
            if (controller == null || !controller.gameObject.activeInHierarchy) return false;
            var action = controller.isTrackedAction.action;
            return action == null || !action.enabled || action.ReadValue<float>() > 0.5f;
        }

        public void ResetBaselines() { sequence = 0; }
        public void SetEnabled(bool value) { inputEnabled = value; }
    }
}
