using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PharmaLabVR.Session
{
    // A local teacher annotation is not Native science, a grade, student
    // evidence, or academic attestation. Bind to BOTH saved payloads.
    public static class InstructorFeedbackService
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static string Fingerprint(string nativeJson, string studentJournal)
        {
            if (string.IsNullOrEmpty(nativeJson) || string.IsNullOrEmpty(studentJournal))
                throw new InvalidDataException("A complete portable study is required.");
            var raw = "PLV-FORMATIVE-STUDY-V1\n" +
                Utf8.GetByteCount(nativeJson).ToString(CultureInfo.InvariantCulture) + ":" +
                nativeJson + "\n" +
                Utf8.GetByteCount(studentJournal).ToString(CultureInfo.InvariantCulture) + ":" +
                studentJournal;
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Utf8.GetBytes(raw)))
                    .Replace("-", "").ToLowerInvariant();
        }

        public static string FeedbackPath(string directory, string fingerprint)
        {
            if (string.IsNullOrWhiteSpace(directory) || fingerprint == null ||
                fingerprint.Length != 64 || fingerprint.Any(c =>
                    (c < '0' || c > '9') && (c < 'a' || c > 'f')))
                throw new ArgumentException("Validated local study fingerprint required.");
            return Path.Combine(directory, "instructor-feedback-" + fingerprint + ".plv-notes");
        }

        private static string Label(string choice)
        {
            switch (choice)
            {
                case "request-evidence": return "Request another recorded Native measurement.";
                case "check-method": return "Review the instrument method and uncertainty.";
                case "compare-hypothesis": return "Compare this observation to the original student hypothesis.";
                default: throw new ArgumentException("Unsupported formative feedback choice.", nameof(choice));
            }
        }

        private static string NoteDigest(NotebookEntry note)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Utf8.GetBytes(
                    note.BranchId + "\n" + note.EventSequence + "\n" +
                    note.VesselId + "\n" + note.RecordedAtUtc + "\n" + note.Text)))
                    .Replace("-", "").ToLowerInvariant();
        }

        private static string FormativeAnnotation(string fingerprint,
            NotebookEntry studentNote, int zeroBasedIndex, string choice)
        {
            return "LOCAL INSTRUCTOR FORMATIVE FEEDBACK — NOT GRADED OR ATTESTED" +
                "\nStudy fingerprint: " + fingerprint +
                "\nStudent note #" + (zeroBasedIndex + 1).ToString(CultureInfo.InvariantCulture) +
                " · event " + studentNote.EventSequence +
                " · SHA256 " + NoteDigest(studentNote) +
                "\n" + Label(choice);
        }

        // The learner may inspect local sidecar feedback only for the exact
        // checksum-validated exported .plvpack snapshot. The teacher sidecar
        // is a separate, UNSIGNED local annotation, never a grade, identity,
        // Native observation or permission to mutate the student notebook.
        // Every displayed line must match a saved student note digest and
        // one of the three supported formative choices. Unrecognized rows
        // fail closed, even when their local journal checksum is valid.
        public static string StudentSummary(NotebookService journal, string directory,
            string fingerprint, string branchId, string savedEventSequence,
            IReadOnlyList<NotebookEntry> studentNotes)
        {
            if (journal == null || studentNotes == null ||
                string.IsNullOrWhiteSpace(branchId) ||
                !ulong.TryParse(savedEventSequence, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var ceiling) ||
                savedEventSequence != ceiling.ToString(CultureInfo.InvariantCulture))
                throw new InvalidDataException("Validated student study checkpoint required.");
            var feedback = journal.ReadCurrent(
                FeedbackPath(directory, fingerprint), branchId, savedEventSequence);
            if (feedback.Count == 0)
                return "No local instructor formative feedback for this exact exported study. " +
                    "Copy instructor-feedback-" + fingerprint + ".plv-notes from the " +
                    "instructor into this device's local sessions folder if supplied. " +
                    "This sidecar is unsigned, ungraded and not an identity attestation.";
            var labels = new StringBuilder(
                "Local formative feedback (not graded/attested; no identity verification).");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in feedback)
            {
                if (entry == null || entry.RecordKind != NotebookRecordKind.User ||
                    entry.BranchId != branchId ||
                    entry.EventSequence != savedEventSequence)
                    throw new InvalidDataException(
                        "Instructor feedback is not bound to the exported Native checkpoint.");
                var matched = false;
                for (var i = 0; i < studentNotes.Count && !matched; i++)
                {
                    var student = studentNotes[i];
                    if (student == null || student.BranchId != branchId ||
                        !ulong.TryParse(student.EventSequence, NumberStyles.None,
                            CultureInfo.InvariantCulture, out var eventId) ||
                        eventId > ceiling || student.VesselId != entry.VesselId)
                        continue;
                    foreach (var choice in new[] {
                        "request-evidence", "check-method", "compare-hypothesis" })
                    {
                        if (!string.Equals(entry.Text, FormativeAnnotation(
                                fingerprint, student, i, choice), StringComparison.Ordinal))
                            continue;
                        if (!seen.Add(entry.Text))
                            throw new InvalidDataException(
                                "Duplicate instructor feedback in the local sidecar.");
                        labels.Append("\nStudent note #")
                            .Append((i + 1).ToString(CultureInfo.InvariantCulture))
                            .Append(": ").Append(Label(choice));
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                    throw new InvalidDataException(
                        "Instructor response is not bound to an exported student note.");
            }
            return labels.ToString();
        }

        public static NotebookEntry Record(NotebookService journal, string directory,
            string fingerprint, string branchId, string savedEventSequence,
            NotebookEntry studentNote, int zeroBasedIndex, string choice)
        {
            if (journal == null || studentNote == null || zeroBasedIndex < 0)
                throw new ArgumentException("Select an actual recorded learner note.");
            Label(choice);
            if (studentNote.BranchId != branchId ||
                !ulong.TryParse(savedEventSequence, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var savedEvent) ||
                !ulong.TryParse(studentNote.EventSequence, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var noteEvent) ||
                noteEvent > savedEvent)
                throw new InvalidDataException(
                    "The student note is not within this Native branch/event checkpoint.");
            var path = FeedbackPath(directory, fingerprint);
            var annotation = FormativeAnnotation(
                fingerprint, studentNote, zeroBasedIndex, choice);
            // One lock-held read/duplicate-check/atomic-write transaction.
            // A separate ReadCurrent + Append raced concurrent XR presses.
            // The feedback ledger remains separate from student Notebook.
            return journal.AppendOnce(path, branchId, savedEventSequence,
                studentNote.VesselId, annotation);
        }

        public static string Summary(NotebookService journal, string directory,
            string fingerprint, string branchId, string savedEventSequence)
        {
            if (journal == null) throw new ArgumentNullException(nameof(journal));
            var entries = journal.ReadCurrent(FeedbackPath(directory, fingerprint),
                branchId, savedEventSequence);
            if (entries.Count == 0)
                return "Local instructor feedback: none for this exact study snapshot.";
            return "Local instructor feedback: " +
                entries.Count.ToString(CultureInfo.InvariantCulture) +
                " formative response(s) for this snapshot (not graded/attested).\n" +
                entries[entries.Count - 1].Text;
        }
    }
}
