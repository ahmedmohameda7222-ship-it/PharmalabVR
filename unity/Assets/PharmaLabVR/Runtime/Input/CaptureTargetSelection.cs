using System;
using System.Collections.Generic;

namespace PharmaLabVR.Input
{
    // Strictly geometry-side arbitration; no inventory ownership or chemistry.
    // If overlapping apertures both claim a parcel, do NOT award it to the
    // first collider returned by Unity's nondeterministic object enumeration.
    public static class CaptureTargetSelection
    {
        public static string ResolveUnique(IReadOnlyList<string> eligibleInventoryIds)
        {
            if (eligibleInventoryIds == null || eligibleInventoryIds.Count != 1)
                return null;
            var id = eligibleInventoryIds[0];
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
    }
}
