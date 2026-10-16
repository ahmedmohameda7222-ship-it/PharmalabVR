using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PharmaLabVR.Session
{
    public sealed class RecordedSessionReview
    {
        public string BranchId { get; }
        public string ModelIdentity { get; }
        public RecordedObservation[] Observations { get; }
        public IReadOnlyList<RecordedCommandReceipt> CommandReceipts { get; }

        private RecordedSessionReview(string branchId, string modelIdentity,
            RecordedObservation[] observations, IReadOnlyList<RecordedCommandReceipt> receipts)
        {
            BranchId = branchId;
            ModelIdentity = modelIdentity;
            Observations = observations;
            CommandReceipts = receipts;
        }

        public static RecordedSessionReview Open(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > 16 * 1024 * 1024)
                throw new InvalidDataException("Recorded session is missing, empty, or oversized.");
            return FromJson(File.ReadAllText(info.FullName, System.Text.Encoding.UTF8));
        }

        public static RecordedSessionReview OpenRecoverable(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Native saved Review path is required.", nameof(path));
            try { return Open(path); }
            catch (Exception primary) when (primary is InvalidDataException ||
                                           primary is IOException || primary is ArgumentException)
            {
                // Read-only recovery only: never overwrite, replay or migrate
                // Native state. Both paths pass exactly the same record validator.
                return Open(path + ".bak");
            }
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
            var source = saved.commandReceipts ?? Array.Empty<ReceiptEnvelope>();
            var receipts = new RecordedCommandReceipt[source.Length];
            var sequences = new HashSet<ulong>();
            for (var index = 0; index < source.Length; index++)
            {
                var item = source[index];
                if (item?.canonicalCommand == null || item.terminalOutcome == null ||
                    !ulong.TryParse(item.commandSequence, NumberStyles.None, CultureInfo.InvariantCulture,
                        out var number) || number == 0 ||
                    number.ToString(CultureInfo.InvariantCulture) != item.commandSequence ||
                    !sequences.Add(number) ||
                    item.canonicalCommand.schemaVersion != 1 ||
                    item.terminalOutcome.schemaVersion != 1 ||
                    item.canonicalCommand.branchId != saved.branchId ||
                    item.terminalOutcome.branchId != saved.branchId ||
                    item.canonicalCommand.commandSequence != item.commandSequence ||
                    item.terminalOutcome.commandSequence != item.commandSequence ||
                    item.terminalOutcome.type != "CommandOutcome" ||
                    string.IsNullOrWhiteSpace(item.canonicalCommand.type) ||
                    string.IsNullOrWhiteSpace(item.terminalOutcome.code))
                    throw new InvalidDataException("Native command receipt identity is invalid.");
                receipts[index] = new RecordedCommandReceipt(item.commandSequence,
                    item.canonicalCommand.type, item.terminalOutcome.accepted,
                    item.terminalOutcome.code);
            }
            Array.Sort(receipts, (left, right) =>
                ulong.Parse(left.CommandSequence, CultureInfo.InvariantCulture)
                    .CompareTo(ulong.Parse(right.CommandSequence, CultureInfo.InvariantCulture)));
            return new RecordedSessionReview(saved.branchId,
                saved.scientificPackage.identity ?? string.Empty, saved.observations,
                new ReadOnlyCollection<RecordedCommandReceipt>(receipts));
        }

        [Serializable] private sealed class RecordedEnvelope
        {
            public int schemaVersion;
            public string branchId;
            public ScientificPackageRecord scientificPackage;
            public RecordedObservation[] observations;
            public ReceiptEnvelope[] commandReceipts;
        }

        [Serializable] private sealed class ScientificPackageRecord
        {
            public string identity;
        }

        public sealed class RecordedCommandReceipt
        {
            public string CommandSequence { get; }
            public string CommandType { get; }
            public bool Accepted { get; }
            public string OutcomeCode { get; }

            internal RecordedCommandReceipt(string sequence, string type, bool accepted, string code)
            {
                CommandSequence = sequence;
                CommandType = type;
                Accepted = accepted;
                OutcomeCode = code;
            }
        }

        [Serializable] private sealed class ReceiptEnvelope
        {
            public string commandSequence;
            public CanonicalCommandEnvelope canonicalCommand;
            public CommandOutcomeEnvelope terminalOutcome;
        }

        [Serializable] private sealed class CanonicalCommandEnvelope
        {
            public int schemaVersion;
            public string branchId;
            public string commandSequence;
            public string type;
        }

        [Serializable] private sealed class CommandOutcomeEnvelope
        {
            public int schemaVersion;
            public string branchId;
            public string commandSequence;
            public string type;
            public bool accepted;
            public string code;
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
            public string maturity;
            public double value;
            public bool valueAvailable;

            // JsonUtility maps a missing number to 0.0. An explicitly present,
            // Native-confirmed number is required before displaying recorded pH.
            public string DisplayValue => valueAvailable && support == "Supported" &&
                freshness == "Current" &&
                computationState == "Ready" && double.IsFinite(value) ?
                value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "--";
        }
    }
}
