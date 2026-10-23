using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PharmaLabVR.Session
{
    // Durable learner annotations are separate from authoritative Native chemistry.
    public static class ResearchLearningReview
    {
        private const int MaxExportBytes = 8 * 1024 * 1024;

        // Replay learner findings from a real checksum-validated offline
        // journal, at or before the Native checkpoint being reviewed.
        // A corrupt/missing journal may not crash Review or fabricate evidence.
        // Source branch identity must match the currently loaded Native branch.
        public static string ReadRecordedJournal(NotebookService journal,
            string filePath, string activeNativeBranchId, string recordedBranchId,
            string recordedEventSequence, bool compact)
        {
            if (journal == null || string.IsNullOrWhiteSpace(filePath) ||
                string.IsNullOrWhiteSpace(activeNativeBranchId) ||
                !string.Equals(activeNativeBranchId, recordedBranchId,
                    StringComparison.Ordinal))
                return "Local student notebook unavailable for this recorded branch.";
            try
            {
                var notes = journal.Read(filePath, recordedBranchId,
                    recordedEventSequence);
                return Render(notes, recordedBranchId, compact);
            }
            catch (Exception error) when (error is InvalidDataException ||
                                         error is IOException ||
                                         error is UnauthorizedAccessException ||
                                         error is ArgumentException)
            {
                // Do not leak unvalidated note text, paths, backup identities
                // or partial results into student/instructor Review.
                return "Local notebook unavailable: offline journal could not be verified.";
            }
        }

        // Live Notebook must distinguish genuinely absent observations from
        // an unreadable checksum-protected journal. Both its latest entry
        // and pH history are scoped to the current Native event ceiling.
        public static string ReadActiveNotebookSummary(NotebookService journal,
            string filePath, string activeBranchId, string activeEventSequence)
        {
            if (journal == null || string.IsNullOrWhiteSpace(filePath) ||
                string.IsNullOrWhiteSpace(activeBranchId))
                return "Research notebook unavailable: no active Native journal.";
            try
            {
                var entries = journal.Read(filePath, activeBranchId,
                    activeEventSequence);
                if (entries.Count == 0)
                    return "Research notebook: no recorded annotations.";
                var latest = entries[entries.Count - 1];
                var preview = latest.Text.Replace('\r', ' ').Replace('\n', ' ');
                if (preview.Length > 105)
                    preview = preview.Substring(0, 105) + "...";
                return "Research notebook · " + entries.Count +
                    " event-linked notes\n" +
                    "Latest · event " + latest.EventSequence + " · " +
                    latest.VesselId + "\n" + preview + "\n" +
                    ResearchNotebookPHHistory.Summarize(entries, activeBranchId,
                        "receiver", 3) +
                    "\nDesktop: write notes; Desktop/XR: record pH and select a research explanation.";
            }
            catch (Exception error) when (error is InvalidDataException ||
                                         error is IOException ||
                                         error is UnauthorizedAccessException ||
                                         error is ArgumentException)
            {
                return "Research notebook unavailable: offline journal could not be verified.";
            }
        }

        // Student CSV has its own schema; not certified Native results.
        public static string RenderCsv(IReadOnlyList<NotebookEntry> notes, string reviewBranchId)
        {
            if (string.IsNullOrWhiteSpace(reviewBranchId))
                throw new ArgumentException("Recorded branch is required.", nameof(reviewBranchId));
            var output = new StringBuilder(
                "schemaVersion,recordKind,branchId,eventSequence,vesselId,recordedAtUtc,annotation\n");
            foreach (var entry in notes ?? Array.Empty<NotebookEntry>())
            {
                if (entry == null || entry.BranchId != reviewBranchId) continue;
                var fields = new[] { "1", "studentAnnotation", entry.BranchId,
                    entry.EventSequence, entry.VesselId, entry.RecordedAtUtc, entry.Text };
                output.Append(string.Join(",", fields.Select(QuoteCsv))).Append('\n');
            }
            if (Encoding.UTF8.GetByteCount(output.ToString()) > MaxExportBytes)
                throw new InvalidDataException("Student annotations exceed CSV export capacity.");
            return output.ToString();
        }

        public static void WriteCsv(string path, IReadOnlyList<NotebookEntry> notes,
            string reviewBranchId)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("CSV destination required.", nameof(path));
            var bytes = new UTF8Encoding(false, true).GetBytes(RenderCsv(notes, reviewBranchId));
            var target = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(directory)) throw new InvalidDataException("No CSV parent directory.");
            Directory.CreateDirectory(directory);
            var temporary = target + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(true);
                }
                if (File.Exists(target))
                    File.Replace(temporary, target, target + ".bak", true);
                else
                    File.Move(temporary, target);
            }
            catch
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
        }

        private static string QuoteCsv(string value)
        {
            value ??= string.Empty;
            var first = value.TrimStart(' ', '\t', '\r', '\n');
            if (first.Length > 0 && "=+-@".IndexOf(first[0]) >= 0)
                value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public static string RenderPage(IReadOnlyList<NotebookEntry> notes,
            string reviewBranchId, int requestedIndex)
        {
            if (string.IsNullOrWhiteSpace(reviewBranchId))
                return "No matching Native student branch.";
            var matching = (notes ?? Array.Empty<NotebookEntry>())
                .Where(note => note != null && note.BranchId == reviewBranchId &&
                    !string.IsNullOrWhiteSpace(note.Text)).ToArray();
            if (matching.Length == 0) return "No student annotations for this recorded branch.";
            var index = Math.Max(0, Math.Min(requestedIndex, matching.Length - 1));
            var entry = matching[index];
            var statement = entry.Text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (statement.Length > 80)
                statement = statement.Substring(0, 77) + "...";
            return "Student note " + (index + 1) + "/" + matching.Length +
                " · event " + entry.EventSequence + " · " + entry.VesselId +
                "\n" + statement + " (not independently verified)";
        }

        public static string Render(IReadOnlyList<NotebookEntry> notes, string reviewBranchId,
            bool compact)
        {
            if (string.IsNullOrWhiteSpace(reviewBranchId))
                return "Local notebook unavailable: missing recorded branch.";
            var matching = (notes ?? Array.Empty<NotebookEntry>())
                .Where(item => item != null && item.BranchId == reviewBranchId &&
                    !string.IsNullOrWhiteSpace(item.Text)).ToArray();
            if (matching.Length == 0)
                return "No student annotations for this recorded branch.";
            var text = new StringBuilder("Local student notebook · ");
            text.Append(matching.Length).Append(" student annotations (not Native scientific evidence)");
            var limit = compact ? 2 : 8;
            var start = Math.Max(0, matching.Length - limit);
            for (var i = start; i < matching.Length; i++)
            {
                var entry = matching[i];
                var excerpt = entry.Text.Replace('\r', ' ').Replace('\n', ' ').Trim();
                var maxLength = compact ? 64 : 170;
                if (excerpt.Length > maxLength)
                    excerpt = excerpt.Substring(0, maxLength - 3) + "...";
                text.Append("\nevent ").Append(entry.EventSequence).Append(" · ")
                    .Append(entry.VesselId).Append(" · ").Append(excerpt);
            }
            if (start > 0) text.Append("\nOpen Notebook for earlier notes.");
            if (!compact)
            {
                var history = ResearchNotebookPHHistory.Summarize(
                    matching, reviewBranchId, "receiver", 4);
                if (!history.StartsWith("No recorded Native pH", StringComparison.Ordinal))
                    text.Append("\n").Append(history);
            }
            return text.ToString();
        }
    }
}
