using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace PharmaLabVR.Session
{
    // Compare two independently validated recorded Native snapshots, never
    // reconstructed or interpolated chemistry. Availability and model identity
    // must agree before any signed numerical delta is shown.
    public sealed class RecordedRunComparison
    {
        public string BaselineBranchId { get; }
        public string CandidateBranchId { get; }
        public string BaselineModelIdentity { get; }
        public string CandidateModelIdentity { get; }
        public IReadOnlyList<ComparisonRow> Rows { get; }

        private RecordedRunComparison(RecordedSessionReview baseline, RecordedSessionReview candidate,
            IReadOnlyList<ComparisonRow> rows)
        {
            BaselineBranchId = baseline.BranchId;
            CandidateBranchId = candidate.BranchId;
            BaselineModelIdentity = baseline.ModelIdentity;
            CandidateModelIdentity = candidate.ModelIdentity;
            Rows = rows;
        }

        private sealed class Pair
        {
            public RecordedSessionReview.RecordedObservation Baseline;
            public RecordedSessionReview.RecordedObservation Candidate;
        }

        public sealed class ComparisonRow
        {
            public string VesselId { get; }
            public string ObservableId { get; }
            public string BaselineStatus { get; }
            public string CandidateStatus { get; }
            public string BaselineDisplay { get; }
            public string CandidateDisplay { get; }
            public bool DeltaAvailable { get; }
            public double Delta { get; }
            public string DeltaReason { get; }

            internal ComparisonRow(string vessel, string observable,
                RecordedSessionReview baselineRun, RecordedSessionReview candidateRun,
                RecordedSessionReview.RecordedObservation a,
                RecordedSessionReview.RecordedObservation b)
            {
                VesselId = vessel;
                ObservableId = observable;
                BaselineStatus = Availability(a);
                CandidateStatus = Availability(b);
                BaselineDisplay = BaselineStatus == "Current" ? a.DisplayValue : "--";
                CandidateDisplay = CandidateStatus == "Current" ? b.DisplayValue : "--";
                bool samePackage =
                    !string.IsNullOrWhiteSpace(baselineRun.ModelIdentity) &&
                    string.Equals(baselineRun.ModelIdentity, candidateRun.ModelIdentity,
                        StringComparison.Ordinal);
                bool sameProvider = a != null && b != null &&
                    !string.IsNullOrWhiteSpace(a.modelId) &&
                    !string.IsNullOrWhiteSpace(a.databaseIdentity) &&
                    string.Equals(a.modelId, b.modelId, StringComparison.Ordinal) &&
                    string.Equals(a.databaseIdentity, b.databaseIdentity, StringComparison.Ordinal);
                if (BaselineStatus != "Current" || CandidateStatus != "Current")
                    DeltaReason = "One or both recorded values are unavailable, stale or failed.";
                else if (!samePackage || !sameProvider)
                    DeltaReason = "Scientific package or provider provenance differs or is absent.";
                else if (!double.IsFinite(b.value - a.value))
                    DeltaReason = "Computed difference exceeds finite numeric bounds.";
                else
                {
                    DeltaAvailable = true;
                    Delta = b.value - a.value;
                    DeltaReason = "Comparable recorded pH values; no recomputation.";
                }
            }

            private static string Availability(RecordedSessionReview.RecordedObservation observation)
            {
                if (observation == null) return "Absent";
                if (observation.support != "Supported") return "Unsupported";
                if (observation.computationState == "Failed") return "Failed";
                if (observation.freshness == "Stale") return "Stale";
                if (observation.freshness == "Current" &&
                    observation.computationState == "Ready")
                    return observation.valueAvailable && double.IsFinite(observation.value) ?
                        "Current" : "Failed";
                return "Pending";
            }
        }

        public static RecordedRunComparison CompareSaved(string savedPath,
            RecordedSessionReview candidate)
        {
            if (string.IsNullOrWhiteSpace(savedPath))
                throw new ArgumentException("Saved Native checkpoint path is required.", nameof(savedPath));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            RecordedSessionReview baseline;
            try { baseline = RecordedSessionReview.Open(savedPath); }
            catch (Exception error) when (error is InvalidDataException ||
                                          error is IOException || error is ArgumentException)
            {
                // Only an independently validated confirmed backup may substitute
                // for a damaged/missing primary; never invent recorded observations.
                baseline = RecordedSessionReview.Open(savedPath + ".bak");
            }
            return Compare(baseline, candidate);
        }

        public static RecordedRunComparison Compare(RecordedSessionReview baseline,
            RecordedSessionReview candidate)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            var grouped = new SortedDictionary<string, Pair>(StringComparer.Ordinal);
            Add(grouped, baseline.Observations, isBaseline: true);
            Add(grouped, candidate.Observations, isBaseline: false);
            var result = new List<ComparisonRow>();
            foreach (var record in grouped)
            {
                var id = record.Key.Split('\0');
                result.Add(new ComparisonRow(id[0], id[1], baseline, candidate,
                    record.Value.Baseline, record.Value.Candidate));
            }
            return new RecordedRunComparison(baseline, candidate,
                new ReadOnlyCollection<ComparisonRow>(result));
        }

        private static void Add(SortedDictionary<string, Pair> grouped,
            RecordedSessionReview.RecordedObservation[] observations, bool isBaseline)
        {
            foreach (var value in observations)
            {
                if (value.vesselId.IndexOf('\0') >= 0 ||
                    value.observableId.IndexOf('\0') >= 0)
                    throw new InvalidDataException("Recorded observation ID contains a separator.");
                var key = value.vesselId + "\0" + value.observableId;
                if (!grouped.TryGetValue(key, out var pair))
                {
                    pair = new Pair();
                    grouped.Add(key, pair);
                }
                if (isBaseline)
                {
                    if (pair.Baseline != null)
                        throw new InvalidDataException("Duplicate Native observation in baseline.");
                    pair.Baseline = value;
                }
                else
                {
                    if (pair.Candidate != null)
                        throw new InvalidDataException("Duplicate Native observation in candidate.");
                    pair.Candidate = value;
                }
            }
        }
    }
}
