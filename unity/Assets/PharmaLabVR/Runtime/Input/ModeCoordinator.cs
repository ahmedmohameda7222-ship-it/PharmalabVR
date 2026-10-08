using System;
using UnityEngine;
using PharmaLabVR.UI;

namespace PharmaLabVR.Input
{
    public enum ApplicationMode { Desktop, VirtualReality }

    public sealed class ModeCoordinator : MonoBehaviour
    {
        [SerializeField] private GameObject desktopRig;
        [SerializeField] private GameObject xrRig;
        public ApplicationMode Mode { get; private set; }
        public event Action<ApplicationMode> ModeChanged;

        private void Start() => Select(BootMenu.RequestedMode);

        public void Configure(GameObject desktop, GameObject xr)
        {
            desktopRig = desktop;
            xrRig = xr;
            Select(BootMenu.RequestedMode);
        }

        public void Select(ApplicationMode mode)
        {
            if (desktopRig == null || xrRig == null) throw new InvalidOperationException("Both mode rigs must be assigned.");
            desktopRig.SetActive(mode == ApplicationMode.Desktop);
            xrRig.SetActive(mode == ApplicationMode.VirtualReality);
            foreach (var adapter in desktopRig.GetComponentsInChildren<MonoBehaviour>(true))
                if (adapter is ILabInputAdapter desktopInputAdapter) desktopInputAdapter.SetEnabled(mode == ApplicationMode.Desktop);
            foreach (var adapter in xrRig.GetComponentsInChildren<MonoBehaviour>(true))
                if (adapter is ILabInputAdapter xrInputAdapter) xrInputAdapter.SetEnabled(mode == ApplicationMode.VirtualReality);
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }
    }
}
