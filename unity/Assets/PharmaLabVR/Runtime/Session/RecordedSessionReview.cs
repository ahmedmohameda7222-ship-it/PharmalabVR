using System;
using System.IO;
using UnityEngine;

namespace PharmaLabVR.Session
{
    public sealed class RecordedSessionReview
    {
        public string BranchId { get; }
        public string ModelIdentity { get; }
        public RecordedObservation[] Observations { get; }

        private RecordedSessionReview(string branchId, string modelIdentity, RecordedObservation[] observations)
        {
            BranchId = branchId;
            ModelIdentity = modelIdentity;
            Observations = observations;
        }

        public static RecordedSessionReview Open(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > 16 * 1024 * 1024)
                throw new InvalidDataException("Recorded session is missing, empty, or oversized.");
            return FromJson(File.ReadAllText(info.FullName, System.Text.Encoding.UTF8));
        }

        public static RecordedSessionReview FromJson(string json)
        {
            if (string.IsNullOrEmpty(json) ||
                System.Text.Encoding.UTF8.GetByteCount(json) > 16 * 1024 * 1024)
                throw new InvalidDataException("Recorded session is empty or oversized.");
            var saved = JsonUtility.FromJson<RecordedEnvelope>(json);
            if (saved == null || saved.schemaVersion != 1 || string.IsNullOrWhiteSpace(saved.branchId) ||
                saved.scientificPackage == null || saved.observations == null || saved.observations.Length > 16)
                throw new InvalidDataException("Recorded session lacks its model or observation identity.");
            foreach (var observation in saved.observations)
                if (observation == null || string.IsNullOrWhiteSpace(observation.vesselId) ||
                    observation.observableId != "pH")
                    throw new InvalidDataException("Recorded observation identity is invalid.");
            return new RecordedSessionReview(saved.branchId,
                saved.scientificPackage.identity ?? string.Empty, saved.observations);
        }

        [Serializable] private sealed class RecordedEnvelope
        {
            public int schemaVersion;
            public string branchId;
            public ScientificPackageRecord scientificPackage;
            public RecordedObservation[] observations;
        }

        [Serializable] private sealed class ScientificPackageRecord
        {
            public string identity;
        }

        [Serializable] public sealed class RecordedObservation
        {
            public string vesselId;
            public string observableId;
            public string asOfMaterialRevision;
            public string support;
            public string freshness;
            public string computationState;
            public string modelId;
            public string databaseIdentity;
            public double value;

            public string DisplayValue => support == "Supported" && computationState == "Ready" &&
                double.IsFinite(value) ? value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "--";
        }
    }
}
