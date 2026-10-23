using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using System.Text;

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
            // Exact recorded numbers: do not export rounded UI labels.
            public bool BaselineRawAvailable { get; }
            public bool CandidateRawAvailable { get; }
            public double BaselineRawPH { get; }
            public double CandidateRawPH { get; }
            public string BaselineProviderModelId { get; }
            public string BaselineDatabaseIdentity { get; }
            public string CandidateProviderModelId { get; }
            public string CandidateDatabaseIdentity { get; }

            internal ComparisonRow(string vessel, string observable,
                RecordedSessionReview baselineRun, RecordedSessionReview candidateRun,
                RecordedSessionReview.RecordedObservation a,
                RecordedSessionReview.RecordedObservation b)
            {
                VesselId = vessel;
                ObservableId = observable;
                BaselineStatus = Availability(a);
                CandidateStatus = Availability(b);
                BaselineRawAvailable = BaselineStatus == "Current" && a != null;
                CandidateRawAvailable = CandidateStatus == "Current" && b != null;
                BaselineRawPH = BaselineRawAvailable ? a.value : 0.0;
                CandidateRawPH = CandidateRawAvailable ? b.value : 0.0;
                BaselineProviderModelId = a?.modelId ?? string.Empty;
                BaselineDatabaseIdentity = a?.databaseIdentity ?? string.Empty;
                CandidateProviderModelId = b?.modelId ?? string.Empty;
                CandidateDatabaseIdentity = b?.databaseIdentity ?? string.Empty;
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
            return Compare(RecordedSessionReview.OpenRecoverable(savedPath), candidate);
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

    // Immutable, recorded-only comparison export. Input rows originate from
    // two separately validated saved Native snapshots. This never runs a
    // scientific solver, invents unavailable pH or yields a grade.
    public static class RecordedRunComparisonCsv
    {
        private const int MaxBytes = 2 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static string RenderCsv(RecordedRunComparison recorded)
        {
            if (recorded == null) throw new ArgumentNullException(nameof(recorded));
            if (recorded.Rows.Count > 32)
                throw new InvalidDataException("Recorded comparison exceeds two Native vessel collections.");
            var output = new StringBuilder();
            output.Append("schemaVersion,recordKind,baselineBranchId,candidateBranchId,");
            output.Append("baselineModelIdentity,candidateModelIdentity,");
            output.Append("baselineProviderModelId,baselineDatabaseIdentity,");
            output.Append("candidateProviderModelId,candidateDatabaseIdentity,vesselId,observableId,");
            output.Append("unit,baselineStatus,candidateStatus,baselineRawPH,candidateRawPH,");
            output.Append("deltaAvailable,deltaPH,deltaReason\n");
            foreach (var row in recorded.Rows)
            {
                AppendRow(output, "1", "recorded-only-not-attested",
                    recorded.BaselineBranchId, recorded.CandidateBranchId,
                    recorded.BaselineModelIdentity, recorded.CandidateModelIdentity,
                    row.BaselineProviderModelId, row.BaselineDatabaseIdentity,
                    row.CandidateProviderModelId, row.CandidateDatabaseIdentity,
                    row.VesselId, row.ObservableId, "pH",
                    row.BaselineStatus, row.CandidateStatus,
                    row.BaselineRawAvailable ?
                        row.BaselineRawPH.ToString("R", CultureInfo.InvariantCulture) : "",
                    row.CandidateRawAvailable ?
                        row.CandidateRawPH.ToString("R", CultureInfo.InvariantCulture) : "",
                    row.DeltaAvailable ? "true" : "false",
                    row.DeltaAvailable ?
                        row.Delta.ToString("R", CultureInfo.InvariantCulture) : "",
                    row.DeltaReason);
            }
            var text = output.ToString();
            if (Utf8.GetByteCount(text) > MaxBytes)
                throw new InvalidDataException("Recorded comparison CSV exceeds 2 MiB.");
            return text;
        }

        // Every student export receives a new name. Neither an existing CSV
        // nor a prior Native/Notebook checkpoint is overwritten.
        public static string WriteNew(string directory, RecordedRunComparison recorded)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Offline CSV directory is required.", nameof(directory));
            var bytes = Utf8.GetBytes(RenderCsv(recorded));
            directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory,
                "native-comparison-" + Guid.NewGuid().ToString("N") + ".plv-compare.csv");
            var stage = target + ".stage";
            if (File.Exists(target) || File.Exists(stage))
                throw new IOException("Offline comparison export file already exists.");
            try
            {
                using (var stream = new FileStream(stage, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(stage, target); // immutable destination; never Replace
                return target;
            }
            catch
            {
                try { if (File.Exists(stage)) File.Delete(stage); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
        }

        private static void AppendRow(StringBuilder output, params string[] columns)
        {
            if (columns.Length != 20)
                throw new InvalidOperationException("Recorded comparison CSV column count changed.");
            for (var index = 0; index < columns.Length; index++)
            {
                if (index > 0) output.Append(',');
                // Only finite C# double values emitted using invariant
                // round-trip format can bypass spreadsheet text escaping.
                output.Append(index == 15 || index == 16 || index == 18 ?
                    columns[index] ?? string.Empty : EscapeText(columns[index]));
            }
            output.Append('\n');
        }

        private static string EscapeText(string text)
        {
            text ??= string.Empty;
            if (text.Length > 0 && (text[0] == '=' || text[0] == '+' ||
                text[0] == '-' || text[0] == '@' || text[0] == '\r' ||
                text[0] == '\t'))
                text = "'" + text;
            return text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ?
                "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }
    }

}
