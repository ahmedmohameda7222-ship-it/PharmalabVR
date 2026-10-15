using System;

namespace PharmaLabVR.UI
{
    // Unity-independent presentation adapter; no Native chemistry or timing.
    public static class AudioPreferenceRouting
    {
        public static float Gain(LabSetupPreferences selected)
        {
            if (selected == null) throw new ArgumentNullException(nameof(selected));
            return selected.AudioVolume;
        }
    }
}
