using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PharmaLabVR.Session
{
    // Local, ungraded chronological journal: no modeled/interpolated curve.
    public static class ResearchNotebookPHHistory
    {
        public static string Summarize(IReadOnlyList<NotebookEntry> notes, string branchId,
            string vesselId, int maxPoints)
        {
            const string empty = "No recorded Native pH observations in this local notebook.";
            if (notes == null || string.IsNullOrWhiteSpace(branchId) ||
                string.IsNullOrWhiteSpace(vesselId) || maxPoints < 1) return empty;
            var points = new SortedDictionary<ulong, double>();
            foreach (var note in notes)
            {
                if (note == null ||
                    note.RecordKind != NotebookRecordKind.NativePH ||
                    note.BranchId != branchId || note.VesselId != vesselId ||
                    note.Text == null || !note.Text.StartsWith("Native pH ", StringComparison.Ordinal) ||
                    !ulong.TryParse(note.EventSequence, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var eventId) ||
                    note.EventSequence != eventId.ToString(CultureInfo.InvariantCulture) ||
                    !note.Text.Contains(" · event " + note.EventSequence + " · Supported/Current",
                        StringComparison.Ordinal) ||
                    !note.Text.Contains("Research observation only; no certified endpoint inferred.",
                        StringComparison.Ordinal))
                    continue;
                const string rawTag = " · raw Native pH ";
                const string vesselTag = " · vessel ";
                var rawStart = note.Text.IndexOf(rawTag, StringComparison.Ordinal);
                if (rawStart <= "Native pH ".Length) continue;
                var vesselStart = note.Text.IndexOf(vesselTag,
                    rawStart + rawTag.Length, StringComparison.Ordinal);
                if (vesselStart < rawStart + rawTag.Length) continue;
                var displayed = note.Text.Substring("Native pH ".Length,
                    rawStart - "Native pH ".Length);
                var rawText = note.Text.Substring(rawStart + rawTag.Length,
                    vesselStart - rawStart - rawTag.Length);
                if (!note.Text.Substring(vesselStart).StartsWith(
                        vesselTag + vesselId + " · material revision ", StringComparison.Ordinal) ||
                    !double.TryParse(rawText, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var raw) ||
                    double.IsNaN(raw) || double.IsInfinity(raw) ||
                    raw.ToString("0.00", CultureInfo.InvariantCulture) != displayed)
                    continue;
                if (!points.ContainsKey(eventId)) points.Add(eventId, raw);
            }
            if (points.Count == 0) return empty;
            var output = new StringBuilder(
                "Local recorded Native pH by event (not a certified titration curve):");
            foreach (var point in points.Reverse().Take(maxPoints).Reverse())
                output.Append("\nevent ").Append(
                    point.Key.ToString(CultureInfo.InvariantCulture))
                    .Append(" · pH ").Append(
                    point.Value.ToString("R", CultureInfo.InvariantCulture));
            return output.ToString();
        }
    }
}
