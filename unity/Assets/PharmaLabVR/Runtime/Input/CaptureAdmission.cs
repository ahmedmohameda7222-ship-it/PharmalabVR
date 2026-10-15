using System;

namespace PharmaLabVR.Input
{
    // Research geometry admission for an instantaneous receiver aperture.
    // No parcel or chemistry is committed here: Native owns material transport.
    public static class CaptureAdmission
    {
        // Safety bound for the current static-aperture model, NOT measured
        // receiver/capture calibration. Moving targets require a future
        // trajectory-based intersection solver.
        public const double MaximumReceiverSpeedMetresPerSecond = 0.02;

        public static bool MayCapture(
            double receiverUprightDot,
            double outletDownwardDot,
            double outletHeightMetres,
            double lateralDistanceSquaredMetres,
            double apertureRadiusMetres,
            double maximumFallMetres,
            double receiverSpeedMetresPerSecond)
        {
            if (!Finite(receiverUprightDot) || !Finite(outletDownwardDot) ||
                !Finite(outletHeightMetres) || !Finite(lateralDistanceSquaredMetres) ||
                !Finite(apertureRadiusMetres) || !Finite(maximumFallMetres) ||
                !Finite(receiverSpeedMetresPerSecond))
                return false;

            return receiverUprightDot >= 0.9 &&
                   outletDownwardDot >= 0.85 &&
                   receiverSpeedMetresPerSecond >= 0.0 &&
                   receiverSpeedMetresPerSecond <= MaximumReceiverSpeedMetresPerSecond &&
                   apertureRadiusMetres > 0.0 &&
                   maximumFallMetres > 0.0 &&
                   outletHeightMetres > 0.0 &&
                   outletHeightMetres <= maximumFallMetres &&
                   lateralDistanceSquaredMetres >= 0.0 &&
                   lateralDistanceSquaredMetres <= apertureRadiusMetres * apertureRadiusMetres;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
