using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PharmaLabVR.Session
{
    // Offline formative preview only. Checksums detect accidental corruption,
    // not identity, course enrollment, server attestation or grading authority.
    // Reading never imports the archive into a live Native CoreSession.
    public sealed class InstructorPortableReview
    {
        public string NativeBranchId { get; }
        public string NativeEventSequence { get; }
        public string ScientificModelIdentity { get; }
        public IReadOnlyList<RecordedSessionReview.RecordedCommandReceipt> NativeReceipts { get; }
        public IReadOnlyList<RecordedSessionReview.RecordedObservation> NativeObservations { get; }
        public IReadOnlyList<NotebookEntry> StudentAnnotations { get; }

        private InstructorPortableReview(PortableSessionData source,
            RecordedSessionReview recorded, IReadOnlyList<NotebookEntry> notes)
        {
            NativeBranchId = source.BranchId;
            NativeEventSequence = source.EventSequence;
            ScientificModelIdentity = recorded.ModelIdentity;
            NativeReceipts = recorded.CommandReceipts;
            NativeObservations = Array.AsReadOnly(recorded.Observations);
            StudentAnnotations = notes;
        }

        public static InstructorPortableReview Open(string archivePath)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("Local portable archive is required.", nameof(archivePath));
            var source = PortableSessionArchive.Read(archivePath);
            var recorded = RecordedSessionReview.FromJson(source.NativeJson);
            if (!string.Equals(recorded.BranchId, source.BranchId, StringComparison.Ordinal))
                throw new InvalidDataException("Portable Native record and review branch disagree.");
            var notes = NotebookService.ReadPortableBranch(
                source.NotebookSnapshot, source.BranchId, source.EventSequence);
            return new InstructorPortableReview(source, recorded, notes);
        }

        public string Summary
        {
            get
            {
                var text = new StringBuilder(
                    "OFFLINE INSTRUCTOR PREVIEW — NOT ATTESTED OR GRADED.\n");
                text.Append("Native branch ").Append(NativeBranchId)
                    .Append(" · event ").Append(NativeEventSequence)
                    .Append("\nScientific model ").Append(
                        string.IsNullOrEmpty(ScientificModelIdentity) ?
                        "unavailable" : ScientificModelIdentity)
                    .Append("\nRecorded Native command receipts ").Append(NativeReceipts.Count)
                    .Append("; observations ").Append(NativeObservations.Count)
                    .Append("; learner annotations ").Append(StudentAnnotations.Count);
                foreach (var observation in NativeObservations)
                {
                    var display = observation.DisplayValue;
                    text.Append("\n").Append(InstructorObservationLabel.Render(
                        observation.vesselId, display, observation.value,
                        display != "--", observation.asOfMaterialRevision))
                        .Append(" · ").Append(observation.support)
                        .Append('/').Append(observation.freshness);
                }
                text.Append("\n").Append(
                    ResearchLearningReview.Render(StudentAnnotations, NativeBranchId, true));
                text.Append("\nChecksum integrity is not evidence of a student's identity, " +
                    "institutional approval, or scientific attestation.");
                return text.ToString();
            }
        }
    }
}
