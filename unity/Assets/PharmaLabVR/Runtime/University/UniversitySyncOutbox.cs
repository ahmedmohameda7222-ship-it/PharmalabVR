using System;
using System.Collections.Generic;

namespace PharmaLabVR.University
{
    // Offline-only upload intent, never Native material state or a cloud receipt.
    public sealed class UniversityUploadIntent
    {
        public string InstitutionId { get; }
        public string LocalSessionId { get; }
        public string EventId { get; }
        public string NativeBranchId { get; }
        public string NativeEventSequence { get; }
        public string NativeReceiptSha256 { get; }
        public string ScientificPackageSha256 { get; }
        public string ArtifactSha256 { get; }

        public UniversityUploadIntent(string institutionId, string localSessionId,
            string eventId, string nativeBranchId, string nativeEventSequence,
            string nativeReceiptSha256, string scientificPackageSha256, string artifactSha256)
        {
            InstitutionId = institutionId;
            LocalSessionId = localSessionId;
            EventId = eventId;
            NativeBranchId = nativeBranchId;
            NativeEventSequence = nativeEventSequence;
            NativeReceiptSha256 = nativeReceiptSha256;
            ScientificPackageSha256 = scientificPackageSha256;
            ArtifactSha256 = artifactSha256;
        }
    }

    // TDD RED: no file changes or acknowledgements until tested implementation.
    public sealed class UniversitySyncOutbox
    {
        public UniversitySyncOutbox(string path, int maxBytes = 1048576) { }
        public bool Enqueue(UniversityUploadIntent intent) => false;
        public IReadOnlyList<UniversityUploadIntent> Pending() => Array.Empty<UniversityUploadIntent>();
    }
}
