using System;
using System.Globalization;
using System.Linq;

namespace PharmaLabVR.Lab
{
    // Student-facing guidance, not a simulated inventory or scientific endpoint.
    // Every displayed volume/revision comes from the latest Native snapshot.
    public static class LabBenchReadout
    {
        public static string Inventory(LabViewModel snapshot)
        {
            if (snapshot?.Vessels == null) return "Native bench inventory unavailable.";
            return Format(snapshot, "source", "Stock bottle") + "\n" +
                   Format(snapshot, "receiver", "Receiver");
        }

        public static string Compact(LabViewModel snapshot)
        {
            if (snapshot?.Vessels == null) return "Native inventory unavailable.";
            return Format(snapshot, "source", "Stock") + " | " +
                   Format(snapshot, "receiver", "Receiver");
        }

        public static string NextAction(LabViewModel snapshot, bool needsStockPreparation,
            bool nativePaused, bool timeHeld)
        {
            if (timeHeld) return "Hold: restore tracking/neutral input; Continue only when Native permits.";
            if (needsStockPreparation)
                return "Prepare a supported stock and optional clean rinse water before transfer.";
            if (nativePaused) return "Pause: use the controlled Continue action before transferring.";
            if (snapshot?.Vessels == null ||
                !snapshot.Vessels.Any(vessel => vessel.Id == "source") ||
                !snapshot.Vessels.Any(vessel => vessel.Id == "receiver"))
                return "Native inventory unavailable; no material operation is permitted.";
            var receiver = snapshot.Vessels.First(vessel => vessel.Id == "receiver");
            if (!ValidVolume(receiver.CommittedVolumeM3, receiver.CapacityM3))
                return "Native receiver inventory unavailable; inspect the session before proceeding.";
            if (receiver.CommittedVolumeM3 > 0d)
                return "Inspect the receiver pH support/freshness; Save or open recorded Review.";
            var source = snapshot.Vessels.First(vessel => vessel.Id == "source");
            if (!ValidVolume(source.CommittedVolumeM3, source.CapacityM3) ||
                source.CommittedVolumeM3 <= 0d)
                return "Native stock inventory unavailable or empty; do not attempt a pour.";
            return "Transfer 0.01 mL with the research stock bottle, then Inspect receiver.";
        }

        private static string Format(LabViewModel view, string id, string title)
        {
            if (!view.Vessels.Any(item => item.Id == id)) return title + ": unavailable";
            var vessel = view.Vessels.First(item => item.Id == id);
            if (!ValidVolume(vessel.CommittedVolumeM3, vessel.CapacityM3))
                return title + ": unavailable";
            var revision = string.IsNullOrEmpty(vessel.MaterialRevision)
                ? "unknown" : vessel.MaterialRevision;
            return title + ": " +
                (vessel.CommittedVolumeM3 * 1e6).ToString("0.000", CultureInfo.InvariantCulture) +
                " mL (Native rev " + revision + ")";
        }

        private static bool ValidVolume(double volume, double capacity)
        {
            return !double.IsNaN(volume) && !double.IsInfinity(volume) &&
                !double.IsNaN(capacity) && !double.IsInfinity(capacity) &&
                capacity > 0d && volume >= 0d && volume <= capacity + 1e-10;
        }
    }
}
