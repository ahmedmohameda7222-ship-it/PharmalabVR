using System;

namespace PharmaLabVR.Input
{
    // Strict presentation-only placement admission. Physics chooses the
    // nearest real bench collider before policy runs; Native owns material.
    public static class DesktopPlacementAdmission
    {
        public const double MaximumBenchGapMetres = 0.20;
        public const double MinimumUprightDot = 0.95;

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
