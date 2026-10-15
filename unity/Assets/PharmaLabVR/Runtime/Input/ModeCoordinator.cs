using System;
using UnityEngine;
using PharmaLabVR.UI;
using PharmaLabVR.Core;

namespace PharmaLabVR.Input
{
    public enum ApplicationMode { Desktop, VirtualReality }

    public sealed class ModeCoordinator : MonoBehaviour
    {
        [SerializeField] private GameObject desktopRig;
        [SerializeField] private GameObject xrRig;
        [SerializeField] private CoreDriver coreDriver;
        public ApplicationMode Mode { get; private set; }
        public event Action<ApplicationMode> ModeChanged;

        private Camera desktopCamera;
        private float baselineDesktopEyeHeight;
        private bool desktopEyeHeightCaptured;

        private void OnEnable() => SetupPreferencesSelection.Changed += ApplySetupPreferences;
        private void OnDisable() => SetupPreferencesSelection.Changed -= ApplySetupPreferences;
        private void Start() => Apply(BootMenu.RequestedMode);

        public void Configure(GameObject desktop, GameObject xr, CoreDriver driver = null)
        {
            desktopRig = desktop;
            xrRig = xr;
            coreDriver = driver;
            Apply(BootMenu.RequestedMode);
        }

        public void Select(ApplicationMode mode)
        {
            if (Application.isPlaying && mode != Mode && coreDriver != null && !coreDriver.BeginModeChange(mode))
                throw new InvalidOperationException("Native core rejected the mode change.");
            Apply(mode);
            coreDriver?.SynchronizeInputSequences();
        }

        private void ApplySetupPreferences(LabSetupPreferences setup)
        {
            AudioListener.volume = setup.AudioVolume;
            if (desktopRig == null) return;
            if (!desktopEyeHeightCaptured)
            {
                desktopCamera = desktopRig.GetComponentInChildren<Camera>(true);
                if (desktopCamera == null) return;
                baselineDesktopEyeHeight = desktopCamera.transform.localPosition.y;
                desktopEyeHeightCaptured = true;
            }
            var position = desktopCamera.transform.localPosition;
            position.y = baselineDesktopEyeHeight + (setup.BenchHeightMeters - 0.85f) +
                (setup.Posture == "Seated" ? -0.25f : 0f);
            desktopCamera.transform.localPosition = position;
        }

        private void Apply(ApplicationMode mode)
        {
            if (desktopRig == null || xrRig == null) throw new InvalidOperationException("Both mode rigs must be assigned.");
            desktopRig.SetActive(mode == ApplicationMode.Desktop);
            xrRig.SetActive(mode == ApplicationMode.VirtualReality);
            foreach (var adapter in desktopRig.GetComponentsInChildren<MonoBehaviour>(true))
                if (adapter is ILabInputAdapter desktopInputAdapter) desktopInputAdapter.SetEnabled(mode == ApplicationMode.Desktop);
            foreach (var adapter in xrRig.GetComponentsInChildren<MonoBehaviour>(true))
                if (adapter is ILabInputAdapter xrInputAdapter) xrInputAdapter.SetEnabled(mode == ApplicationMode.VirtualReality);
            Mode = mode;
            ApplySetupPreferences(SetupPreferencesSelection.Current);
            ModeChanged?.Invoke(mode);
        }
    }
}
