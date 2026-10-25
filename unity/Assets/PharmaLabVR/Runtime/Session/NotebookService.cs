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
    // Local record provenance, not cryptographic source attestation.
    // V1 six-field archives remain LEGACY and cannot grant new Native pH
    // completion; a seventh, checksum-covered field preserves kind for
    // all newly committed records.
    public enum NotebookRecordKind { Legacy, User, NativePH }

    public sealed class NotebookEntry
    {
        public string BranchId { get; }
        public string EventSequence { get; }
        public string VesselId { get; }
        public string Text { get; }
        public string RecordedAtUtc { get; }
        public NotebookRecordKind RecordKind { get; }

        internal NotebookEntry(string branchId, string eventSequence, string vesselId,
            string text, string recordedAtUtc,
            NotebookRecordKind recordKind = NotebookRecordKind.Legacy)
        {
            BranchId = branchId;
            EventSequence = eventSequence;
            VesselId = vesselId;
            Text = text;
            RecordedAtUtc = recordedAtUtc;
            RecordKind = recordKind;
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
            string vesselId, string text) =>
            AppendTyped(path, branchId, eventSequence, vesselId, text,
                NotebookRecordKind.User);

        // Instructor sidecar responses need exactly-once admission even
        // when two controllers request the same annotation concurrently.
        // Check under the same durable lease as journal publication.
        // This does NOT attest an instructor identity or touch Native science.
        public NotebookEntry AppendOnce(string path, string branchId,
            string eventSequence, string vesselId, string text) =>
            AppendTyped(path, branchId, eventSequence, vesselId, text,
                NotebookRecordKind.User, rejectIdentical: true);

        // Only the Native measurement UI flow may use this recorder,
        // after NativeMeasurementNote.Build has checked the raw/current
        // scientific snapshot. No student text-area action calls it.
        public NotebookEntry AppendNativeMeasurement(string path, string branchId,
            string eventSequence, string vesselId, string text)
        {
            if (string.IsNullOrEmpty(text) ||
                !text.StartsWith("Native pH ", StringComparison.Ordinal) ||
                !text.Contains(" · event " + eventSequence + " · Supported/Current",
                    StringComparison.Ordinal) ||
                !text.Contains(" · vessel " + vesselId + " · material revision ",
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Native pH journal entry requires a matching recorded scientific event.",
                    nameof(text));
            return AppendTyped(path, branchId, eventSequence, vesselId, text,
                NotebookRecordKind.NativePH);
        }

        private NotebookEntry AppendTyped(string path, string branchId,
            string eventSequence, string vesselId, string text,
            NotebookRecordKind recordKind, bool rejectIdentical = false)
        {
            ValidatePath(path);
            ValidateId(branchId, nameof(branchId));
            ValidateRevision(eventSequence);
            ValidateId(vesselId, nameof(vesselId));
            ValidateText(text);
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("Notebook has no parent directory.", nameof(path));
            Directory.CreateDirectory(directory);
            // Lock the entire read/check/write transaction: two independent
            // NotebookService instances cannot race and overwrite student notes.
            // A persistent lock filename avoids the delete/recreate inode race.
            try
            {
                using (var lease = new FileStream(fullPath + ".lock",
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    return AppendUnderLease(fullPath, branchId, eventSequence, vesselId,
                        text, recordKind, rejectIdentical);
            }
            catch (IOException error)
            {
                throw new InvalidDataException(
                    "Research notebook is busy or its storage is unavailable; no note committed.", error);
            }
        }

        private static NotebookEntry AppendUnderLease(string fullPath, string branchId,
            string eventSequence, string vesselId, string text, NotebookRecordKind recordKind,
            bool rejectIdentical)
        {
            if (!File.Exists(fullPath) && File.Exists(fullPath + ".bak"))
                throw new InvalidDataException("Recover the confirmed notebook backup before editing.");
            // Only the primary is writable. Damaged current bytes must never
            // silently be replaced by a backup and a fresh annotation.
            var entries = ReadStrict(fullPath);
            if (rejectIdentical &&
                entries.Exists(item => item.BranchId == branchId &&
                    item.EventSequence == eventSequence &&
                    item.VesselId == vesselId &&
                    item.RecordKind == recordKind &&
                    string.Equals(item.Text, text, StringComparison.Ordinal)))
                throw new InvalidDataException(
                    "This exact local formative response was already committed.");
            // Must be checked INSIDE the held file lease. Desktop/XR may
            // both pass an earlier UI preflight before either commits.
            // The second writer must never create a duplicate app-recorded
            // Native pH for the same scientific branch/event/vessel.
            if (recordKind == NotebookRecordKind.NativePH &&
                entries.Exists(item => item.RecordKind == NotebookRecordKind.NativePH &&
                    item.BranchId == branchId &&
                    item.EventSequence == eventSequence &&
                    item.VesselId == vesselId))
                throw new InvalidDataException(
                    "The current Native vessel pH was already recorded for this event.");
            if (entries.Count >= MaximumNotes)
                throw new InvalidDataException("Research notebook record limit reached.");
            var added = new NotebookEntry(branchId, eventSequence, vesselId, text,
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), recordKind);
            entries.Add(added);
            var bytes = StrictUtf8.GetBytes(Serialize(entries));
            if (bytes.Length > MaximumFileBytes)
                throw new InvalidDataException("Research notebook exceeds its file limit.");
            // Unique create-new staging prevents a stale <journal>.tmp from
            // being truncated after a crash, and never shares temp state with
            // other offline apps or sessions.
            var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
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

        // Recovery-oriented reads preserve the last confirmed, checksummed
        // backup when a primary was damaged. This is NOT proof that the
        // backup represents the current Native event.
        public IReadOnlyList<NotebookEntry> Read(string path, string branchId)
        {
            return ReadInternal(path, branchId, allowDamagedPrimaryRecovery: true);
        }

        // UI/Review reads may NOT silently present older backup annotations
        // as the active student's current evidence when the primary exists
        // but is corrupt. Missing primary still supports committed backup
        // recovery; a damaged primary is explicitly unavailable.
        public IReadOnlyList<NotebookEntry> ReadCurrent(string path,
            string branchId, string maxEventSequence)
        {
            ValidateRevision(maxEventSequence);
            var ceiling = ulong.Parse(maxEventSequence, CultureInfo.InvariantCulture);
            var all = ReadInternal(path, branchId,
                allowDamagedPrimaryRecovery: false);
            var visible = new List<NotebookEntry>();
            foreach (var entry in all)
                if (ulong.Parse(entry.EventSequence, CultureInfo.InvariantCulture) <= ceiling)
                    visible.Add(entry);
            return visible.AsReadOnly();
        }

        private static IReadOnlyList<NotebookEntry> ReadInternal(string path,
            string branchId, bool allowDamagedPrimaryRecovery)
        {
            ValidatePath(path);
            ValidateId(branchId, nameof(branchId));
            var fullPath = Path.GetFullPath(path);
            List<NotebookEntry> all;
            if (!File.Exists(fullPath))
            {
                all = File.Exists(fullPath + ".bak") ?
                    ReadStrict(fullPath + ".bak") : new List<NotebookEntry>();
            }
            else if (!allowDamagedPrimaryRecovery)
            {
                all = ReadStrict(fullPath);
            }
            else
            {
                try { all = ReadStrict(fullPath); }
                catch (Exception error) when (error is InvalidDataException ||
                                               error is IOException ||
                                               error is DecoderFallbackException)
                {
                    if (!File.Exists(fullPath + ".bak")) throw;
                    all = ReadStrict(fullPath + ".bak");
                }
            }
            var result = new List<NotebookEntry>();
            foreach (var item in all)
                if (item.BranchId == branchId) result.Add(item);
            return result.AsReadOnly();
        }

        // A local Native checkpoint at event N may only expose journal entries
        // at or before N, even when a later local run reused the branch ID.
        // The original checksummed journal remains immutable during this read.
        public IReadOnlyList<NotebookEntry> Read(string path, string branchId,
            string maxEventSequence)
        {
            ValidateRevision(maxEventSequence);
            var ceiling = ulong.Parse(maxEventSequence, CultureInfo.InvariantCulture);
            var current = Read(path, branchId);
            var visible = new List<NotebookEntry>();
            foreach (var entry in current)
                if (ulong.Parse(entry.EventSequence, CultureInfo.InvariantCulture) <= ceiling)
                    visible.Add(entry);
            return visible.AsReadOnly();
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

        // Transactional student restore: stage only a checksum-validated,
        // branch-scoped local journal in a *new* session namespace. This
        // must never merge into or replace another student's Notebook.
        public static void InstallPortableSnapshot(string path, string snapshot,
            string branchId, string maxEventSequence)
        {
            ValidatePath(path);
            ValidatePortableSnapshot(snapshot, branchId, maxEventSequence);
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidDataException("Portable Notebook has no parent directory.");
            Directory.CreateDirectory(directory);
            if (File.Exists(fullPath) || File.Exists(fullPath + ".bak"))
                throw new IOException("Portable restore cannot overwrite an existing Notebook.");
            var bytes = StrictUtf8.GetBytes(snapshot);
            var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                // Atomic, no-overwrite publish; the importer chooses a fresh
                // namespace and swaps its Native session only after success.
                File.Move(temporary, fullPath);
            }
            catch
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
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
            var nativeEventKeys = new HashSet<string>(StringComparer.Ordinal);
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
                var checksumIndex = parts.Length - 1;
                if ((parts.Length != 6 && parts.Length != 7) ||
                    !string.Equals(Hash(string.Join("\t", parts, 0, checksumIndex)),
                        parts[checksumIndex], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Notebook record checksum is invalid.");
                try
                {
                    var branch = Decode(parts[0]);
                    var vessel = Decode(parts[2]);
                    var note = Decode(parts[3]);
                    var timestamp = Decode(parts[4]);
                    var recordKind = NotebookRecordKind.Legacy;
                    if (parts.Length == 7 &&
                        (!Enum.TryParse(parts[5], false, out recordKind) ||
                         (recordKind != NotebookRecordKind.Legacy &&
                          recordKind != NotebookRecordKind.User &&
                          recordKind != NotebookRecordKind.NativePH)))
                        throw new InvalidDataException("Notebook evidence kind is unsupported.");
                    ValidateId(branch, nameof(branch));
                    ValidateRevision(parts[1]);
                    ValidateId(vessel, nameof(vessel));
                    ValidateText(note);
                    if (recordKind == NotebookRecordKind.NativePH &&
                        !nativeEventKeys.Add(branch + "\t" + parts[1] + "\t" + vessel))
                        throw new InvalidDataException(
                            "Duplicate app-recorded Native pH event in a checksummed Notebook.");
                    if (!DateTimeOffset.TryParseExact(timestamp, "O", CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var recorded) ||
                        recorded.Offset != TimeSpan.Zero)
                        throw new InvalidDataException("Notebook timestamp is invalid.");
                    result.Add(new NotebookEntry(branch, parts[1], vessel, note,
                        timestamp, recordKind));
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
                    Encode(entry.VesselId), Encode(entry.Text),
                    Encode(entry.RecordedAtUtc), entry.RecordKind.ToString());
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
