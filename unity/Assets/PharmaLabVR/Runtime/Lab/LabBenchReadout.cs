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
                   Format(snapshot, "burette", "Burette") + "\n" +
                   Format(snapshot, "receiver", "Receiver");
        }

        public static string Compact(LabViewModel snapshot)
        {
            if (snapshot?.Vessels == null) return "Native inventory unavailable.";
            return CompactVolume(snapshot, "source", "Stock") + " | " +
                   CompactVolume(snapshot, "burette", "Burette") + " | " +
                   CompactVolume(snapshot, "receiver", "Receiver");
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
            if (snapshot.Vessels.Any(vessel => vessel.Id == "burette"))
            {
                var burette = snapshot.Vessels.First(vessel => vessel.Id == "burette");
                if (ValidVolume(burette.CommittedVolumeM3, burette.CapacityM3) &&
                    burette.CommittedVolumeM3 > 0d)
                    return "Burette primed: release/hold, aim outlet and open stopcock; inspect receiver.";
                return "Prime the parked burette from Native stock, or transfer 0.01 mL by bottle.";
            }
            return "Transfer 0.01 mL with the research stock bottle, then Inspect receiver.";
        }

        // Only report an actual newly credited Native vessel inventory.
        // A requested quantity, accepted command receipt, or unchanged
        // event alone is NOT proof that material reached its destination.
        // Display is derived from independently published Native snapshots.
        public static string ObservedNativeTransfer(LabViewModel before,
            LabViewModel after, string destinationId)
        {
            if (before?.Vessels == null || after?.Vessels == null ||
                string.IsNullOrWhiteSpace(destinationId) ||
                !ulong.TryParse(before.EventSequence, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var previousEvent) ||
                !ulong.TryParse(after.EventSequence, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var currentEvent) ||
                currentEvent <= previousEvent ||
                before.EventSequence != previousEvent.ToString(CultureInfo.InvariantCulture) ||
                after.EventSequence != currentEvent.ToString(CultureInfo.InvariantCulture))
                return null;
            var oldMatching = before.Vessels.Where(v => v.Id == destinationId).ToArray();
            var newMatching = after.Vessels.Where(v => v.Id == destinationId).ToArray();
            if (oldMatching.Length != 1 || newMatching.Length != 1)
                return null;
            var oldValue = oldMatching[0];
            var newValue = newMatching[0];
            if (!ValidVolume(oldValue.CommittedVolumeM3, oldValue.CapacityM3) ||
                !ValidVolume(newValue.CommittedVolumeM3, newValue.CapacityM3) ||
                string.IsNullOrEmpty(oldValue.MaterialRevision) ||
                string.IsNullOrEmpty(newValue.MaterialRevision) ||
                oldValue.MaterialRevision == newValue.MaterialRevision)
                return null;
            var deltaM3 = newValue.CommittedVolumeM3 - oldValue.CommittedVolumeM3;
            // Show only a credit large enough to survive the unit precision
            // of the label. Do not emit "Native +0.000 mL".
            var roundedMl = Math.Round(deltaM3 * 1e6, 3,
                MidpointRounding.AwayFromZero);
            if (double.IsNaN(deltaM3) || double.IsInfinity(deltaM3) || roundedMl <= 0d)
                return null;
            return "Native " + destinationId + " +" +
                roundedMl.ToString("0.000", CultureInfo.InvariantCulture) +
                " mL · events " +
                previousEvent.ToString(CultureInfo.InvariantCulture) + " → " +
                currentEvent.ToString(CultureInfo.InvariantCulture);
        }

        private static string CompactVolume(LabViewModel view, string id, string title)
        {
            if (!view.Vessels.Any(item => item.Id == id)) return title + " --";
            var vessel = view.Vessels.First(item => item.Id == id);
            if (!ValidVolume(vessel.CommittedVolumeM3, vessel.CapacityM3))
                return title + " --";
            return title + " " +
                (vessel.CommittedVolumeM3 * 1e6).ToString("0.00", CultureInfo.InvariantCulture) +
                " mL";
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
