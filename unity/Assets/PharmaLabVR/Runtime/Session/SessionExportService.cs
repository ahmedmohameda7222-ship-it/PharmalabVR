using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PharmaLabVR.Session
{
    // An export's two CSVs share a recoverable publication record.
    // Ordinary I/O failure restores both previous names; after a process
    // exit between publishes the next explicit recovery/export rolls back
    // to the last confirmed pair. This is not a guarantee against power loss
    // before filesystem directory metadata becomes durable.
    public static class RecordedCsvPairWriter
    {
        private const string JournalHeader = "PLV-CSV-PAIR-V1";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static void WritePair(string scientificPath, string scientificCsv,
            string learnerPath, string learnerCsv) =>
            WritePair(scientificPath, scientificCsv, learnerPath, learnerCsv,
                null, null);

        // Test-only callbacks allow forced rollback errors and a simulated
        // abrupt process exit immediately after the first published file.
        internal static void WritePair(string scientificPath, string scientificCsv,
            string learnerPath, string learnerCsv, Action beforeRollback) =>
            WritePair(scientificPath, scientificCsv, learnerPath, learnerCsv,
                beforeRollback, null);

        internal static void WritePair(string scientificPath, string scientificCsv,
            string learnerPath, string learnerCsv, Action beforeRollback,
            Action afterFirstPublish)
        {
            var pair = ValidatePaths(scientificPath, learnerPath);
            if (scientificCsv == null || learnerCsv == null)
                throw new ArgumentException("Both CSV contents are required.");
            var scientificBytes = Utf8.GetBytes(scientificCsv);
            var learnerBytes = Utf8.GetBytes(learnerCsv);
            if (scientificBytes.Length == 0 ||
                scientificBytes.Length > 24 * 1024 * 1024 ||
                learnerBytes.Length == 0 ||
                learnerBytes.Length > 8 * 1024 * 1024)
                throw new InvalidDataException("CSV contents missing or oversized.");

            Directory.CreateDirectory(pair.Directory);
            using var lease = AcquireLease(pair.Scientific);
            // Recover an interrupted prior export before publishing a new
            // one; never mix old annotations with a different Native export.
            RecoverUnderLease(pair);
            var token = Guid.NewGuid().ToString("N");
            var scientificStage = Artifact(pair.Scientific, token, ".stage");
            var learnerStage = Artifact(pair.Learner, token, ".stage");
            var scientificBackup = Artifact(pair.Scientific, token, ".rollback");
            var learnerBackup = Artifact(pair.Learner, token, ".rollback");
            var scientificExisted = File.Exists(pair.Scientific);
            var learnerExisted = File.Exists(pair.Learner);
            try
            {
                Stage(scientificStage, scientificBytes);
                Stage(learnerStage, learnerBytes);
                if (scientificExisted) File.Copy(pair.Scientific, scientificBackup);
                if (learnerExisted) File.Copy(pair.Learner, learnerBackup);
                var journal = MakeJournal(pair, token, scientificExisted,
                    learnerExisted, scientificBackup, learnerBackup);
                // The journal is published only after both backup copies
                // are finished and hashed. Its existence means that
                // interrupted publication MUST be reconciled first.
                var pendingStage = pair.Pending + "." + token + ".stage";
                try
                {
                    Stage(pendingStage, Utf8.GetBytes(journal));
                    File.Move(pendingStage, pair.Pending);
                }
                finally { Cleanup(pendingStage); }

                Publish(scientificStage, pair.Scientific, scientificExisted);
                afterFirstPublish?.Invoke();
                Publish(learnerStage, pair.Learner, learnerExisted);
                // No caller is told the pair is committed before this point.
                File.Delete(pair.Pending);
            }
            catch (Exception original) when (original is IOException ||
                                             original is UnauthorizedAccessException)
            {
                try
                {
                    beforeRollback?.Invoke();
                    RecoverUnderLease(pair);
                }
                catch (Exception failure) when (failure is IOException ||
                                               failure is UnauthorizedAccessException ||
                                               failure is InvalidDataException)
                {
                    // Do not delete the recovery journal/backups on a
                    // double-fault; an operator can still inspect them.
                    throw new IOException("Paired CSV publish and recovery both failed; " +
                        "the journal and original snapshots were retained.",
                        new AggregateException(original, failure));
                }
                throw new IOException("Paired CSV failed; previous files restored.", original);
            }
            finally
            {
                Cleanup(scientificStage);
                Cleanup(learnerStage);
                // If the durable journal still exists, its original backup
                // copies are required after a crash or failed rollback.
                if (!File.Exists(pair.Pending))
                {
                    Cleanup(scientificBackup);
                    Cleanup(learnerBackup);
                }
            }
        }

        // The host can invoke this after relaunch, before displaying exports.
        // False means no interrupted transaction was present.
        public static bool RecoverInterruptedPair(string scientificPath,
            string learnerPath)
        {
            var pair = ValidatePaths(scientificPath, learnerPath);
            Directory.CreateDirectory(pair.Directory);
            using var lease = AcquireLease(pair.Scientific);
            return RecoverUnderLease(pair);
        }

        private sealed class CsvPaths
        {
            public string Scientific;
            public string Learner;
            public string Directory;
            public string Pending;
        }

        private static CsvPaths ValidatePaths(string scientificPath, string learnerPath)
        {
            if (string.IsNullOrWhiteSpace(scientificPath) ||
                string.IsNullOrWhiteSpace(learnerPath))
                throw new ArgumentException("Two CSV destinations are required.");
            var scientific = Path.GetFullPath(scientificPath);
            var learner = Path.GetFullPath(learnerPath);
            var directory = Path.GetDirectoryName(scientific);
            if (string.IsNullOrWhiteSpace(directory) ||
                string.Equals(scientific, learner, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(directory, Path.GetDirectoryName(learner),
                    StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "The two CSV destinations must differ and share a directory.");
            return new CsvPaths { Scientific = scientific, Learner = learner,
                Directory = directory, Pending = scientific + ".pair.pending" };
        }

        private static FileStream AcquireLease(string scientific) =>
            new FileStream(scientific + ".pair.lock", FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);

        private static string Artifact(string path, string token, string suffix) =>
            path + "." + token + suffix;

        private static string DigestBytes(string value)
        {
            using var hash = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Utf8.GetBytes(value)))
                .Replace("-", "").ToLowerInvariant();
        }

        private static string DigestFile(string path)
        {
            using var hash = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(hash.ComputeHash(stream))
                .Replace("-", "").ToLowerInvariant();
        }

        private static string PairIdentity(CsvPaths pair) =>
            DigestBytes(pair.Scientific + "\n" + pair.Learner);

        private static string MakeJournal(CsvPaths pair, string token,
            bool scientificExisted, bool learnerExisted,
            string scientificBackup, string learnerBackup) =>
            JournalHeader + "\n" + token + "\n" +
            (scientificExisted ? "1" : "0") +
            (learnerExisted ? "1" : "0") + "\n" +
            PairIdentity(pair) + "\n" +
            (scientificExisted ? DigestFile(scientificBackup) : "-") + "\n" +
            (learnerExisted ? DigestFile(learnerBackup) : "-") + "\n";

        private static bool IsHex(string value, int length)
        {
            if (value == null || value.Length != length) return false;
            foreach (var c in value)
                if (!((c >= '0' && c <= '9') ||
                      (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        private static bool RecoverUnderLease(CsvPaths pair)
        {
            if (!File.Exists(pair.Pending)) return false;
            // A malformed marker cannot authorize deleting either CSV.
            var record = File.ReadAllText(pair.Pending, Utf8).Split('\n');
            if (record.Length != 7 || record[0] != JournalHeader ||
                !IsHex(record[1], 32) ||
                (record[2] != "00" && record[2] != "01" &&
                 record[2] != "10" && record[2] != "11") ||
                record[3] != PairIdentity(pair) ||
                (record[2][0] == '1' ? !IsHex(record[4], 64) : record[4] != "-") ||
                (record[2][1] == '1' ? !IsHex(record[5], 64) : record[5] != "-") ||
                record[6] != "")
                throw new InvalidDataException("Paired CSV recovery journal is malformed.");

            var scienceExisted = record[2][0] == '1';
            var learnerExisted = record[2][1] == '1';
            var scienceBackup = Artifact(pair.Scientific, record[1], ".rollback");
            var learnerBackup = Artifact(pair.Learner, record[1], ".rollback");
            // Check BOTH confirmed preimages before mutating EITHER file.
            if ((scienceExisted && (!File.Exists(scienceBackup) ||
                 DigestFile(scienceBackup) != record[4])) ||
                (learnerExisted && (!File.Exists(learnerBackup) ||
                 DigestFile(learnerBackup) != record[5])))
                throw new InvalidDataException(
                    "Paired CSV recovery snapshot is missing or altered.");
            // A pre-existing directory at a NEW-file destination is the
            // expected filesystem blocker in the second-publish failure
            // test. It is not ours to remove. Only a destination whose
            // confirmed preimage was a FILE must be writable as a file.
            if ((scienceExisted && Directory.Exists(pair.Scientific)) ||
                (learnerExisted && Directory.Exists(pair.Learner)))
                throw new IOException("An existing CSV's recovery destination is a directory.");

            RestoreVerified(pair.Scientific, scienceBackup, scienceExisted);
            RestoreVerified(pair.Learner, learnerBackup, learnerExisted);
            File.Delete(pair.Pending);
            Cleanup(scienceBackup);
            Cleanup(learnerBackup);
            return true;
        }

        private static void RestoreVerified(string path, string backup, bool existed)
        {
            if (!existed)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            var stage = path + ".recovery.tmp";
            try
            {
                File.Copy(backup, stage, true);
                if (File.Exists(path))
                    File.Replace(stage, path, null, true);
                else
                    File.Move(stage, path);
            }
            finally { Cleanup(stage); }
        }

        private static void Stage(string path, byte[] bytes)
        {
            using var file = new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            file.Write(bytes, 0, bytes.Length);
            file.Flush(true);
        }

        private static void Publish(string stage, string path, bool existed)
        {
            if (existed) File.Replace(stage, path, null, true);
            else File.Move(stage, path);
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
