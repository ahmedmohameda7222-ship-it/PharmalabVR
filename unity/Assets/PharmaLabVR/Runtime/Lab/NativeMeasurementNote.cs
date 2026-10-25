using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PharmaLabVR.Session;
using PharmaLabVR.UI;

namespace PharmaLabVR.Lab
{
    // Constructs a provenance-rich annotation ONLY from an actual Current
    // Native observation displayed for the matching material revision.
    // Scientific computations remain exclusively in Native C++/IPhreeqc.
    public static class NativeMeasurementNote
    {
        // One scientific observation record per Native event and vessel
        // material revision. A learner's text/prediction does not masquerade
        // as a Native note, and independent events remain eligible.
        public static bool WasRecorded(IReadOnlyList<NotebookEntry> entries,
            string branchId, string eventSequence, string vesselId, string materialRevision)
        {
            if (entries == null || string.IsNullOrWhiteSpace(branchId) ||
                string.IsNullOrWhiteSpace(eventSequence) ||
                string.IsNullOrWhiteSpace(vesselId) ||
                string.IsNullOrWhiteSpace(materialRevision))
                return false;
            var fingerprint = " · vessel " + vesselId +
                " · material revision " + materialRevision +
                " · event " + eventSequence + " · Supported/Current";
            return entries.Any(item => item != null &&
                item.BranchId == branchId && item.EventSequence == eventSequence &&
                item.VesselId == vesselId &&
                item.RecordKind == NotebookRecordKind.NativePH &&
                item.Text != null &&
                item.Text.StartsWith("Native pH ", StringComparison.Ordinal) &&
                item.Text.Contains(fingerprint, StringComparison.Ordinal) &&
                item.Text.Contains("Research observation only; no certified endpoint inferred.",
                    StringComparison.Ordinal));
        }

        public static string Build(LabViewModel view, string vesselId,
            MeasurementAvailability availability, string displayedValue)
        {
            if (view?.Vessels == null || view.Observations == null ||
                string.IsNullOrWhiteSpace(view.EventSequence) ||
                string.IsNullOrWhiteSpace(vesselId) ||
                availability != MeasurementAvailability.Current ||
                !double.TryParse(displayedValue, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var ph) ||
                double.IsNaN(ph) || double.IsInfinity(ph))
                throw new InvalidOperationException("Native Current pH and session event are required.");
            if (!view.Vessels.Any(item => item.Id == vesselId) ||
                !view.Observations.Any(item => item.VesselId == vesselId &&
                    item.ObservableId == "pH"))
                throw new InvalidOperationException("The selected Native vessel has no pH observation.");
            var vessel = view.Vessels.First(item => item.Id == vesselId);
            var observation = view.Observations.First(item => item.VesselId == vesselId &&
                item.ObservableId == "pH");
            if (vessel.CommittedVolumeM3 <= 0d ||
                double.IsNaN(vessel.CommittedVolumeM3) ||
                double.IsInfinity(vessel.CommittedVolumeM3) ||
                observation.Support != "Supported" ||
                observation.Freshness != "Current" ||
                observation.ComputationState != "Ready" ||
                !observation.ValueAvailable ||
                double.IsNaN(observation.Value) ||
                double.IsInfinity(observation.Value) ||
                string.IsNullOrWhiteSpace(vessel.MaterialRevision) ||
                observation.AsOfMaterialRevision != vessel.MaterialRevision ||
                string.IsNullOrWhiteSpace(observation.ModelId) ||
                string.IsNullOrWhiteSpace(observation.DatabaseIdentity) ||
                string.IsNullOrWhiteSpace(observation.Maturity))
                throw new InvalidOperationException(
                    "Native pH model, freshness, material revision or provenance is not current.");
            // Prevent a stale UI label and a newer Native observation from
            // silently being joined as one learner evidence event. The same
            // fixed 2-decimal projection is used by CoreDriver.Measurement.
            var nativeDisplay = observation.Value.ToString("0.00", CultureInfo.InvariantCulture);
            if (!string.Equals(nativeDisplay, displayedValue, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Displayed pH is inconsistent with the latest raw Native observation.");
            return "Native pH " + ph.ToString("0.00", CultureInfo.InvariantCulture) +
                " · raw Native pH " + observation.Value.ToString("R", CultureInfo.InvariantCulture) +
                " · vessel " + vesselId +
                " · material revision " + vessel.MaterialRevision +
                " · event " + view.EventSequence +
                " · Supported/Current" +
                " · maturity " + observation.Maturity +
                " · model " + observation.ModelId +
                " · database " + observation.DatabaseIdentity +
                ". Research observation only; no certified endpoint inferred.";
        }
    }
}
