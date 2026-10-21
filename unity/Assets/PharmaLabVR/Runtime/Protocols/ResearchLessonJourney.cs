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
            var source = native.Vessels.FirstOrDefault(v => v.Id == "source");
            var receiver = native.Vessels.FirstOrDefault(v => v.Id == "receiver");
            if (string.IsNullOrEmpty(source.Id) || !IsRepresented(source))
                return title + "Prepare supported Native stock in Setup.";
            if (string.IsNullOrEmpty(receiver.Id) || !IsRepresented(receiver))
                return title + "Native receiver unavailable; no delivery evidence.";
            if (Definition.id == "mixed-acid-titration-research")
            {
                var reserve = native.Vessels.FirstOrDefault(v => v.Id == "reserve-stock");
                if (string.IsNullOrEmpty(reserve.Id) || !IsRepresented(reserve) ||
                    reserve.CommittedVolumeM3 <= 0d)
                    return title + "Secondary prepared Native stock missing. This mixed-acid procedure is incomplete.";
            }
            if (receiver.CommittedVolumeM3 <= 0d)
                return title + "Native stock present; deliver an aliquot into the receiver using accepted commands.";
            var ph = native.Observations?.FirstOrDefault(item =>
                item.VesselId == "receiver" && item.ObservableId == "pH");
            if (ph == null || ph.Support != "Supported" || ph.Freshness != "Current")
                return title + "Material in receiver (Native rev " + receiver.MaterialRevision +
                    "). Inspect pH; await a supported Current observation, then Save/Review.";
            return title + "Receiver has material (Native rev " + receiver.MaterialRevision +
                "). Native pH reports Supported/Current, maturity " + ph.Maturity +
                ". Record it in Notebook, Save, then open recorded Review. No endpoint inferred.";
        }

        private static bool IsRepresented(LabViewModel.VesselView vessel)
        {
            return !double.IsNaN(vessel.CommittedVolumeM3) &&
                !double.IsInfinity(vessel.CommittedVolumeM3) &&
                !double.IsNaN(vessel.CapacityM3) &&
                !double.IsInfinity(vessel.CapacityM3) &&
                vessel.CapacityM3 > 0d && vessel.CommittedVolumeM3 >= 0d &&
                vessel.CommittedVolumeM3 <= vessel.CapacityM3 + 1e-10;
        }
    }
}
