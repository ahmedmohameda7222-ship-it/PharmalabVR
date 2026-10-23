using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PharmaLabVR.Session
{
    public sealed class PortableSessionData
    {
        public string NativeJson { get; }
        public string NotebookSnapshot { get; }
        public string BranchId { get; }
        public string EventSequence { get; }

        internal PortableSessionData(string nativeJson, string notebookSnapshot,
            string branchId, string eventSequence)
        {
            NativeJson = nativeJson;
            NotebookSnapshot = notebookSnapshot;
            BranchId = branchId;
            EventSequence = eventSequence;
        }
    }

    // Portable EXPORT container only. Importing a bundle into the live Native
    // context requires a separate transactional import flow and is not implied.
    // Checksums detect accidental corruption; they are not signatures or trust.
    public sealed class PortableSessionArchive
    {
        private const string Magic = "PLV-PACK-V1";
        private const int MaxNativeBytes = 16 * 1024 * 1024;
        private const int MaxNotesBytes = 1024 * 1024;
        private const int MaxArchiveBytes = 24 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        [Serializable]
        private sealed class NativeIdentity
        {
            public string branchId;
            public string eventSequence;
        }

        public static void Write(string path, string nativeJson, string notebookSnapshot,
            string branchId, string eventSequence)
        {
            var data = Validate(nativeJson, notebookSnapshot, branchId, eventSequence);
            var body = Magic + "\n" + branchId + "\n" + eventSequence + "\n" +
                Convert.ToBase64String(Utf8.GetBytes(data.NativeJson)) + "\n" +
                Convert.ToBase64String(Utf8.GetBytes(data.NotebookSnapshot)) + "\n";
            var content = body + Sha256(body) + "\n";
            var bytes = Utf8.GetBytes(content);
            if (bytes.Length > MaxArchiveBytes)
                throw new InvalidDataException("Portable archive exceeds its size bound.");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Archive path required.", nameof(path));
            var target = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidDataException("Archive has no parent directory.");
            Directory.CreateDirectory(directory);
            var temporary = target + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
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

        // Student evidence export is append-only. Unlike Write (the existing
        // atomic replace-with-backup API), WriteNew must refuse any existing
        // learner bundle even if another process creates it during staging.
        public static void WriteNew(string path, string nativeJson,
            string notebookSnapshot, string branchId, string eventSequence)
        {
            var data = Validate(nativeJson, notebookSnapshot, branchId, eventSequence);
            var body = Magic + "\n" + branchId + "\n" + eventSequence + "\n" +
                Convert.ToBase64String(Utf8.GetBytes(data.NativeJson)) + "\n" +
                Convert.ToBase64String(Utf8.GetBytes(data.NotebookSnapshot)) + "\n";
            var bytes = Utf8.GetBytes(body + Sha256(body) + "\n");
            if (bytes.Length > MaxArchiveBytes)
                throw new InvalidDataException("Portable archive exceeds its size bound.");
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Archive path required.", nameof(path));
            var target = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidDataException("Archive has no parent directory.");
            Directory.CreateDirectory(directory);
            if (File.Exists(target) || File.Exists(target + ".bak"))
                throw new IOException("An earlier student archive cannot be replaced.");
            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporary, target); // no overwrite (including concurrent creation)
            }
            catch
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
        }

        public static PortableSessionData Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Archive path required.", nameof(path));
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > MaxArchiveBytes)
                throw new InvalidDataException("Portable archive is missing, empty, or oversized.");
            string content;
            try { content = File.ReadAllText(info.FullName, Utf8); }
            catch (DecoderFallbackException error)
            { throw new InvalidDataException("Portable archive UTF-8 encoding is invalid.", error); }
            var parts = content.Split('\n');
            if (parts.Length != 7 || parts[0] != Magic || parts[6] != "" ||
                parts[5].Length != 64)
                throw new InvalidDataException("Portable archive framing or version is invalid.");
            var body = string.Join("\n", parts, 0, 5) + "\n";
            if (!string.Equals(parts[5], Sha256(body), StringComparison.Ordinal))
                throw new InvalidDataException("Portable archive checksum mismatch.");
            try
            {
                var native = Utf8.GetString(Convert.FromBase64String(parts[3]));
                var notes = Utf8.GetString(Convert.FromBase64String(parts[4]));
                return Validate(native, notes, parts[1], parts[2]);
            }
            catch (Exception error) when (error is FormatException ||
                                          error is DecoderFallbackException)
            {
                throw new InvalidDataException("Portable archive payload encoding is invalid.", error);
            }
        }

        private static PortableSessionData Validate(string native, string notes,
            string branch, string revision)
        {
            if (string.IsNullOrWhiteSpace(branch) || branch.Length > 64)
                throw new InvalidDataException("Native branch identity is missing or oversized.");
            foreach (var character in branch)
                if (!((character >= 'a' && character <= 'z') ||
                      (character >= 'A' && character <= 'Z') ||
                      (character >= '0' && character <= '9') ||
                      character == '.' || character == '_' || character == '-'))
                    throw new InvalidDataException("Native branch ID is invalid.");
            if (!ulong.TryParse(revision, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ||
                number.ToString(CultureInfo.InvariantCulture) != revision)
                throw new InvalidDataException("Native event revision is not canonical.");
            if (string.IsNullOrEmpty(native) || Utf8.GetByteCount(native) > MaxNativeBytes ||
                string.IsNullOrEmpty(notes) || Utf8.GetByteCount(notes) > MaxNotesBytes)
                throw new InvalidDataException("Archive scientific or notebook payload is empty or oversized.");
            NativeIdentity identity;
            try { identity = JsonUtility.FromJson<NativeIdentity>(native); }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            { throw new InvalidDataException("Native archive JSON is invalid.", error); }
            if (identity == null || identity.branchId != branch || identity.eventSequence != revision)
                throw new InvalidDataException("Native snapshot and declared archive identity disagree.");
            NotebookService.ValidatePortableSnapshot(notes, branch, revision);
            return new PortableSessionData(native, notes, branch, revision);
        }

        private static string Sha256(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(value))).Replace("-", "");
        }
    }
}
