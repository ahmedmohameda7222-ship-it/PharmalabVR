using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PharmaLabVR.Session
{
    // Research notebook notes are annotations, never Native material or solver commands.
    // Their branch and event revisions come from an actual CoreSession/LabViewModel.
    public sealed class NotebookEntry
    {
        public string BranchId { get; }
        public string EventSequence { get; }
        public string VesselId { get; }
        public string Text { get; }
        public string RecordedAtUtc { get; }

        internal NotebookEntry(string branchId, string eventSequence, string vesselId,
            string text, string recordedAtUtc)
        {
            BranchId = branchId;
            EventSequence = eventSequence;
            VesselId = vesselId;
            Text = text;
            RecordedAtUtc = recordedAtUtc;
        }
    }

    // Local, versioned, checksummed note records; the 1 MiB envelope is distinct
    // from the authoritative Native session checkpoint and must not be confused
    // with it. Durable Save still belongs to SaveService.
    public sealed class NotebookService
    {
        private const string Header = "PLV-NOTEBOOK-V1\n";
        private const int MaximumFileBytes = 1024 * 1024;
        private const int MaximumNoteCharacters = 4096;
        private const int MaximumNotes = 512;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public NotebookEntry Append(string path, string branchId, string eventSequence,
            string vesselId, string text)
        {
            ValidatePath(path);
            ValidateId(branchId, nameof(branchId));
            ValidateRevision(eventSequence);
            ValidateId(vesselId, nameof(vesselId));
            ValidateText(text);
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) && File.Exists(fullPath + ".bak"))
                throw new InvalidDataException("Recover the confirmed notebook backup before editing.");
            // Only the primary is writable; corrupt current bytes must never be
            // silently replaced with a backup and new annotations.
            var entries = ReadStrict(fullPath);
            if (entries.Count >= MaximumNotes)
                throw new InvalidDataException("Research notebook record limit reached.");
            var added = new NotebookEntry(branchId, eventSequence, vesselId, text,
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            entries.Add(added);
            var bytes = StrictUtf8.GetBytes(Serialize(entries));
            if (bytes.Length > MaximumFileBytes)
                throw new InvalidDataException("Research notebook exceeds its file limit.");
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("Notebook has no parent directory.", nameof(path));
            Directory.CreateDirectory(directory);
            var temporary = fullPath + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(fullPath))
                    File.Replace(temporary, fullPath, fullPath + ".bak", true);
                else
                    File.Move(temporary, fullPath);
                return added;
            }
            catch
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
        }

        public IReadOnlyList<NotebookEntry> Read(string path, string branchId)
        {
            ValidatePath(path);
            ValidateId(branchId, nameof(branchId));
            var fullPath = Path.GetFullPath(path);
            List<NotebookEntry> all;
            try
            {
                if (!File.Exists(fullPath) && !File.Exists(fullPath + ".bak"))
                    return Array.Empty<NotebookEntry>();
                all = ReadStrict(fullPath);
                if (!File.Exists(fullPath))
                    all = ReadStrict(fullPath + ".bak");
            }
            catch (Exception error) when (error is InvalidDataException ||
                                         error is IOException || error is DecoderFallbackException)
            {
                if (!File.Exists(fullPath + ".bak")) throw;
                all = ReadStrict(fullPath + ".bak");
            }
            var result = new List<NotebookEntry>();
            foreach (var item in all)
                if (item.BranchId == branchId) result.Add(item);
            return result.AsReadOnly();
        }

        // Export only source-validated, branch-matching event annotations. Never
        // silently package a corrupted primary or an unconfirmed backup as current.
        public string ExportBranch(string path, string branchId, string maxEventSequence)
        {
            ValidatePath(path);
            ValidateId(branchId, nameof(branchId));
            ValidateRevision(maxEventSequence);
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) && File.Exists(fullPath + ".bak"))
                throw new InvalidDataException("Recover the notebook backup before portable export.");
            var entries = ReadStrict(fullPath);
            var limit = ulong.Parse(maxEventSequence, CultureInfo.InvariantCulture);
            var selected = new List<NotebookEntry>();
            foreach (var item in entries)
                if (item.BranchId == branchId &&
                    ulong.Parse(item.EventSequence, CultureInfo.InvariantCulture) <= limit)
                    selected.Add(item);
            return Serialize(selected);
        }

        public static void ValidatePortableSnapshot(string snapshot, string branchId,
            string maxEventSequence)
        {
            ValidateId(branchId, nameof(branchId));
            ValidateRevision(maxEventSequence);
            var limit = ulong.Parse(maxEventSequence, CultureInfo.InvariantCulture);
            var entries = ParseStrict(snapshot);
            foreach (var item in entries)
                if (item.BranchId != branchId ||
                    ulong.Parse(item.EventSequence, CultureInfo.InvariantCulture) > limit)
                    throw new InvalidDataException("Portable notes do not match the Native branch/revision.");
        }

        // Used by read-only teaching review of a .plvpack. Parse the
        // checksum-verified journal in memory; never recreate/write a local
        // Notebook file or accept a different Native branch/future event.
        public static IReadOnlyList<NotebookEntry> ReadPortableBranch(
            string snapshot, string branchId, string maxEventSequence)
        {
            ValidateId(branchId, nameof(branchId));
            ValidateRevision(maxEventSequence);
            var limit = ulong.Parse(maxEventSequence, CultureInfo.InvariantCulture);
            var parsed = ParseStrict(snapshot);
            foreach (var entry in parsed)
                if (entry.BranchId != branchId ||
                    ulong.Parse(entry.EventSequence, CultureInfo.InvariantCulture) > limit)
                    throw new InvalidDataException(
                        "Portable student annotation is outside its Native branch or event.");
            return parsed.AsReadOnly();
        }

        private static List<NotebookEntry> ReadStrict(string path)
        {
            if (!File.Exists(path)) return new List<NotebookEntry>();
            var file = new FileInfo(path);
            if (file.Length <= 0 || file.Length > MaximumFileBytes)
                throw new InvalidDataException("Notebook is empty or oversized.");
            return ParseStrict(File.ReadAllText(path, StrictUtf8));
        }

        private static List<NotebookEntry> ParseStrict(string content)
        {
            var result = new List<NotebookEntry>();
            if (string.IsNullOrEmpty(content) ||
                StrictUtf8.GetByteCount(content) > MaximumFileBytes)
                throw new InvalidDataException("Notebook snapshot is empty or oversized.");
            if (!content.StartsWith(Header, StringComparison.Ordinal) ||
                !content.EndsWith("\n", StringComparison.Ordinal))
                throw new InvalidDataException("Notebook header or committed tail is invalid.");
            var lines = content.Substring(Header.Length).Split('\n');
            foreach (var line in lines)
            {
                if (line.Length == 0) continue;
                var parts = line.Split('\t');
                if (parts.Length != 6 ||
                    !string.Equals(Hash(string.Join("\t", parts, 0, 5)), parts[5],
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Notebook record checksum is invalid.");
                try
                {
                    var branch = Decode(parts[0]);
                    var vessel = Decode(parts[2]);
                    var note = Decode(parts[3]);
                    var timestamp = Decode(parts[4]);
                    ValidateId(branch, nameof(branch));
                    ValidateRevision(parts[1]);
                    ValidateId(vessel, nameof(vessel));
                    ValidateText(note);
                    if (!DateTimeOffset.TryParseExact(timestamp, "O", CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var recorded) ||
                        recorded.Offset != TimeSpan.Zero)
                        throw new InvalidDataException("Notebook timestamp is invalid.");
                    result.Add(new NotebookEntry(branch, parts[1], vessel, note, timestamp));
                }
                catch (Exception error) when (error is ArgumentException ||
                                             error is FormatException ||
                                             error is DecoderFallbackException)
                {
                    throw new InvalidDataException("Notebook record identity or contents are invalid.", error);
                }
                if (result.Count > MaximumNotes)
                    throw new InvalidDataException("Notebook record count exceeds its limit.");
            }
            return result;
        }

        private static string Serialize(IReadOnlyList<NotebookEntry> entries)
        {
            var text = new StringBuilder(Header);
            foreach (var entry in entries)
            {
                var body = string.Join("\t", Encode(entry.BranchId), entry.EventSequence,
                    Encode(entry.VesselId), Encode(entry.Text), Encode(entry.RecordedAtUtc));
                text.Append(body).Append('\t').Append(Hash(body)).Append('\n');
            }
            return text.ToString();
        }

        private static string Encode(string value) =>
            Convert.ToBase64String(StrictUtf8.GetBytes(value));

        private static string Decode(string value) =>
            StrictUtf8.GetString(Convert.FromBase64String(value));

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(StrictUtf8.GetBytes(text)))
                    .Replace("-", string.Empty);
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Notebook path is required.", nameof(path));
        }

        private static void ValidateId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
                throw new ArgumentException("Notebook identifier is missing or oversized.", name);
            foreach (var character in value)
            {
                if ((character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9') ||
                    character == '-' || character == '_' || character == '.') continue;
                throw new ArgumentException("Notebook identifiers must be ASCII Native IDs.", name);
            }
        }

        private static void ValidateRevision(string value)
        {
            if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                    out var revision) || revision.ToString(CultureInfo.InvariantCulture) != value)
                throw new ArgumentException("Notebook revision must be a canonical Native event sequence.",
                    nameof(value));
        }

        private static void ValidateText(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumNoteCharacters)
                throw new ArgumentException("Research note is empty or oversized.", nameof(value));
            foreach (var character in value)
            {
                if ((char.IsControl(character) && character != '\n' &&
                     character != '\r' && character != '\t') ||
                    (character >= '\u0600' && character <= '\u06FF') ||
                    (character >= '\u0750' && character <= '\u077F') ||
                    (character >= '\u08A0' && character <= '\u08FF') ||
                    (character >= '\uFB50' && character <= '\uFDFF') ||
                    (character >= '\uFE70' && character <= '\uFEFF'))
                    throw new ArgumentException("Note text contains an unsupported script or control.", nameof(value));
            }
        }
    }
}
