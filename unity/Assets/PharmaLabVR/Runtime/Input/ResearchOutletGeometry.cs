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

    // Native-rendered liquid level is read by the scene target; this admission
    // only checks geometric immersion and stationary upright stock access.
    public static class ResearchSourceImmersionAdmission
    {
        public static bool MayAspirate(double bottleUprightDot, double pipetteDownDot,
            double tipDepthMetres, double surfaceDepthMetres, double bottomDepthMetres,
            double lateralSquaredMetres, double mouthRadiusMetres, double bottleSpeedMetresPerSecond)
        {
            return Finite(bottleUprightDot) && Finite(pipetteDownDot) &&
                Finite(tipDepthMetres) && Finite(surfaceDepthMetres) &&
                Finite(bottomDepthMetres) && Finite(lateralSquaredMetres) &&
                Finite(mouthRadiusMetres) && Finite(bottleSpeedMetresPerSecond) &&
                bottleUprightDot >= 0.9 && pipetteDownDot >= 0.85 &&
                mouthRadiusMetres > 0 && lateralSquaredMetres >= 0 &&
                lateralSquaredMetres <= mouthRadiusMetres * mouthRadiusMetres &&
                surfaceDepthMetres > 0 && bottomDepthMetres > surfaceDepthMetres &&
                tipDepthMetres >= surfaceDepthMetres + 0.002 &&
                tipDepthMetres <= bottomDepthMetres - 0.004 &&
                bottleSpeedMetresPerSecond >= 0 &&
                bottleSpeedMetresPerSecond <= CaptureAdmission.MaximumReceiverSpeedMetresPerSecond;
        }

        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
