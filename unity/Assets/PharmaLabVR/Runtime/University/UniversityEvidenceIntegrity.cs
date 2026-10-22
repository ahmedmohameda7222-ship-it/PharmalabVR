using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace PharmaLabVR.University
{
    // A local integrity gate, not an attestation of cloud upload.
    // Receipts must originate from the Native command/export boundary.
    public static class UniversityEvidenceIntegrity
    {
        public static bool EnqueueVerifiedEvidence(this UniversitySyncOutbox store,
            UniversityUploadIntent intent, byte[] nativeReceiptBytes,
            byte[] scientificPackageBytes, byte[] artifactBytes)
        {
            if (store == null || intent == null)
                throw new ArgumentException("Outbox and intent are required.");
            AssertDigest(nativeReceiptBytes, intent.NativeReceiptSha256, 1048576);
            AssertDigest(scientificPackageBytes, intent.ScientificPackageSha256, 16777216);
            AssertDigest(artifactBytes, intent.ArtifactSha256, 52428800);
            // The supplied Native terminal outcome must belong to THIS Native
            // export, branch, command sequence and scientific-package digest.
            // This is client-side consistency only, never an origin attestation.
            AssertNativeSourceBinding(intent, nativeReceiptBytes,
                scientificPackageBytes, artifactBytes);
            // This only records a pending local intent: no server ack exists.
            return store.Enqueue(intent);
        }


        [DataContract]
        private sealed class NativeExport
        {
            [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
            [DataMember(Name = "branchId")] public string BranchId { get; set; }
            [DataMember(Name = "nextCommandSequence")] public string NextCommandSequence { get; set; }
            [DataMember(Name = "scientificPackage")] public NativePackage Package { get; set; }
            [DataMember(Name = "commandReceipts")] public NativeReceipt[] Receipts { get; set; }
        }

        [DataContract]
        private sealed class NativePackage
        {
            [DataMember(Name = "identity")] public string Identity { get; set; }
        }

        [DataContract]
        private sealed class NativeReceipt
        {
            [DataMember(Name = "commandSequence")] public string CommandSequence { get; set; }
            [DataMember(Name = "canonicalCommand")] public NativeCommand Command { get; set; }
            [DataMember(Name = "terminalOutcome")] public NativeOutcome Outcome { get; set; }
        }

        [DataContract]
        private sealed class NativeCommand
        {
            [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
            [DataMember(Name = "branchId")] public string BranchId { get; set; }
            [DataMember(Name = "commandSequence")] public string CommandSequence { get; set; }
            [DataMember(Name = "type")] public string Type { get; set; }
        }

        [DataContract]
        private sealed class NativeOutcome
        {
            [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
            [DataMember(Name = "branchId")] public string BranchId { get; set; }
            [DataMember(Name = "commandSequence")] public string CommandSequence { get; set; }
            [DataMember(Name = "type")] public string Type { get; set; }
            [DataMember(Name = "accepted")] public bool Accepted { get; set; }
            [DataMember(Name = "code")] public string Code { get; set; }
            [DataMember(Name = "message")] public string Message { get; set; }
        }

        private static T ReadNativeJson<T>(byte[] bytes)
        {
            try
            {
                // Reject non-UTF8 bytes rather than accepting replacement characters
                // in an identity-bearing command or exported session.
                var normalized = new UTF8Encoding(false, true).GetBytes(
                    new UTF8Encoding(false, true).GetString(bytes));
                using (var input = new MemoryStream(normalized, false))
                    return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(input);
            }
            catch (Exception error) when (error is SerializationException ||
                error is System.Xml.XmlException || error is DecoderFallbackException ||
                error is InvalidCastException)
            {
                throw new InvalidDataException("Native receipt/export JSON is not structurally valid.", error);
            }
        }

        private static ulong CanonicalSequence(string value)
        {
            if (value == null || !ulong.TryParse(value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var parsed) || parsed == 0 ||
                parsed.ToString(CultureInfo.InvariantCulture) != value)
                throw new InvalidDataException("Native command sequence is not canonical.");
            return parsed;
        }

        private static void AssertNativeSourceBinding(UniversityUploadIntent intent,
            byte[] nativeReceiptBytes, byte[] scientificPackageBytes, byte[] artifactBytes)
        {
            var terminal = ReadNativeJson<NativeOutcome>(nativeReceiptBytes);
            var export = ReadNativeJson<NativeExport>(artifactBytes);
            if (terminal == null || export == null ||
                terminal.SchemaVersion != 1 || export.SchemaVersion != 1 ||
                terminal.Type != "CommandOutcome" ||
                terminal.Code == null || terminal.Message == null ||
                export.BranchId != intent.NativeBranchId ||
                terminal.BranchId != intent.NativeBranchId ||
                terminal.CommandSequence != intent.NativeEventSequence ||
                export.Package == null || export.Package.Identity == null ||
                export.Receipts == null || export.Receipts.Length == 0 ||
                export.Receipts.Length > 100000)
                throw new InvalidDataException("Native receipt and exported session identities do not agree.");

            string packageDigest;
            using (var sha = SHA256.Create())
                packageDigest = BitConverter.ToString(sha.ComputeHash(scientificPackageBytes))
                    .Replace("-", "");
            if (!string.Equals(packageDigest, export.Package.Identity,
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Native export scientific package differs from pinned bytes.");

            var expected = CanonicalSequence(intent.NativeEventSequence);
            if (CanonicalSequence(export.NextCommandSequence) <= expected)
                throw new InvalidDataException("Native export precedes its purported receipt.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var matched = false;
            foreach (var entry in export.Receipts)
            {
                if (entry == null || entry.Command == null || entry.Outcome == null ||
                    entry.Command.SchemaVersion != 1 || entry.Outcome.SchemaVersion != 1 ||
                    entry.Command.BranchId != export.BranchId ||
                    entry.Outcome.BranchId != export.BranchId ||
                    entry.CommandSequence != entry.Command.CommandSequence ||
                    entry.CommandSequence != entry.Outcome.CommandSequence ||
                    string.IsNullOrWhiteSpace(entry.Command.Type) ||
                    entry.Outcome.Type != "CommandOutcome" ||
                    entry.Outcome.Code == null || entry.Outcome.Message == null ||
                    !seen.Add(entry.CommandSequence) ||
                    CanonicalSequence(entry.CommandSequence) >=
                        CanonicalSequence(export.NextCommandSequence))
                    throw new InvalidDataException("Native export contains an invalid or duplicate command receipt.");
                if (entry.CommandSequence != intent.NativeEventSequence)
                    continue;
                if (matched ||
                    entry.Outcome.Accepted != terminal.Accepted ||
                    entry.Outcome.Code != terminal.Code ||
                    entry.Outcome.Message != terminal.Message ||
                    entry.Outcome.Type != terminal.Type)
                    throw new InvalidDataException("Native terminal receipt does not match the exported command.");
                matched = true;
            }
            if (!matched)
                throw new InvalidDataException("Native terminal outcome is absent from this export.");
        }

        private static void AssertDigest(byte[] bytes, string expected, int maxLength)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > maxLength)
                throw new InvalidDataException("Native evidence payload size is invalid.");
            using (var sha = SHA256.Create())
            {
                var actual = BitConverter.ToString(sha.ComputeHash(bytes))
                    .Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                    throw new InvalidDataException("Native evidence SHA-256 mismatch.");
            }
        }
    }
}
