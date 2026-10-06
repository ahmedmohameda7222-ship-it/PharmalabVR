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
        public string[] optionalUnsupportedObservables;
        public string[] steps;
        public string[] tracks;
        public string[] limitations;
    }

    [Serializable]
    public sealed class AssessmentContext
    {
        public string inputMode;
        public string[] assistanceUsed;
        public bool EligibleForCertifiedAssessment => false;
    }

    public sealed class ProtocolRunner
    {
        public ProtocolDefinition Definition { get; }
        public int StepIndex { get; private set; }
        public double CumulativeDeliveredVolumeM3 { get; private set; }
        public double StageDeliveredVolumeM3 { get; private set; }

        public ProtocolRunner(ProtocolDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (definition.schemaVersion != 1 || definition.certifiedScoring || definition.maturity != "Research")
                throw new ArgumentException("Only schema-1 ungraded Research protocols are supported.", nameof(definition));
        }

        public void RecordCommittedDelivery(double volumeM3)
        {
            if (double.IsNaN(volumeM3) || double.IsInfinity(volumeM3) || volumeM3 < 0.0)
                throw new ArgumentOutOfRangeException(nameof(volumeM3));
            CumulativeDeliveredVolumeM3 += volumeM3;
            StageDeliveredVolumeM3 += volumeM3;
        }

        public void RecordRefill() => StageDeliveredVolumeM3 = 0.0;

        public bool AdvanceInstruction()
        {
            if (Definition.steps == null || StepIndex + 1 >= Definition.steps.Length) return false;
            StepIndex++;
            return true;
        }
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
