using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace PharmaLabVR.Input
{
    public sealed class XRInputAdapter : MonoBehaviour, ILabInputAdapter
    {
        [SerializeField] private Transform labFrame;
        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;
        [SerializeField] private ActionBasedController leftActionController;
        [SerializeField] private ActionBasedController rightActionController;
        [SerializeField] private string geometryProfileHash = "unassigned";
        private ulong sequence;
        private bool inputEnabled;

        public void Configure(
            Transform frame,
            ActionBasedController left,
            ActionBasedController right,
            string profileHash)
        {
            labFrame = frame;
            leftActionController = left;
            rightActionController = right;
            leftController = left != null ? left.transform : null;
            rightController = right != null ? right.transform : null;
            geometryProfileHash = profileHash;
        }

        public LabInputSample[] SampleInputs(ulong nowNs)
        {
            if (!inputEnabled || labFrame == null || leftController == null || rightController == null)
                return System.Array.Empty<LabInputSample>();
            return new[] {
                LabGeometryAdapter.BuildSample("left-controller", ++sequence, nowNs, labFrame, leftController, Actuator(leftActionController), geometryProfileHash, Tracked(leftActionController)),
                LabGeometryAdapter.BuildSample("right-controller", ++sequence, nowNs, labFrame, rightController, Actuator(rightActionController), geometryProfileHash, Tracked(rightActionController))
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
