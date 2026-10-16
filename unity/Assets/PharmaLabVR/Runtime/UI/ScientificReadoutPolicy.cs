using System;

namespace PharmaLabVR.UI
{
    // Presentation-only classification of immutable Native observation metadata.
    // No numerical predictions, solver fallback, or material changes occur here.
    public static class ScientificReadoutPolicy
    {
        public static MeasurementAvailability Resolve(string support,
            string computationState, string freshness, double value, bool valueAvailable)
        {
            if (support != "Supported") return MeasurementAvailability.Unsupported;
            if (computationState == "Failed") return MeasurementAvailability.Failed;
            if (freshness == "Stale") return MeasurementAvailability.Stale;
            if (freshness == "Current" && computationState == "Ready")
                return valueAvailable && double.IsFinite(value) ?
                    MeasurementAvailability.Current : MeasurementAvailability.Failed;
            return MeasurementAvailability.Pending;
        }

        public static string Describe(string vesselId, MeasurementAvailability availability,
            string displayValue, string support, string freshness,
            string maturity, string modelIdentity)
        {
            // Defense in depth: even a stale client label accidentally carrying
            // numeric text cannot reveal it as an authoritative current reading.
            var shown = availability == MeasurementAvailability.Current &&
                !string.IsNullOrWhiteSpace(displayValue) ? displayValue : "--";
            var model = string.IsNullOrWhiteSpace(modelIdentity) ?
                "No supported model" : modelIdentity;
            return "pH " + vesselId + ": " + shown + " · " + availability +
                "\n" + (support ?? "Unknown") + " / " + (freshness ?? "Absent") +
                " · " + (maturity ?? "Research") + " · " + model;
        }
    }
}
