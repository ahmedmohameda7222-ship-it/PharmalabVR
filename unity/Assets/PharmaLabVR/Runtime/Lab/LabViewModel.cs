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
        public IReadOnlyList<ObservationView> Observations { get; }

        public readonly struct ObservationView
        {
            public string VesselId { get; }
            public string ObservableId { get; }
            public string ModelId { get; }
            public string DatabaseIdentity { get; }
            public string Support { get; }
            public string Freshness { get; }
            public string Maturity { get; }
            public string AsOfMaterialRevision { get; }
            // The original unrounded Native IPhreeqc observation, never a
            // value reverse-engineered from the two-decimal display label.
            public double Value { get; }
            public bool ValueAvailable { get; }
            public string ComputationState { get; }

            public ObservationView(string vesselId, string observableId, string modelId,
                string databaseIdentity, string support, string freshness, string maturity,
                string asOfMaterialRevision = null, double value = 0d,
                bool valueAvailable = false, string computationState = null)
            {
                VesselId = vesselId;
                ObservableId = observableId;
                ModelId = modelId;
                DatabaseIdentity = databaseIdentity;
                Support = support;
                Freshness = freshness;
                Maturity = maturity;
                AsOfMaterialRevision = asOfMaterialRevision;
                Value = value;
                ValueAvailable = valueAvailable;
                ComputationState = computationState;
            }
        }

        private LabViewModel(string eventSequence, IReadOnlyList<VesselView> vessels,
            IReadOnlyList<ObservationView> observations)
        {
            EventSequence = eventSequence;
            Vessels = vessels;
            Observations = observations;
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
            var sourceObservations = source.observations ?? Array.Empty<ObservationEnvelope>();
            var observations = new ObservationView[sourceObservations.Length];
            for (var index = 0; index < observations.Length; index++)
            {
                var observation = sourceObservations[index];
                if (observation == null) throw new FormatException("Native observation view is incomplete.");
                observations[index] = new ObservationView(observation.vesselId, observation.observableId,
                    observation.modelId, observation.databaseIdentity, observation.support,
                    observation.freshness, observation.maturity,
                    observation.asOfMaterialRevision, observation.value,
                    observation.valueAvailable, observation.computationState);
            }
            return new LabViewModel(source.eventSequence, new ReadOnlyCollection<VesselView>(vessels),
                new ReadOnlyCollection<ObservationView>(observations));
        }

        [Serializable] private sealed class SnapshotEnvelope
        {
            public string eventSequence;
            public VesselEnvelope[] vessels;
            public ObservationEnvelope[] observations;
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

        [Serializable] private sealed class ObservationEnvelope
        {
            public string vesselId;
            public string observableId;
            public string modelId;
            public string databaseIdentity;
            public string support;
            public string freshness;
            public string maturity;
            public string asOfMaterialRevision;
            public double value;
            public bool valueAvailable;
            public string computationState;
        }
    }
}
