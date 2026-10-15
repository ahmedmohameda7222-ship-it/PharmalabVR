using System;

namespace PharmaLabVR.Tools
{
    // Native committed source receipts are authoritative. This state controls
    // visibility only and cannot create droplets or change the material ledger.
    public sealed class DropParcelState
    {
        public string ActiveParcelId { get; private set; } = string.Empty;
        public double CommittedVolumeM3 { get; private set; }
        public string LastLandedParcelId { get; private set; } = string.Empty;
        private string lastHoldReason = string.Empty;

        // A deliberate Native load or checkpoint restart creates a new
        // replay epoch. An earlier landing token may validly be pending again
        // in the restored, held Native snapshot.
        public void ResetSession()
        {
            ActiveParcelId = string.Empty;
            CommittedVolumeM3 = 0.0;
            LastLandedParcelId = string.Empty;
        }

        // Native may publish many snapshots while the same SessionLoad or
        // CheckpointRestart hold remains active. Recognize each hold transition
        // once; accepted repeat imports/restarts invoke ResetSession explicitly.
        public bool ApplyNativeSnapshot(string parcelId, double volumeM3, string holdReason)
        {
            var reason = holdReason ?? string.Empty;
            if ((reason == "SessionLoad" || reason == "CheckpointRestart") &&
                reason != lastHoldReason)
                ResetSession();
            lastHoldReason = reason;
            return ApplySnapshot(parcelId, volumeM3);
        }

        public bool ApplySnapshot(string parcelId, double volumeM3)
        {
            if (string.IsNullOrWhiteSpace(parcelId) ||
                double.IsNaN(volumeM3) || double.IsInfinity(volumeM3) ||
                volumeM3 <= 0.0 || parcelId == LastLandedParcelId)
            {
                ActiveParcelId = string.Empty;
                CommittedVolumeM3 = 0.0;
                return false;
            }
            ActiveParcelId = parcelId;
            CommittedVolumeM3 = volumeM3;
            return true;
        }

        public bool Landed(string parcelId)
        {
            if (string.IsNullOrWhiteSpace(parcelId) || parcelId != ActiveParcelId ||
                CommittedVolumeM3 <= 0.0) return false;
            LastLandedParcelId = parcelId;
            ActiveParcelId = string.Empty;
            CommittedVolumeM3 = 0.0;
            return true;
        }
    }
}
