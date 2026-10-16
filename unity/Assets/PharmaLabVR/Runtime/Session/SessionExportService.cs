using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PharmaLabVR.Session
{
    // Export-only recorded Native data. No solver calls or recomputation.
    // A blank value is deliberate for stale, failed or unsupported observations.
    public sealed class SessionExportService
    {
        private const int MaximumCsvBytes = 24 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static string RenderCsv(RecordedSessionReview review)
        {
            if (review == null) throw new ArgumentNullException(nameof(review));
            var buffer = new StringBuilder();
            buffer.Append("schemaVersion,recordKind,branchId,commandSequence,vesselId,observableId,unit,");
            buffer.Append("support,freshness,computationState,maturity,modelId,databaseIdentity,");
            buffer.Append("materialRevision,commandType,accepted,outcomeCode,value\n");
            foreach (var observation in review.Observations)
            {
                var current = observation.valueAvailable &&
                    observation.support == "Supported" &&
                    observation.freshness == "Current" &&
                    observation.computationState == "Ready" &&
                    double.IsFinite(observation.value);
                AppendRow(buffer, "1", "observation", review.BranchId, "",
                    observation.vesselId, observation.observableId, "pH",
                    observation.support, observation.freshness, observation.computationState,
                    observation.maturity, observation.modelId, observation.databaseIdentity,
                    observation.asOfMaterialRevision, "", "", "",
                    current ? observation.value.ToString("R", CultureInfo.InvariantCulture) : "");
            }
            foreach (var receipt in review.CommandReceipts)
                AppendRow(buffer, "1", "command", review.BranchId, receipt.CommandSequence,
                    "", "", "", "", "", "", "", "", "", "",
                    receipt.CommandType, receipt.Accepted ? "true" : "false", receipt.OutcomeCode, "");
            if (Utf8.GetByteCount(buffer.ToString()) > MaximumCsvBytes)
                throw new InvalidDataException("Recorded CSV exceeds its export limit.");
            return buffer.ToString();
        }

        public static void WriteCsv(string path, RecordedSessionReview review)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A CSV destination path is required.", nameof(path));
            var content = Utf8.GetBytes(RenderCsv(review));
            var target = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(target);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidDataException("A CSV parent directory is required.");
            Directory.CreateDirectory(directory);
            var temporary = target + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(content, 0, content.Length);
                    stream.Flush(true);
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

        private static void AppendRow(StringBuilder output, params string[] columns)
        {
            if (columns.Length != 18) throw new InvalidOperationException("CSV column count changed.");
            for (var index = 0; index < columns.Length; index++)
            {
                if (index != 0) output.Append(',');
                output.Append(Escape(columns[index] ?? string.Empty));
            }
            output.Append('\n');
        }

        private static string Escape(string text)
        {
            // Spreadsheet programs can interpret untrusted metadata as formulas.
            // Prefix unsafe leading characters, then quote RFC-4180 separators.
            if (text.Length > 0 && (text[0] == '=' || text[0] == '+' ||
                                    text[0] == '-' || text[0] == '@' ||
                                    text[0] == '\t' || text[0] == '\r'))
                text = "'" + text;
            if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            return text;
        }
    }
}
