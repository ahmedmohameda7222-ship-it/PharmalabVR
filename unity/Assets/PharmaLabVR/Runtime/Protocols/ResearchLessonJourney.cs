using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PharmaLabVR.Lab;
using PharmaLabVR.UI;
using PharmaLabVR.Session;

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

        // Pure reflection against the student's own numeric criterion.
        // The Native observation gate is reused; it never generates or
        // adjusts scientific values, grades or experiment endpoints.
        public static string CompareRecordedPrediction(string choice, LabViewModel native,
            string vesselId, MeasurementAvailability availability, string displayedNativePH)
        {
            if (!IsSupportedPrediction(choice) || vesselId != "receiver")
                throw new InvalidOperationException("A receiver pH hypothesis is required.");
            NativeMeasurementNote.Build(native, vesselId, availability, displayedNativePH);
            // Two Native values on opposite sides of seven can BOTH render
            // as 7.00. The learning criterion must use the unrounded value
            // from the exact revision-matched Native observation, not UI text.
            var raw = native.Observations.First(item => item.VesselId == vesselId &&
                item.ObservableId == "pH").Value;
            var belowSeven = raw < 7d;
            var predictedBelow = choice == "below-7";
            return "Student prediction criterion: receiver pH " +
                (predictedBelow ? "below 7" : "at least 7") +
                ". Observed current Native pH " + raw.ToString("R", CultureInfo.InvariantCulture) +
                " (display " + displayedNativePH + ") is " +
                (belowSeven ? "below 7" : "at least 7") +
                "; criterion " + (predictedBelow == belowSeven ? "matched" : "not matched") +
                ". Research reflection only; not a grade or endpoint.";
        }

        // A learner selects one of three *prompts* after saving a current
        // Native receiver measurement. The selected statement is not a
        // scientific computation, model conclusion, or academic grade.
        public static bool IsSupportedExplanation(string choice) =>
            choice == "composition-hypothesis" ||
            choice == "method-uncertainty" ||
            choice == "model-limitations";

        public static string BuildRecordedExplanation(string lessonId, string choice,
            LabViewModel native, string vesselId,
            MeasurementAvailability availability, string displayedNativePH)
        {
            if (string.IsNullOrWhiteSpace(lessonId) ||
                !IsSupportedExplanation(choice) || vesselId != "receiver")
                throw new InvalidOperationException("Choose an authored learner explanation for a research lesson.");
            var verified = NativeMeasurementNote.Build(
                native, vesselId, availability, displayedNativePH);
            var statement = choice switch
            {
                "composition-hypothesis" =>
                    "I hypothesize that prepared stock composition affects the observed pH; a controlled repeat could test this.",
                "method-uncertainty" =>
                    "I would inspect retained pipette volume and accepted transfer receipts before attributing differences to chemistry.",
                _ =>
                    "I would check model support, observation freshness and database identity before interpreting this pH."
            };
            return "Student explanation for " + lessonId +
                " at Native event " + native.EventSequence + " · " + choice +
                ": " + statement + " Selected by learner; not Native scientific evidence." +
                " Observation reference: " + verified;
        }

        public static bool HasRecordedExplanation(IReadOnlyList<NotebookEntry> entries,
            string lessonId, string branchId, string eventSequence)
        {
            if (entries == null || string.IsNullOrWhiteSpace(lessonId) ||
                string.IsNullOrWhiteSpace(branchId) || string.IsNullOrWhiteSpace(eventSequence))
                return false;
            var prefix = "Student explanation for " + lessonId +
                " at Native event " + eventSequence + " · ";
            return entries.Any(entry => entry != null &&
                entry.BranchId == branchId && entry.EventSequence == eventSequence &&
                entry.VesselId == "receiver" && entry.Text != null &&
                entry.Text.StartsWith(prefix, StringComparison.Ordinal));
        }

        // The first durable hypothesis for the same local Native branch and
        // lesson survives a scene/controller rebuild. Annotation only; never
        // infer chemistry or import hypotheses across independent branches.
        public static string RecoverSavedPrediction(IReadOnlyList<NotebookEntry> entries,
            string lessonId, string nativeBranchId, string currentNativeEventSequence)
        {
            if (entries == null || string.IsNullOrWhiteSpace(lessonId) ||
                string.IsNullOrWhiteSpace(nativeBranchId) ||
                !ulong.TryParse(currentNativeEventSequence, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var currentEvent) ||
                currentEvent.ToString(CultureInfo.InvariantCulture) != currentNativeEventSequence)
                return null;
            var prefix = "Student hypothesis for " + lessonId +
                ": receiver pH predicted ";
            const string suffix =
                ". This is a learner prediction, not a Native result or certified endpoint.";
            foreach (var entry in entries)
            {
                // Restoring event N on the *same* branch must not use a
                // prediction from event N+1 or from a malformed annotation.
                if (entry == null || entry.BranchId != nativeBranchId ||
                    entry.VesselId != "receiver" || entry.Text == null ||
                    !ulong.TryParse(entry.EventSequence, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var authoredEvent) ||
                    authoredEvent > currentEvent ||
                    authoredEvent.ToString(CultureInfo.InvariantCulture) != entry.EventSequence ||
                    !entry.Text.StartsWith(prefix, StringComparison.Ordinal) ||
                    !entry.Text.EndsWith(suffix, StringComparison.Ordinal))
                    continue;
                var answer = entry.Text.Substring(prefix.Length,
                    entry.Text.Length - prefix.Length - suffix.Length);
                if (answer == "below 7") return "below-7";
                if (answer == "at least 7") return "at-least-7";
            }
            return null;
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

        // Guided, ungraded progress. State comes only from the current Native
        // snapshot plus branch-matched durable local student annotations.
        public string EvidenceChecklist(string liveBranchId, LabViewModel native,
            IReadOnlyList<NotebookEntry> notes)
        {
            const string disclaimer = "No certified grade or endpoint inferred.";
            if (string.IsNullOrWhiteSpace(liveBranchId) || liveBranchId != BranchId)
                return "Research checklist unavailable for a different Native branch. " + disclaimer;
            if (native?.Vessels == null || string.IsNullOrWhiteSpace(native.EventSequence))
                return "Research checklist awaits a Native snapshot. " + disclaimer;
            var sourceAvailable = native.Vessels.Any(v => v.Id == "source" &&
                IsRepresented(v.CommittedVolumeM3, v.CapacityM3) &&
                v.CommittedVolumeM3 > 0d && v.MaterialRevision != "0");
            var receiverExists = native.Vessels.Any(v => v.Id == "receiver" &&
                IsRepresented(v.CommittedVolumeM3, v.CapacityM3));
            var receiver = receiverExists ?
                native.Vessels.First(v => v.Id == "receiver") : default;
            var transferred = receiverExists && receiver.CommittedVolumeM3 > 0d;
            var nativePH = transferred && native.Observations != null &&
                native.Observations.Any(o => o.VesselId == "receiver" &&
                    o.ObservableId == "pH" &&
                    ScientificReadoutPolicy.Resolve(o.Support, o.ComputationState,
                        o.Freshness, o.Value, o.ValueAvailable) ==
                        MeasurementAvailability.Current &&
                    o.AsOfMaterialRevision == receiver.MaterialRevision &&
                    !string.IsNullOrWhiteSpace(o.ModelId) &&
                    !string.IsNullOrWhiteSpace(o.DatabaseIdentity) &&
                    !string.IsNullOrWhiteSpace(o.Maturity));
            var notesForBranch = notes ?? Array.Empty<NotebookEntry>();
            var predicted = RecoverSavedPrediction(
                notesForBranch, Definition.id, BranchId, native.EventSequence) != null;
            var recorded = nativePH && NativeMeasurementNote.WasRecorded(
                notesForBranch, BranchId, native.EventSequence, "receiver",
                receiver.MaterialRevision);
            var reflected = recorded && HasRecordedExplanation(
                notesForBranch, Definition.id, BranchId, native.EventSequence);
            var checklist =
                (sourceAvailable ? "Observed: prepared Native stock" :
                    "Pending: prepare Native stock") +
                "\n" + (predicted ? "Recorded: student hypothesis" :
                    "Pending: student hypothesis") +
                "\n" + (transferred ? "Observed: Native receiver transfer" :
                    "Pending: Native receiver transfer") +
                "\n" + (nativePH ? "Observed: supported current Native pH" :
                    "Pending: supported current Native pH") +
                "\n" + (recorded ? "Recorded: Native pH" :
                    "Pending: recorded Native pH") +
                "\n" + (reflected ? "Recorded: learner reflection" :
                    "Pending: learner reflection");
            if (Definition.id == "mixed-acid-titration-research")
            {
                var reserveReady = native.Vessels.Any(v => v.Id == "reserve-stock" &&
                    IsRepresented(v.CommittedVolumeM3, v.CapacityM3) &&
                    v.CommittedVolumeM3 > 0d && v.MaterialRevision != "0");
                checklist += "\n" + (reserveReady ? "Observed: prepared reserve stock" :
                    "Pending: prepare reserve stock");
            }
            return checklist + "\n" + disclaimer;
        }

        // XR receives a single factual next step, never the full desktop
        // checklist overlapping its physical input targets.
        public string CompactEvidenceStatus(string liveBranchId, LabViewModel native,
            IReadOnlyList<NotebookEntry> notes)
        {
            if (liveBranchId != BranchId || native?.Vessels == null)
                return "Research evidence unavailable for this Native branch (not graded).";
            var lines = EvidenceChecklist(liveBranchId, native, notes)
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var total = lines.Count(line => line.StartsWith("Pending: ", StringComparison.Ordinal) ||
                line.StartsWith("Recorded: ", StringComparison.Ordinal) ||
                line.StartsWith("Observed: ", StringComparison.Ordinal));
            var completed = lines.Count(line => line.StartsWith("Recorded: ", StringComparison.Ordinal) ||
                line.StartsWith("Observed: ", StringComparison.Ordinal));
            var pending = lines.FirstOrDefault(line =>
                line.StartsWith("Pending: ", StringComparison.Ordinal));
            var next = pending == null ? "Save and open recorded Review" :
                pending.Substring("Pending: ".Length);
            return "Study steps documented " + completed + "/" + total +
                " (not graded). Next: " + next + ".";
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
            // The lesson narrative must use the same Native availability
            // gate as the actual readout and Notebook. Supported/Current labels
            // alone are insufficient while IPhreeqc is Pending or the material
            // revision has changed.
            var current = native.Observations?.Where(item =>
                item.VesselId == "receiver" && item.ObservableId == "pH" &&
                ScientificReadoutPolicy.Resolve(item.Support, item.ComputationState,
                    item.Freshness, item.Value, item.ValueAvailable) ==
                    MeasurementAvailability.Current &&
                item.AsOfMaterialRevision == receiver.MaterialRevision &&
                !string.IsNullOrWhiteSpace(item.ModelId) &&
                !string.IsNullOrWhiteSpace(item.DatabaseIdentity) &&
                !string.IsNullOrWhiteSpace(item.Maturity)).ToArray();
            if (current == null || current.Length == 0)
                return title + "Material in receiver (Native rev " + receiver.MaterialRevision +
                    "). Inspect pH; await a supported Current observation, then Save/Review.";
            var ph = current[0];
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
