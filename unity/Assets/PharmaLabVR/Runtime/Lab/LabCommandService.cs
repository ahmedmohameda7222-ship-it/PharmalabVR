using System;
using System.Collections.Generic;
using System.Text;
using PharmaLabVR.Core;
using UnityEngine;

namespace PharmaLabVR.Lab
{
    // A captured intent keeps the revisions the user saw. Native code alone decides admission.
    public sealed class LabCommandService
    {
        private readonly CoreSession session;

        public LabCommandService(CoreSession session) => this.session = session ?? throw new ArgumentNullException(nameof(session));

        public TouchedRevisions CaptureTouchedRevisions(params string[] vesselIds)
        {
            if (vesselIds == null) throw new ArgumentNullException(nameof(vesselIds));
            var snapshot = JsonUtility.FromJson<Snapshot>(session.ReadSnapshot());
            if (snapshot?.vessels == null) throw new InvalidOperationException("Native material snapshot is unavailable.");
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in vesselIds)
            {
                if (string.IsNullOrWhiteSpace(id) || values.ContainsKey(id))
                    throw new ArgumentException("Touched vessel identities must be unique and nonempty.", nameof(vesselIds));
                Vessel matched = null;
                foreach (var vessel in snapshot.vessels)
                    if (vessel.id == id) { matched = vessel; break; }
                if (matched == null || !ulong.TryParse(matched.materialRevision, out _))
                    throw new ArgumentException("Touched vessel is absent from the native snapshot: " + id, nameof(vesselIds));
                values.Add(id, matched.materialRevision);
            }
            var json = new StringBuilder("{");
            foreach (var item in values)
            {
                if (json.Length > 1) json.Append(',');
                json.Append('"').Append(Escape(item.Key)).Append("\":\"").Append(item.Value).Append('"');
            }
            json.Append('}');
            return new TouchedRevisions(json.ToString());
        }

        public LabCommandReceipt Submit(string type, string payloadJson, TouchedRevisions revisions)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(payloadJson))
                throw new ArgumentException("A command type and payload are required.");
            if (revisions == null) throw new ArgumentNullException(nameof(revisions));
            var raw = session.SubmitOrdered(type, payloadJson, revisions.Json);
            var outcome = JsonUtility.FromJson<Outcome>(raw);
            if (outcome == null || outcome.type != "CommandOutcome" || string.IsNullOrEmpty(outcome.code))
                throw new InvalidOperationException("Native command returned an invalid receipt.");
            return new LabCommandReceipt(outcome.accepted, outcome.code, outcome.commandSequence, raw);
        }

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable] private sealed class Snapshot { public Vessel[] vessels; }
        [Serializable] private sealed class Vessel { public string id; public string materialRevision; }
        [Serializable] private sealed class Outcome
        {
            public string type;
            public bool accepted;
            public string code;
            public string commandSequence;
        }
    }

    public sealed class TouchedRevisions
    {
        internal string Json { get; }
        internal TouchedRevisions(string json) => Json = json;
    }

    public sealed class LabCommandReceipt
    {
        public bool Accepted { get; }
        public string Code { get; }
        public string CommandSequence { get; }
        public string RawJson { get; }
        internal LabCommandReceipt(bool accepted, string code, string sequence, string rawJson)
        {
            Accepted = accepted;
            Code = code;
            CommandSequence = sequence;
            RawJson = rawJson;
        }
    }
}
