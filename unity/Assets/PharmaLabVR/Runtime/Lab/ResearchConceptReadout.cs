using System;
using System.Globalization;
using System.Linq;

namespace PharmaLabVR.Lab
{
    // Pure teaching readout of actually observed Native material and IPhreeqc
    // status. No equilibrium calculations, virtual titration curves or grades.
    public static class ResearchConceptReadout
    {
        public static string Describe(LabViewModel native, string vesselId)
        {
            if (native?.Vessels == null || string.IsNullOrWhiteSpace(vesselId) ||
                !native.Vessels.Any(v => v.Id == vesselId))
                return "Native concept inspection unavailable: select an existing vessel.";
            var vessel = native.Vessels.First(v => v.Id == vesselId);
            if (!double.IsFinite(vessel.CommittedVolumeM3) ||
                !double.IsFinite(vessel.CapacityM3) ||
                vessel.CapacityM3 <= 0d || vessel.CommittedVolumeM3 < 0d ||
                vessel.CommittedVolumeM3 > vessel.CapacityM3 + 1e-10)
                return "Native material volume unavailable; no scientific interpretation.";
            var materialRevision = vessel.MaterialRevision ?? "unknown";
            var result = "Native event " + (native.EventSequence ?? "unknown") +
                " · vessel " + vesselId +
                "\nResearch additive inventory " +
                (vessel.CommittedVolumeM3 * 1e6).ToString("0.000", CultureInfo.InvariantCulture) +
                " mL (capacity " +
                (vessel.CapacityM3 * 1e6).ToString("0.000", CultureInfo.InvariantCulture) +
                " mL)" +
                "\nNative material revision " + materialRevision;
            var observation = native.Observations?.FirstOrDefault(
                item => item.VesselId == vesselId && item.ObservableId == "pH");
            if (observation == null ||
                observation.Support != "Supported" ||
                observation.Freshness != "Current" ||
                observation.ComputationState != "Ready" ||
                !observation.ValueAvailable ||
                !double.IsFinite(observation.Value) ||
                observation.AsOfMaterialRevision != vessel.MaterialRevision ||
                string.IsNullOrWhiteSpace(observation.ModelId) ||
                string.IsNullOrWhiteSpace(observation.DatabaseIdentity))
                return result + "\npH unavailable: Native support, freshness, value or material revision is not confirmed." +
                    "\nNo endpoint or grade can be inferred.";
            return result +
                "\nCurrent Native pH " +
                observation.Value.ToString("0.000", CultureInfo.InvariantCulture) +
                " · Supported/Current" +
                "\nResearch model " + observation.ModelId +
                "\nDatabase " + observation.DatabaseIdentity +
                "\nMaturity " + (observation.Maturity ?? "unknown") +
                ". Observation is not an endpoint or certified score.";
        }
    }
}
