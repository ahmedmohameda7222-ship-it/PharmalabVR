using UnityEngine;

namespace PharmaLabVR.Input
{
    public sealed class XRInputAdapter : MonoBehaviour, ILabInputAdapter
    {
        [SerializeField] private Transform labFrame;
        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;
        [SerializeField] private string geometryProfileHash = "unassigned";
        private ulong sequence;
        private bool inputEnabled;

        public LabInputSample[] SampleInputs(ulong nowNs)
        {
            if (!inputEnabled) return System.Array.Empty<LabInputSample>();
            return new[] {
                LabGeometryAdapter.BuildSample("left-controller", ++sequence, nowNs, labFrame, leftController, 0f, geometryProfileHash, leftController.gameObject.activeInHierarchy),
                LabGeometryAdapter.BuildSample("right-controller", ++sequence, nowNs, labFrame, rightController, 0f, geometryProfileHash, rightController.gameObject.activeInHierarchy)
            };
        }

        public void ResetBaselines() { sequence = 0; }
        public void SetEnabled(bool value) { inputEnabled = value; }
    }
}
