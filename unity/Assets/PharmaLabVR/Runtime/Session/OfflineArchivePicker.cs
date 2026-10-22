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

        public static bool ValidName(string name) =>
            name != null && SafeFileName.IsMatch(name);
    }
}
