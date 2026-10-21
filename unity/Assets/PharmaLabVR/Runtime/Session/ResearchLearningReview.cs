using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PharmaLabVR.Session
{
    // Durable learner annotations are separate from authoritative Native chemistry.
    public static class ResearchLearningReview
    {
        public static string Render(IReadOnlyList<NotebookEntry> notes, string reviewBranchId,
            bool compact)
        {
            if (string.IsNullOrWhiteSpace(reviewBranchId))
                return "Local notebook unavailable: missing recorded branch.";
            var matching = (notes ?? Array.Empty<NotebookEntry>())
                .Where(item => item != null && item.BranchId == reviewBranchId &&
                    !string.IsNullOrWhiteSpace(item.Text)).ToArray();
            if (matching.Length == 0)
                return "No student annotations for this recorded branch.";
            var text = new StringBuilder("Local student notebook · ");
            text.Append(matching.Length).Append(" student annotations (not Native scientific evidence)");
            var limit = compact ? 2 : 8;
            var start = Math.Max(0, matching.Length - limit);
            for (var i = start; i < matching.Length; i++)
            {
                var entry = matching[i];
                var excerpt = entry.Text.Replace('\r', ' ').Replace('\n', ' ').Trim();
                var maxLength = compact ? 64 : 170;
                if (excerpt.Length > maxLength)
                    excerpt = excerpt.Substring(0, maxLength - 3) + "...";
                text.Append("\nevent ").Append(entry.EventSequence).Append(" · ")
                    .Append(entry.VesselId).Append(" · ").Append(excerpt);
            }
            if (start > 0) text.Append("\nOpen Notebook for earlier notes.");
            return text.ToString();
        }
    }
}
