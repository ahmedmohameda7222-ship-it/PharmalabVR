// Contract harness only: production UniversityNativeReceiptProducer links
// the real PharmaLabVR.Core.CoreSession in Unity. This is NOT a Native run.
using System;

namespace PharmaLabVR.Core
{
    public sealed class CoreSession
    {
        private readonly string export;
        public string BranchId { get; }
        public bool IsOpen { get; set; } = true;
        public int ExportCount { get; private set; }

        public CoreSession(string branchId, string nativeExport)
        {
            BranchId = branchId;
            export = nativeExport ?? throw new ArgumentNullException(nameof(nativeExport));
        }

        public string ExportSession()
        {
            if (!IsOpen) throw new ObjectDisposedException(nameof(CoreSession));
            ExportCount++;
            return export;
        }
    }
}
