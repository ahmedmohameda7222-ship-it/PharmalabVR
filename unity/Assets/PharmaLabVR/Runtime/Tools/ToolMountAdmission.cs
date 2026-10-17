using System;

namespace PharmaLabVR.Tools
{
    // Conservative research dock geometry, not experimentally calibrated.
    // Admission happens BEFORE any Unity pose or Rigidbody mutation.
    public static class ToolMountAdmission
    {
        public const double MaximumDockDistanceMetres = 0.12;
        public const double MaximumAngularDeviationDegrees = 25.0;

        public static bool CanMount(double distanceMetres, double angularErrorDegrees,
                                    bool selectedByInteractor)
        {
            if (selectedByInteractor || double.IsNaN(distanceMetres) ||
                double.IsInfinity(distanceMetres) || double.IsNaN(angularErrorDegrees) ||
                double.IsInfinity(angularErrorDegrees))
                return false;
            return distanceMetres >= 0.0 &&
                   distanceMetres <= MaximumDockDistanceMetres &&
                   angularErrorDegrees >= 0.0 &&
                   angularErrorDegrees <= MaximumAngularDeviationDegrees;
        }
    }
}
