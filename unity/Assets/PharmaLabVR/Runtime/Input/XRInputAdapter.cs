using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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
            var owner = selected ? SelectingController() : null;
            var directBodyGrip = selected && grabInteractable.firstInteractorSelecting is XRDirectInteractor;
            var actuator = selected ? Actuator(owner, directBodyGrip) : 0f;
            // Every admitted recovery baseline must reflect actual head and both
            // controller tracking. An unheld tool is not evidence of tracked XR.
            var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            var headTracked = head.isValid &&
                head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracked) && tracked;
            var trackingValid = XRTrackingAdmission.HasFreshTracking(
                headTracked, Tracked(leftActionController), Tracked(rightActionController));
            return new[] {
                LabGeometryAdapter.BuildSample(
                    toolId, ++sequence, nowNs, labFrame, registeredTool, actuator, geometryProfileHash,
                    trackingValid && (!selected || Tracked(owner)),
                    captureTarget != null ? captureTarget.Estimate(registeredTool) : null)
            };
        }

        private static float Actuator(ActionBasedController controller, bool directBodyGrip)
        {
            if (controller == null) return 0f;
            var ray = controller.GetComponentInChildren<XRRayInteractor>(true);
            if (!InteractionOwnership.MayActuateValve(directBodyGrip, Tracked(controller),
                ray != null && ray.IsOverUIGameObject())) return 0f;
            var grip = controller.selectActionValue.action;
            var trigger = controller.activateActionValue.action;
            return ChooseActuatorValue(
                grip != null ? grip.ReadValue<float>() : 0f,
                trigger != null ? trigger.ReadValue<float>() : 0f);
        }

        private static float ChooseActuatorValue(float gripValue, float triggerValue) => Mathf.Clamp01(triggerValue);

        private ActionBasedController SelectingController()
        {
            var interactor = grabInteractable != null ? grabInteractable.firstInteractorSelecting : null;
            var selectingTransform = interactor?.transform;
            if (selectingTransform == null) return null;
            if (leftController != null && selectingTransform.IsChildOf(leftController)) return leftActionController;
            if (rightController != null && selectingTransform.IsChildOf(rightController)) return rightActionController;
            return null;
        }

        private static bool Tracked(ActionBasedController controller)
        {
            if (controller == null || !controller.gameObject.activeInHierarchy) return false;
            var action = controller.isTrackedAction.action;
            return action != null && action.enabled && action.ReadValue<float>() > 0.5f;
        }

        public void ResetBaselines() { }
        public void AdvanceSequence(string id, ulong watermark)
        {
            if (id == toolId && sequence < watermark) sequence = watermark;
        }
        public void SetEnabled(bool value) { inputEnabled = value; }
    }
}
