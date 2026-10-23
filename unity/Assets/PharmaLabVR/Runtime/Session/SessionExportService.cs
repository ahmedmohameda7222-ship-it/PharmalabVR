using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PharmaLabVR.Session
{
    // Stages both files before publication and rolls both back on ordinary I/O
    // errors. Two separate file entries cannot be power-loss atomic.
    public static class RecordedCsvPairWriter
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static void WritePair(string scientificPath, string scientificCsv,
            string learnerPath, string learnerCsv)
        {
            if (string.IsNullOrWhiteSpace(scientificPath) ||
                string.IsNullOrWhiteSpace(learnerPath) ||
                scientificCsv == null || learnerCsv == null)
                throw new ArgumentException("Two CSV paths and contents are required.");
            var science = Path.GetFullPath(scientificPath);
            var learner = Path.GetFullPath(learnerPath);
            var directory = Path.GetDirectoryName(science);
            if (string.Equals(science, learner, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(directory) ||
                !string.Equals(directory, Path.GetDirectoryName(learner),
                    StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Paired CSV paths must differ and share a directory.");
            var scienceBytes = Utf8.GetBytes(scientificCsv);
            var learnerBytes = Utf8.GetBytes(learnerCsv);
            if (scienceBytes.Length == 0 || scienceBytes.Length > 24 * 1024 * 1024 ||
                learnerBytes.Length == 0 || learnerBytes.Length > 8 * 1024 * 1024)
                throw new InvalidDataException("CSV contents missing or oversized.");

            Directory.CreateDirectory(directory);
            // Serialize all writers targeting this scientific/student pair,
            // including different Unity instances. Retain the stable lock
            // filename across runs; unlinking a held lock permits inode races.
            using var lease = new FileStream(science + ".pair.lock",
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var token = Guid.NewGuid().ToString("N");
            var scienceStage = science + "." + token + ".stage";
            var learnerStage = learner + "." + token + ".stage";
            var scienceBackup = science + "." + token + ".rollback";
            var learnerBackup = learner + "." + token + ".rollback";
            var scienceExisted = File.Exists(science);
            var learnerExisted = File.Exists(learner);
            var touchedScience = false;
            var touchedLearner = false;
            try
            {
                Stage(scienceStage, scienceBytes);
                Stage(learnerStage, learnerBytes);
                if (scienceExisted) File.Copy(science, scienceBackup);
                if (learnerExisted) File.Copy(learner, learnerBackup);
                touchedScience = true;
                Publish(scienceStage, science, scienceExisted);
                touchedLearner = true;
                Publish(learnerStage, learner, learnerExisted);
            }
            catch (Exception error) when (error is IOException ||
                                         error is UnauthorizedAccessException)
            {
                Exception rollbackError = null;
                try
                {
                    if (touchedLearner) Restore(learner, learnerBackup, learnerExisted);
                    if (touchedScience) Restore(science, scienceBackup, scienceExisted);
                }
                catch (Exception failure) when (failure is IOException ||
                                               failure is UnauthorizedAccessException)
                {
                    rollbackError = failure;
                }
                if (rollbackError != null)
                    throw new IOException("Paired CSV publish and rollback both failed.",
                        new AggregateException(error, rollbackError));
                throw new IOException("Paired CSV failed; previous output was restored.", error);
            }
            finally
            {
                Cleanup(scienceStage);
                Cleanup(learnerStage);
                Cleanup(scienceBackup);
                Cleanup(learnerBackup);
            }
        }

        private static void Stage(string path, byte[] bytes)
        {
            using (var file = new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                file.Write(bytes, 0, bytes.Length);
                file.Flush(true);
            }
        }

        private static void Publish(string stage, string target, bool previouslyExisted)
        {
            if (previouslyExisted) File.Replace(stage, target, null, true);
            else File.Move(stage, target);
        }

        private static void Restore(string target, string backup, bool previouslyExisted)
        {
            if (previouslyExisted) File.Copy(backup, target, true);
            else if (File.Exists(target)) File.Delete(target);
        }

        private static void Cleanup(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

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
                // The final column is generated from a finite Native double
                // using invariant round-trip formatting, not untrusted copy.
                // Its sign must stay numeric; spreadsheet formula protection
                // applies only to text metadata columns.
                output.Append(index == 17 ? (columns[index] ?? string.Empty) :
                    Escape(columns[index] ?? string.Empty));
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
