using NUnit.Framework;
using PharmaLabVR.UI;

namespace PharmaLabVR.Tests.EditMode
{
    // Authored for final Unity campaign; product codec is compiled and
    // exercised now by the independent headless C# routing suite.
    public sealed class SetupPreferencesCodecTests
    {
        [Test]
        public void P10_02_RoundtripPreservesAllEightRealAccessibilityChoices()
        {
            var selected = new LabSetupPreferences("Seated", "Left", 0.77f,
                1.25f, true, true, true, 0.45f);
            var encoded = SetupPreferencesCodec.Encode(selected);
            Assert.That(SetupPreferencesCodec.TryDecode(encoded, out var restored), Is.True);
            Assert.That(restored.Posture, Is.EqualTo("Seated"));
            Assert.That(restored.DominantHand, Is.EqualTo("Left"));
            Assert.That(restored.BenchHeightMeters, Is.EqualTo(0.77f));
            Assert.That(restored.TextScale, Is.EqualTo(1.25f));
            Assert.That(restored.CaptionsEnabled && restored.ReducedMotion && restored.HighContrast, Is.True);
            Assert.That(restored.AudioVolume, Is.EqualTo(0.45f));
            Assert.That(SetupPreferencesCodec.TryDecode(encoded.Replace("0.77", "NaN"), out _), Is.False);
        }
    }
}
