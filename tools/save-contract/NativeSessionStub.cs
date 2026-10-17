// Filesystem-only test double: no Native solver, chemistry, or Unity execution.
using System;
using System.IO;
namespace PharmaLabVR.Core
{
    public sealed class ScientificPackageMismatchException : Exception
    {
        public ScientificPackageMismatchException(string message) : base(message) { }
    }
    public sealed class CoreSession
    {
        public string Serialized { get; }
        public bool IsOpen => true;
        public CoreSession(string serialized) => Serialized = serialized;
        public string ExportSession() => Serialized;
        public static CoreSession ImportSession(string json, string scientificDatabasePath)
        {
            if (string.IsNullOrWhiteSpace(scientificDatabasePath) ||
                !json.StartsWith("journal:v1:", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid filesystem fixture.");
            return new CoreSession(json);
        }
    }
}
