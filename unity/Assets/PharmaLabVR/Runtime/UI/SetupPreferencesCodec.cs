using System;
using System.Globalization;

namespace PharmaLabVR.UI
{
    // Strict versioned, culture-invariant, non-chemical user option codec.
    // Never deserialize Native inventory or a simulation profile as settings.
    public static class SetupPreferencesCodec
    {
        private static readonly CultureInfo StableCulture = CultureInfo.InvariantCulture;
        public static string Encode(LabSetupPreferences current)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            return string.Join(";", new[]
            {
                "1", current.Posture, current.DominantHand,
                current.BenchHeightMeters.ToString("R", StableCulture),
                current.TextScale.ToString("R", StableCulture),
                current.CaptionsEnabled ? "1" : "0",
                current.ReducedMotion ? "1" : "0",
                current.HighContrast ? "1" : "0",
                current.AudioVolume.ToString("R", StableCulture)
            });
        }

        public static bool TryDecode(string value, out LabSetupPreferences selected)
        {
            selected = null;
            if (string.IsNullOrEmpty(value) || value.Length > 256) return false;
            var parts = value.Split(';');
            if (parts.Length != 9 || parts[0] != "1" ||
                !float.TryParse(parts[3], NumberStyles.Float, StableCulture, out var benchHeight) ||
                !float.TryParse(parts[4], NumberStyles.Float, StableCulture, out var textScale) ||
                !float.TryParse(parts[8], NumberStyles.Float, StableCulture, out var audioVolume) ||
                !Finite(benchHeight) || !Finite(textScale) || !Finite(audioVolume) ||
                !ReadFlag(parts[5], out var captions) || !ReadFlag(parts[6], out var reducedMotion) ||
                !ReadFlag(parts[7], out var highContrast))
                return false;
            try
            {
                selected = new LabSetupPreferences(parts[1], parts[2], benchHeight,
                    textScale, captions, reducedMotion, highContrast, audioVolume);
                return true;
            }
            catch (ArgumentException)
            {
                selected = null;
                return false;
            }
        }

        private static bool ReadFlag(string value, out bool result)
        {
            result = value == "1";
            return value == "0" || value == "1";
        }
        private static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);
    }
}
