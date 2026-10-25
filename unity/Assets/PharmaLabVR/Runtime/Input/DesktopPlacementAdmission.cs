using System;

namespace PharmaLabVR.Input
{
    // Strict presentation-only placement admission. Physics chooses the
    // nearest real bench collider before policy runs; Native owns material.
    public static class DesktopPlacementAdmission
    {
        public const double MaximumBenchGapMetres = 0.20;
        public const double MinimumUprightDot = 0.95;

        // A beaker's root is at its center, not at the physical foot. A
        // receiver may only be placed when its actual lowest collider point
        // is above the bench and within the same authorized handoff gap.
        public static bool CanPlaceReceiver(double rootHeightAboveBenchMetres,
            double rootToPhysicalBottomMetres, double uprightDot,
            bool nearestColliderIsBenchSurface, bool pointerOverUi)
        {
            if (double.IsNaN(rootToPhysicalBottomMetres) ||
                double.IsInfinity(rootToPhysicalBottomMetres) ||
                rootToPhysicalBottomMetres <= 0d)
                return false;
            return CanPlace(rootHeightAboveBenchMetres - rootToPhysicalBottomMetres,
                uprightDot, nearestColliderIsBenchSurface, pointerOverUi);
        }

        public static bool CanPlace(double verticalDistanceMetres, double uprightDot,
                                    bool nearestColliderIsBenchSurface, bool pointerOverUi)
        {
            if (pointerOverUi || !nearestColliderIsBenchSurface ||
                double.IsNaN(verticalDistanceMetres) || double.IsInfinity(verticalDistanceMetres) ||
                double.IsNaN(uprightDot) || double.IsInfinity(uprightDot))
                return false;
            return verticalDistanceMetres >= 0.0 &&
                   verticalDistanceMetres <= MaximumBenchGapMetres &&
                   uprightDot >= MinimumUprightDot && uprightDot <= 1.0;
        }
    }
}
