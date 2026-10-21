using System;
using System.Linq;
using PharmaLabVR.Lab;

namespace PharmaLabVR.Protocols
{
    // Learner instructions are view-only. This class NEVER mutates Native
    // inventory, predicts endpoints, certifies learning, or assigns a grade.
    // SessionController binds the selection to the actual Native branch.
    public sealed class ResearchLessonJourney
    {
        public ProtocolDefinition Definition { get; }
        public string BranchId { get; }
        public string Instructions { get; }

        public ResearchLessonJourney(ProtocolDefinition definition, string nativeBranchId)
        {
            _ = new ProtocolRunner(definition);
            if (string.IsNullOrWhiteSpace(definition.id) || string.IsNullOrWhiteSpace(nativeBranchId) ||
                definition.steps == null || definition.steps.Length < 3 ||
                definition.steps.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("A complete ungraded Research lesson and Native branch are required.");
            Definition = definition;
            BranchId = nativeBranchId;
            Instructions = string.Join("\n", definition.steps.Select((step, i) =>
                (i + 1).ToString() + ". " + step));
        }

        public static bool IsSupportedPrediction(string choice) =>
            choice == "below-7" || choice == "at-least-7";

        // Prediction is learner intent, recorded before any material reaches
        // the receiving vessel. No result can be inferred from this choice.
        public bool CanPredict(string liveBranchId, LabViewModel native, bool held,
            string existingChoice)
        {
            if (held || !string.IsNullOrEmpty(existingChoice) ||
                !string.Equals(liveBranchId, BranchId, StringComparison.Ordinal) ||
                native?.Vessels == null || string.IsNullOrWhiteSpace(native.EventSequence))
                return false;
            return native.Vessels.Any(vessel => vessel.Id == "receiver" &&
                IsRepresented(vessel.CommittedVolumeM3, vessel.CapacityM3) &&
                vessel.CommittedVolumeM3 == 0d);
        }

        public string Summary(string liveNativeBranchId, LabViewModel native,
            bool paused, bool held)
        {
            var title = Definition.id + " · Research only · no certified score. ";
            if (string.IsNullOrWhiteSpace(liveNativeBranchId) ||
                !string.Equals(BranchId, liveNativeBranchId, StringComparison.Ordinal))
                return title + "Native branch changed. Reselect this lesson for the current session.";
            if (held) return title + "Hold: no experiment progress until Native recovery.";
            if (native?.Vessels == null)
                return title + "Native material state unavailable; no progress can be inferred.";
            if (paused) return title + "Pause: material frozen. Continue through the Native session.";
            if (!native.Vessels.Any(v => v.Id == "source"))
                return title + "Prepare supported Native stock in Setup.";
            if (!native.Vessels.Any(v => v.Id == "receiver"))
                return title + "Native receiver unavailable; no delivery evidence.";
            var source = native.Vessels.First(v => v.Id == "source");
            var receiver = native.Vessels.First(v => v.Id == "receiver");
            if (!IsRepresented(source.CommittedVolumeM3, source.CapacityM3))
                return title + "Prepare supported Native stock in Setup.";
            if (!IsRepresented(receiver.CommittedVolumeM3, receiver.CapacityM3))
                return title + "Native receiver unavailable; no delivery evidence.";
            if (Definition.id == "mixed-acid-titration-research")
            {
                if (!native.Vessels.Any(v => v.Id == "reserve-stock"))
                    return title + "Secondary prepared Native stock missing. This mixed-acid procedure is incomplete.";
                var reserve = native.Vessels.First(v => v.Id == "reserve-stock");
                if (!IsRepresented(reserve.CommittedVolumeM3, reserve.CapacityM3) ||
                    reserve.CommittedVolumeM3 <= 0d)
                    return title + "Secondary prepared Native stock missing. This mixed-acid procedure is incomplete.";
            }
            if (receiver.CommittedVolumeM3 <= 0d)
                return title + "Native stock present; deliver an aliquot into the receiver using accepted commands.";
            if (native.Observations == null ||
                !native.Observations.Any(item => item.VesselId == "receiver" &&
                    item.ObservableId == "pH" && item.Support == "Supported" &&
                    item.Freshness == "Current"))
                return title + "Material in receiver (Native rev " + receiver.MaterialRevision +
                    "). Inspect pH; await a supported Current observation, then Save/Review.";
            var ph = native.Observations.First(item => item.VesselId == "receiver" &&
                item.ObservableId == "pH" && item.Support == "Supported" &&
                item.Freshness == "Current");
            return title + "Receiver has material (Native rev " + receiver.MaterialRevision +
                "). Native pH reports Supported/Current, maturity " + ph.Maturity +
                ". Record it in Notebook, Save, then open recorded Review. No endpoint inferred.";
        }

        private static bool IsRepresented(double volumeM3, double capacityM3)
        {
            return !double.IsNaN(volumeM3) && !double.IsInfinity(volumeM3) &&
                !double.IsNaN(capacityM3) && !double.IsInfinity(capacityM3) &&
                capacityM3 > 0d && volumeM3 >= 0d &&
                volumeM3 <= capacityM3 + 1e-10;
        }
    }
}
