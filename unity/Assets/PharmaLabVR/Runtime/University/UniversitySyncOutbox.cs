using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PharmaLabVR.University
{
    // Cloud metadata only. Never infer a Native material event from this DTO;
    // Native commands, measured values and science provenance stay authoritative
    // in the local C++ session and the checksum-verified Native export.
    public sealed class UniversityUploadIntent
    {
        public string InstitutionId { get; }
        public string LocalSessionId { get; }
        public string EventId { get; }
        public string NativeBranchId { get; }
        public string NativeEventSequence { get; }
        public string NativeReceiptSha256 { get; }
        public string ScientificPackageSha256 { get; }
        public string ArtifactSha256 { get; }

        public UniversityUploadIntent(string institutionId, string localSessionId,
            string eventId, string nativeBranchId, string nativeEventSequence,
            string nativeReceiptSha256, string scientificPackageSha256, string artifactSha256)
        {
            InstitutionId = institutionId;
            LocalSessionId = localSessionId;
            EventId = eventId;
            NativeBranchId = nativeBranchId;
            NativeEventSequence = nativeEventSequence;
            NativeReceiptSha256 = nativeReceiptSha256;
            ScientificPackageSha256 = scientificPackageSha256;
            ArtifactSha256 = artifactSha256;
        }
    }

    // Offline-only durable queue. No network, credentials, service key,
    // synchronization acknowledgement, auto-compaction or chemistry mutation.
    // U04 server transport MUST authorize tenant+session before attempting upload.
    // Until then all records remain explicitly Pending, even after a restart.
    public sealed class UniversitySyncOutbox
    {
        private const string Header = "PLV-UOUTBOX-V1\n";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly string path;
        private readonly int maxBytes;

        public UniversitySyncOutbox(string path, int maxBytes = 1048576)
        {
            if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\0') >= 0 ||
                !Path.IsPathRooted(path) || maxBytes < 128 || maxBytes > 16777216)
                throw new ArgumentException("A bounded absolute offline outbox path is required.");
            this.path = Path.GetFullPath(path);
            this.maxBytes = maxBytes;
        }

        // Returns true only when the new local intent has been flushed to disk.
        // False means an IDENTICAL local retry; NOT a successful cloud upload.
        public bool Enqueue(UniversityUploadIntent intent)
        {
            Validate(intent);
            // Every writer must own this cross-instance filesystem lease
            // BEFORE reading the current event set. Otherwise simultaneous
            // read-modify-replace operations silently lose one of the events.
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                    throw new InvalidDataException("Outbox has no parent directory.");
                Directory.CreateDirectory(directory);
                using (var lease = new FileStream(path + ".lock",
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    return EnqueueWithExclusiveLease(intent);
                }
            }
            catch (Exception error) when (error is IOException ||
                                           error is UnauthorizedAccessException)
            {
                throw new InvalidDataException(
                    "Another offline outbox writer holds the lease or storage is unavailable.",
                    error);
            }
        }

        private bool EnqueueWithExclusiveLease(UniversityUploadIntent intent)
        {
            var records = ReadStrict();
            foreach (var existing in records)
            {
                if (existing.InstitutionId != intent.InstitutionId ||
                    existing.LocalSessionId != intent.LocalSessionId ||
                    existing.EventId != intent.EventId) continue;
                if (Encode(existing) == Encode(intent)) return false;
                throw new InvalidDataException(
                    "Event identity conflict: immutable Native upload intent differs.");
            }
            records.Add(intent);
            var builder = new StringBuilder(Header);
            foreach (var record in records)
            {
                var payload = Encode(record);
                builder.Append(Sha256(payload)).Append('\t')
                    .Append(payload).Append('\n');
            }
            var bytes = StrictUtf8.GetBytes(builder.ToString());
            if (bytes.Length > maxBytes)
                throw new InvalidDataException("University outbox capacity exceeded.");

            var temporary = path + ".tmp";
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                    throw new InvalidDataException("University outbox has no parent directory.");
                Directory.CreateDirectory(directory);
                using (var file = new FileStream(temporary, FileMode.Create,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(true);
                }
                if (File.Exists(path))
                    File.Replace(temporary, path, path + ".bak", true);
                else
                    File.Move(temporary, path);
                return true;
            }
            catch (Exception error) when (error is IOException ||
                                           error is UnauthorizedAccessException)
            {
                // Failed staging is not an acknowledged operation. Do not
                // trust or overwrite the previous confirmed outbox.
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw new InvalidDataException("Offline intent was not durably staged.", error);
            }
        }

        // Always pending. There is deliberately no public MarkUploaded method:
        // only an authenticated, verified server-receipt reconciliation phase
        // may eventually move these events out of the local pending queue.
        public IReadOnlyList<UniversityUploadIntent> Pending() =>
            ReadStrict().AsReadOnly();

        private List<UniversityUploadIntent> ReadStrict()
        {
            var result = new List<UniversityUploadIntent>();
            if (!File.Exists(path))
            {
                if (File.Exists(path + ".bak"))
                    throw new InvalidDataException(
                        "Confirmed outbox missing; recover explicitly before new staging.");
                return result;
            }
            var info = new FileInfo(path);
            if (info.Length < Header.Length || info.Length > maxBytes)
                throw new InvalidDataException("Outbox file is empty or exceeds its bound.");
            string text;
            try { text = StrictUtf8.GetString(File.ReadAllBytes(path)); }
            catch (DecoderFallbackException error)
            {
                throw new InvalidDataException("Outbox is not valid UTF-8.", error);
            }
            if (!text.StartsWith(Header, StringComparison.Ordinal) ||
                !text.EndsWith("\n", StringComparison.Ordinal))
                throw new InvalidDataException("Outbox header or committed tail is invalid.");
            var lines = text.Split('\n');
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 1; index < lines.Length - 1; index++)
            {
                var columns = lines[index].Split('\t');
                if (columns.Length != 9)
                    throw new InvalidDataException("Outbox record field count is invalid.");
                var payload = string.Join("\t", columns, 1, 8);
                if (columns[0] != Sha256(payload))
                    throw new InvalidDataException("Outbox record checksum mismatch.");
                var record = new UniversityUploadIntent(columns[1], columns[2],
                    columns[3], columns[4], columns[5], columns[6],
                    columns[7], columns[8]);
                try { Validate(record); }
                catch (ArgumentException error)
                {
                    throw new InvalidDataException("Persisted outbox identity invalid.", error);
                }
                var key = record.InstitutionId + "/" + record.LocalSessionId + "/" +
                          record.EventId;
                if (!seen.Add(key))
                    throw new InvalidDataException("Duplicate event identity in outbox.");
                result.Add(record);
            }
            return result;
        }

        private static string Encode(UniversityUploadIntent intent) =>
            string.Join("\t", intent.InstitutionId, intent.LocalSessionId,
                intent.EventId, intent.NativeBranchId, intent.NativeEventSequence,
                intent.NativeReceiptSha256, intent.ScientificPackageSha256,
                intent.ArtifactSha256);

        private static void Validate(UniversityUploadIntent intent)
        {
            if (intent == null || !CanonicalUuid(intent.InstitutionId) ||
                !CanonicalUuid(intent.EventId) ||
                !SafeToken(intent.LocalSessionId) || !SafeToken(intent.NativeBranchId) ||
                !CanonicalSequence(intent.NativeEventSequence) ||
                !CanonicalSha256(intent.NativeReceiptSha256) ||
                !CanonicalSha256(intent.ScientificPackageSha256) ||
                !CanonicalSha256(intent.ArtifactSha256))
                throw new ArgumentException("Invalid tenant-scoped Native upload identity.");
        }

        private static bool CanonicalUuid(string text) =>
            text != null && Guid.TryParseExact(text, "D", out var parsed) &&
            parsed != Guid.Empty &&
            string.Equals(parsed.ToString("D"), text, StringComparison.Ordinal);

        private static bool CanonicalSequence(string text) =>
            text != null &&
            ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture,
                out var value) && value > 0 &&
            value.ToString(CultureInfo.InvariantCulture) == text;

        private static bool SafeToken(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 128) return false;
            foreach (var ch in text)
                if (!(ch >= 'a' && ch <= 'z') &&
                    !(ch >= 'A' && ch <= 'Z') &&
                    !(ch >= '0' && ch <= '9') &&
                    ch != '-' && ch != '_' && ch != '.') return false;
            return true;
        }

        private static bool CanonicalSha256(string text)
        {
            if (text == null || text.Length != 64) return false;
            foreach (var ch in text)
                if (!(ch >= '0' && ch <= '9') && !(ch >= 'a' && ch <= 'f'))
                    return false;
            return true;
        }

        private static string Sha256(string payload)
        {
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(StrictUtf8.GetBytes(payload));
                return BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
