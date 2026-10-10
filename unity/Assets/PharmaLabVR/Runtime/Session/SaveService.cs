using System;
using System.IO;
using PharmaLabVR.Core;

namespace PharmaLabVR.Session
{
    public sealed class SaveService
    {
        private const int MaxSaveBytes = 16 * 1024 * 1024;

        public string Save(CoreSession session, string directory, string name)
        {
            if (session == null || !session.IsOpen) throw new InvalidOperationException("No open native session.");
            ValidateName(name);
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, name + ".plv.json");
            var temporary = target + ".tmp";
            var bytes = System.Text.Encoding.UTF8.GetBytes(session.ExportSession());
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(target)) File.Replace(temporary, target, target + ".bak", true); else File.Move(temporary, target);
            return target;
        }

        public CoreSession Load(string path, string expectedScientificDatabasePath)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (string.IsNullOrEmpty(expectedScientificDatabasePath)) throw new InvalidOperationException("Scientific package is not ready.");
            try { return LoadOne(path, expectedScientificDatabasePath); }
            catch (Exception primary) when (IsRecoverableReadFailure(primary))
            {
                try { return LoadOne(path + ".bak", expectedScientificDatabasePath); }
                catch (Exception backup) when (IsRecoverableReadFailure(backup))
                {
                    throw new InvalidDataException("Neither the last save nor its confirmed backup can be loaded.",
                        new AggregateException(primary, backup));
                }
            }
        }

        private static CoreSession LoadOne(string path, string expectedScientificDatabasePath)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > MaxSaveBytes)
                throw new InvalidDataException("Save is missing, empty, or oversized.");
            return CoreSession.ImportSession(File.ReadAllText(info.FullName, System.Text.Encoding.UTF8), expectedScientificDatabasePath);
        }

        private static bool IsRecoverableReadFailure(Exception error) =>
            error is InvalidDataException || error is InvalidOperationException ||
            error is ArgumentException || error is IOException;

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Save name must be one safe file name.", nameof(name));
        }
    }
}
