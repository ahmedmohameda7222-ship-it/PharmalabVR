using NUnit.Framework;
using PharmaLabVR.UI;
using UnityEngine;

namespace PharmaLabVR.Tests.EditMode
{
    // Authored for deferred Unity Test Runner; headless tests the pure
    // AudioPreferenceRouting production policy but not the actual mixer.
    public sealed class AudioPreferencesOutputTests
    {
        [Test]
        public void P10_02_ChangingSharedSettingsUpdatesRealAudioListenerGain()
        {
            var previous = SetupPreferencesSelection.Current;
            var priorGain = AudioListener.volume;
            var go = new GameObject("audio-preference-test");
            try
            {
                go.AddComponent<AudioPreferencesOutput>();
                SetupPreferencesSelection.Select(previous.With(audioVolume: 0.35f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.35f).Within(1e-6f));
                SetupPreferencesSelection.Select(previous.With(audioVolume: 0f));
                Assert.That(AudioListener.volume, Is.EqualTo(0f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(go);
                SetupPreferencesSelection.Select(previous);
                AudioListener.volume = priorGain;
            }
        }
    }
}
