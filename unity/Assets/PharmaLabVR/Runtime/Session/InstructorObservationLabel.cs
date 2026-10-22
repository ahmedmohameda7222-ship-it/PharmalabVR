using System;
using System.Globalization;

namespace PharmaLabVR.Session
{
    // Read-only scientific provenance, not a learner grade or model inference.
    public static class InstructorObservationLabel
    {
        public static string Render(string vesselId, string displayedValue,
            double rawValue, bool supportedCurrentNativeValue, string materialRevision)
        {
            var name = string.IsNullOrWhiteSpace(vesselId) ? "unknown vessel" : vesselId;
            var revision = string.IsNullOrWhiteSpace(materialRevision) ?
                "unknown" : materialRevision;
            var prefix = "Native " + name + " pH ";
            if (!supportedCurrentNativeValue || !double.IsFinite(rawValue) ||
                !string.Equals(displayedValue,
                    rawValue.ToString("0.00", CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
                return prefix + "-- · rev " + revision +
                    " (not an available, consistent Native pH observation)";
            return prefix + "display " + displayedValue +
                " · raw " + rawValue.ToString("R", CultureInfo.InvariantCulture) +
                " · rev " + revision;
        }
    }
}
