using System;
using System.Globalization;

namespace PharmaLabVR.Tools
{
    // Pure command-payload composition. No simulated chemistry or Unity inventory.
    public static class ResearchInstrumentPayloads
    {
        public const string PipetteProfile = "pipette-25ml-research-v1";
        public const string BottleProfile = "bottle-100ml-research-v1";

        private static string Id(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
                throw new ArgumentException("A bounded research inventory/tool id is required.");
            foreach (char c in value)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') || c == '_' || c == '-'))
                    throw new ArgumentException("Invalid native identifier: " + value);
            return "\"" + value + "\"";
        }

        private static string Revision(string revision)
        {
            if (!ulong.TryParse(revision, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException("A captured native revision is required.");
            return "\"" + value.ToString(CultureInfo.InvariantCulture) + "\"";
        }

        private static string Volume(double volumeM3)
        {
            if (double.IsNaN(volumeM3) || double.IsInfinity(volumeM3) || volumeM3 <= 0)
                throw new ArgumentOutOfRangeException(nameof(volumeM3), "Use a positive finite measured volume.");
            return volumeM3.ToString("R", CultureInfo.InvariantCulture);
        }

        public static string Place(string toolId, string sourceInventoryId,
                                   string overflowSinkId, string profile)
        {
            if (profile != PipetteProfile && profile != BottleProfile)
                throw new ArgumentException("Metered research profile not recognized.", nameof(profile));
            return "{\"toolId\":" + Id(toolId) +
                   ",\"sourceInventoryId\":" + Id(sourceInventoryId) +
                   ",\"overflowSinkId\":" + Id(overflowSinkId) +
                   ",\"coordinateFrame\":\"lab\",\"geometryProfileHash\":" + Id(profile) +
                   ",\"profileRevision\":\"1\"}";
        }

        public static string Aspirate(string toolId, double volumeM3, string inventoryRevision) =>
            "{\"toolId\":" + Id(toolId) + ",\"quantityM3\":" + Volume(volumeM3) +
            ",\"expectedInventoryRevision\":" + Revision(inventoryRevision) + "}";

        public static string DispenseOrPour(string toolId, string receiverId,
                                            double volumeM3, string inventoryRevision) =>
            "{\"toolId\":" + Id(toolId) + ",\"destinationInventoryId\":" + Id(receiverId) +
            ",\"quantityM3\":" + Volume(volumeM3) +
            ",\"expectedInventoryRevision\":" + Revision(inventoryRevision) + "}";

        public static string SwitchPipetteSource(string toolId, string nextStockId, string inventoryRevision) =>
            "{\"toolId\":" + Id(toolId) + ",\"sourceInventoryId\":" + Id(nextStockId) +
            ",\"expectedInventoryRevision\":" + Revision(inventoryRevision) + "}";

        // A refill transfers existing already-prepared Native stock; it never
        // issues PrepareStock after setup or creates any new material.
        public static string RefillFromPreparedStock(string sourceId, string destinationId,
                                                      string overflowSinkId, double volumeM3)
        {
            if (sourceId == destinationId)
                throw new ArgumentException("A Native stock bottle cannot refill itself.");
            return "{\"sourceInventoryId\":" + Id(sourceId) +
                   ",\"sourceRegion\":\"Homogeneous\"" +
                   ",\"selection\":\"HomogeneousAqueousLiquid\"" +
                   ",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":" + Volume(volumeM3) + "}" +
                   ",\"captureFractions\":[{\"destinationInventoryId\":" + Id(destinationId) +
                   ",\"fraction\":1.0}]" +
                   ",\"overflowSinkId\":" + Id(overflowSinkId) + "}";
        }

        public static string Rinse(string toolId, string cleanWaterSourceId, string wasteSinkId,
                                   double volumeM3, string inventoryRevision) =>
            "{\"toolId\":" + Id(toolId) + ",\"rinseSourceInventoryId\":" + Id(cleanWaterSourceId) +
            ",\"wasteSinkId\":" + Id(wasteSinkId) +
            ",\"expectedInventoryRevision\":" + Revision(inventoryRevision) +
            ",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":" + Volume(volumeM3) + "}}";
    }
}
