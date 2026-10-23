using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PharmaLabVR.Session
{
    // Directory-scoped filename selection only. This is a local UX helper,
    // not proof of author identity, Native science, or university membership.
    public static class OfflineArchivePicker
    {
        private static readonly Regex SafeFileName = new Regex(
            @"\A[A-Za-z0-9_-]{1,96}\.plvpack\z", RegexOptions.CultureInvariant);

        public static IReadOnlyList<string> Names(string sessionFolder,
            IEnumerable<string> candidatePaths, int limit = 12)
        {
            if (string.IsNullOrWhiteSpace(sessionFolder) ||
                candidatePaths == null || limit < 1 || limit > 64)
                throw new ArgumentException("A bounded local session directory and file list are required.");
            var directory = Path.GetFullPath(sessionFolder);
            var comparison = Path.DirectorySeparatorChar == '\\' ?
                StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var unique = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in candidatePaths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                string actual;
                try { actual = Path.GetFullPath(path); }
                catch (ArgumentException) { continue; }
                catch (NotSupportedException) { continue; }
                if (!string.Equals(Path.GetDirectoryName(actual), directory, comparison))
                    continue;
                var name = Path.GetFileName(actual);
                if (SafeFileName.IsMatch(name)) unique.Add(name);
            }
            return unique.Take(limit).ToArray();
        }

        public static string NextName(IReadOnlyList<string> names, string current)
        {
            if (names == null || names.Count == 0) return null;
            for (var i = 0; i < names.Count; i++)
                if (string.Equals(names[i], current, StringComparison.Ordinal))
                    return names[(i + 1) % names.Count];
            return names[0];
        }

        // An export is immutable learner evidence, not a mutable "latest"
        // record: new Notebook annotations at the same Native event still
        // warrant a separately selectable .plvpack. No paths from labels.
        public static string NewStudentExportName(string label,
            string nativeEventSequence, string uniqueHex)
        {
            if (string.IsNullOrWhiteSpace(nativeEventSequence) ||
                nativeEventSequence.Length > 20 ||
                !nativeEventSequence.All(c => c >= '0' && c <= '9') ||
                !ulong.TryParse(nativeEventSequence, out _) ||
                uniqueHex == null || uniqueHex.Length != 32 ||
                !uniqueHex.All(c => (c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'f')))
                throw new ArgumentException(
                    "A valid Native event and unique lowercase export token are required.");
            var prefix = new string((label ?? string.Empty).Where(c =>
                (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') || c == '-' || c == '_')
                .Take(28).ToArray());
            if (string.IsNullOrEmpty(prefix)) prefix = "study";
            var result = prefix + "-e" + nativeEventSequence + "-" +
                uniqueHex + ".plvpack";
            if (!ValidName(result))
                throw new InvalidDataException("Portable export filename exceeds safety bound.");
            return result;
        }

        public static bool ValidName(string name) =>
            name != null && SafeFileName.IsMatch(name);
    }
}
