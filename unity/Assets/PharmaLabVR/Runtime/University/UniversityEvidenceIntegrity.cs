using System;
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
            // This only records a pending local intent: no server ack exists.
            return store.Enqueue(intent);
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
