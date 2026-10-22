using UnityEngine;

namespace PharmaLabVR.Input
{
    public sealed class DesktopHoldAnchorFollower : MonoBehaviour
    {
        [SerializeField] private Transform view;
        [SerializeField] private Vector3 viewSpaceOffset = new Vector3(0.25f, -0.18f, 0.65f);

        public void Configure(Transform viewTransform, Vector3 offset)
        {
            view = viewTransform;
            viewSpaceOffset = offset;
            RefreshPose();
        }

        private void LateUpdate() => RefreshPose();

        private void RefreshPose()
        {
            if (view == null) return;
            transform.SetPositionAndRotation(view.TransformPoint(viewSpaceOffset), view.rotation);
        }
    }
}
