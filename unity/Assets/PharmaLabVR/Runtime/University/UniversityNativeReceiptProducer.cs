using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using PharmaLabVR.Core;

namespace PharmaLabVR.University
{
    // Captures a COMPLETE Native command outcome and its immutable Native
    // session export from the same live CoreSession. Never submits commands
    // or computes scientific values. This is LOCAL consistency, NOT remote
    // scientific attestation or a signed server sync acknowledgement.
    public static class UniversityNativeReceiptProducer
    {
        private const int MaxReceiptBytes = 1048576;
        private const int MaxExportBytes = 52428800;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [DataContract]
        private sealed class TerminalIdentity
        {
            [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
            [DataMember(Name = "branchId")] public string BranchId { get; set; }
            [DataMember(Name = "commandSequence")] public string CommandSequence { get; set; }
            [DataMember(Name = "type")] public string Type { get; set; }
        }

        // Only a terminal outcome returned by the Native SubmitOrdered path is
        // suitable. The caller passes its ORIGINAL JSON; this method obtains
        // export bytes from the still-active Native CoreSession itself.
        // EventId is an immutable local queue id, NOT a Native event clock.
        public static bool CaptureCompletedCommand(UniversitySyncOutbox outbox,
            CoreSession nativeSession, string institutionId, string localSessionId,
            string eventId, string completedOutcomeJson,
            byte[] verifiedScientificPackageBytes)
        {
            if (outbox == null) throw new ArgumentNullException(nameof(outbox));
            if (nativeSession == null) throw new ArgumentNullException(nameof(nativeSession));
            if (!nativeSession.IsOpen || string.IsNullOrEmpty(nativeSession.BranchId))
                throw new InvalidDataException("An active Native session is required.");
            if (string.IsNullOrEmpty(completedOutcomeJson) ||
                verifiedScientificPackageBytes == null ||
                verifiedScientificPackageBytes.Length == 0 ||
                verifiedScientificPackageBytes.Length > 16777216)
                throw new InvalidDataException("Native terminal outcome and bounded science bytes are required.");

            byte[] outcomeBytes;
            try { outcomeBytes = StrictUtf8.GetBytes(completedOutcomeJson); }
            catch (EncoderFallbackException error)
            {
                throw new InvalidDataException("Native outcome contains invalid text.", error);
            }
            if (outcomeBytes.Length == 0 || outcomeBytes.Length > MaxReceiptBytes)
                throw new InvalidDataException("Native terminal outcome exceeds its byte limit.");

            TerminalIdentity identity;
            try
            {
                using (var input = new MemoryStream(outcomeBytes, false))
                    identity = new DataContractJsonSerializer(typeof(TerminalIdentity))
                        .ReadObject(input) as TerminalIdentity;
            }
            catch (Exception error) when (error is SerializationException ||
                error is System.Xml.XmlException)
            {
                throw new InvalidDataException("Native terminal outcome is malformed.", error);
            }
            if (identity == null || identity.SchemaVersion != 1 ||
                identity.Type != "CommandOutcome" ||
                identity.BranchId != nativeSession.BranchId ||
                identity.CommandSequence == null ||
                !ulong.TryParse(identity.CommandSequence, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var sequence) ||
                sequence == 0 ||
                sequence.ToString(CultureInfo.InvariantCulture) != identity.CommandSequence)
                throw new InvalidDataException("Terminal outcome is not from the active Native branch.");

            // CoreSession's Native ABI boundary is the only export source.
            // Never manufacture the export from a Unity DTO or a cloud reply.
            var exportedJson = nativeSession.ExportSession();
            if (string.IsNullOrEmpty(exportedJson))
                throw new InvalidDataException("Native export is absent.");
            byte[] exportBytes;
            try { exportBytes = StrictUtf8.GetBytes(exportedJson); }
            catch (EncoderFallbackException error)
            {
                throw new InvalidDataException("Native export contains invalid text.", error);
            }
            if (exportBytes.Length == 0 || exportBytes.Length > MaxExportBytes)
                throw new InvalidDataException("Native export exceeds its byte limit.");

            var intent = new UniversityUploadIntent(institutionId, localSessionId, eventId,
                nativeSession.BranchId, identity.CommandSequence, Digest(outcomeBytes),
                Digest(verifiedScientificPackageBytes), Digest(exportBytes));
            // The source-binding gate checks exact receipt/export/package
            // relationships before the durable queue can be mutated.
            // Even success is a LOCAL PENDING intent, never academic sync.
            return outbox.EnqueueVerifiedEvidence(intent, outcomeBytes,
                verifiedScientificPackageBytes, exportBytes);
        }

        private static string Digest(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes))
                    .Replace("-", "").ToLowerInvariant();
        }
    }
}
