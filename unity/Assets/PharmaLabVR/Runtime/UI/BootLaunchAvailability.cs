using PharmaLabVR.Input;

namespace PharmaLabVR.UI
{
    // Domain-free launch admission. Populated from real Unity XR/runtime state by
    // BootMenu, also compiled by the headless routing contract harness.
    // It neither initializes XR nor creates a Native scientific session.
    public readonly struct BootLaunchAvailability
    {
        public bool IsAndroid { get; }
        public bool XrLoaderActive { get; }
        public bool HeadsetPresent { get; }
        public bool LabSceneAvailable { get; }

        public BootLaunchAvailability(bool isAndroid, bool xrLoaderActive,
            bool headsetPresent, bool labSceneAvailable)
        {
            IsAndroid = isAndroid;
            XrLoaderActive = xrLoaderActive;
            HeadsetPresent = headsetPresent;
            LabSceneAvailable = labSceneAvailable;
        }

        public bool CanEnter(ApplicationMode mode)
        {
            if (!LabSceneAvailable) return false;
            if (mode == ApplicationMode.Desktop) return !IsAndroid;
            if (mode == ApplicationMode.VirtualReality)
                return XrLoaderActive && HeadsetPresent;
            return false;
        }

        public string UnavailableReason(ApplicationMode mode)
        {
            if (!LabSceneAvailable) return "Lab scene is missing from this build.";
            if (mode == ApplicationMode.Desktop)
                return IsAndroid ? "Android requires a compatible VR headset." : string.Empty;
            if (mode != ApplicationMode.VirtualReality) return "Unsupported launch mode.";
            if (!XrLoaderActive) return "OpenXR runtime is unavailable; use Desktop on PC.";
            if (!HeadsetPresent) return "VR headset is not available or connected.";
            return string.Empty;
        }
    }
}
