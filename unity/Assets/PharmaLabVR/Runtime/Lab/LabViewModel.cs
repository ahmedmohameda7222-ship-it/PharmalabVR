using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace PharmaLabVR.Lab
{
    public sealed class LabViewModel
    {
        public readonly struct VesselView
        {
            public string Id { get; }
            public string MaterialRevision { get; }
            public double CapacityM3 { get; }
            public double CommittedVolumeM3 { get; }

            public VesselView(string id, string materialRevision, double capacityM3, double committedVolumeM3)
            {
                Id = id;
                MaterialRevision = materialRevision;
                CapacityM3 = capacityM3;
                CommittedVolumeM3 = committedVolumeM3;
            }
        }

        public string EventSequence { get; }
        public IReadOnlyList<VesselView> Vessels { get; }

        private LabViewModel(string eventSequence, IReadOnlyList<VesselView> vessels)
        {
            EventSequence = eventSequence;
            Vessels = vessels;
        }

        public static LabViewModel FromNativeSnapshot(string snapshot)
        {
            if (string.IsNullOrEmpty(snapshot)) throw new ArgumentException("Native snapshot is empty.", nameof(snapshot));
            var source = JsonUtility.FromJson<SnapshotEnvelope>(snapshot);
            if (source?.vessels == null) throw new FormatException("Native snapshot has no vessels.");
            var vessels = new VesselView[source.vessels.Length];
            for (var index = 0; index < vessels.Length; index++)
            {
                var vessel = source.vessels[index];
                if (vessel == null || vessel.inventory == null || string.IsNullOrEmpty(vessel.id))
                    throw new FormatException("Native vessel view is incomplete.");
                vessels[index] = new VesselView(vessel.id, vessel.materialRevision, vessel.capacityM3,
                    vessel.inventory.researchAdditiveVolumeM3);
            }
            return new LabViewModel(source.eventSequence,
                new ReadOnlyCollection<VesselView>(vessels));
        }

        [Serializable] private sealed class SnapshotEnvelope
        {
            public string eventSequence;
            public VesselEnvelope[] vessels;
        }

        [Serializable] private sealed class VesselEnvelope
        {
            public string id;
            public string materialRevision;
            public double capacityM3;
            public MaterialEnvelope inventory;
        }

        [Serializable] private sealed class MaterialEnvelope
        {
            public double researchAdditiveVolumeM3;
        }
    }
}
