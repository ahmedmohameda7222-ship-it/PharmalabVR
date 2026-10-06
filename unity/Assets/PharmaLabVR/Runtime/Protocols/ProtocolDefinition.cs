using System;

namespace PharmaLabVR.Protocols
{
    [Serializable]
    public sealed class ProtocolDefinition
    {
        public int schemaVersion;
        public string id;
        public string maturity;
        public bool certifiedScoring;
        public string[] requiredObservables;
        public string[] limitations;
    }

    public readonly struct ObservationCapability
    {
        public readonly string Id;
        public readonly string Support;
        public readonly string Computation;
        public readonly string Freshness;
        public readonly string Maturity;
        public ObservationCapability(string id, string support, string computation, string freshness, string maturity)
        { Id = id; Support = support; Computation = computation; Freshness = freshness; Maturity = maturity; }
    }

    public static class CapabilityGate
    {
        public static bool EligibleForCertifiedScore(ProtocolDefinition protocol, ObservationCapability[] capabilities)
        {
            if (protocol == null || !protocol.certifiedScoring || protocol.requiredObservables == null) return false;
            foreach (var required in protocol.requiredObservables)
            {
                var found = false;
                foreach (var capability in capabilities)
                    if (capability.Id == required && capability.Support == "Validated" && capability.Computation == "Ready" && capability.Freshness == "Current" && capability.Maturity == "Published") { found = true; break; }
                if (!found) return false;
            }
            return true;
        }
    }
}
