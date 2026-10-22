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
            return text.ToString();
        }
    }
}
