using UnityEngine;

namespace PharmaLabVR.UI
{
    // Scene-local live output subscribed to one immutable preference stream.
    // One instance in Boot, one in Lab; never part of the Native simulation.
    public sealed class AudioPreferencesOutput : MonoBehaviour
    {
        private void OnEnable()
        {
            SetupPreferencesSelection.Changed += Apply;
            Apply(SetupPreferencesSelection.Current);
        }

        private void OnDisable()
        {
            SetupPreferencesSelection.Changed -= Apply;
        }

        private static void Apply(LabSetupPreferences selected)
        {
            AudioListener.volume = AudioPreferenceRouting.Gain(selected);
        }
    }
}
