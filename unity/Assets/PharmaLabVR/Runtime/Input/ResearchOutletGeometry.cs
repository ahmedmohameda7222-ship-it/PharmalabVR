using System;

namespace PharmaLabVR.Input
{
    // Converts the actual cylindrical mesh transform into a lab-local outlet
    // reference. Does not approximate fluid dynamics or claim calibration.
    public static class ResearchOutletGeometry
    {
        public static double BottomLocalY(double centerLocalY, double meshMinLocalY,
                                          double meshScaleY)
        {
            if (double.IsNaN(centerLocalY) || double.IsInfinity(centerLocalY) ||
                double.IsNaN(meshMinLocalY) || double.IsInfinity(meshMinLocalY) ||
                double.IsNaN(meshScaleY) || double.IsInfinity(meshScaleY) ||
                meshScaleY <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(meshScaleY),
                    "A finite positive physical mesh height is required.");
            return centerLocalY + meshMinLocalY * meshScaleY;
        }
    }
}
