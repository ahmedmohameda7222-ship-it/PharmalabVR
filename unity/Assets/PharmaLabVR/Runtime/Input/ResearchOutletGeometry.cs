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

    // The measured 1 mL rinse-water pool can be smaller than its open
    // container. A tip inside the cup but outside real represented water
    // must never gain permission for a Native cleaning command.
    public static class ResearchRinseApertureAdmission
    {
        public static double EffectiveRadiusMeters(double mouthRadius,
            double meshRadialExtentX, double worldScaleX,
            double meshRadialExtentZ, double worldScaleZ)
        {
            if (!Finite(mouthRadius) || !Finite(meshRadialExtentX) ||
                !Finite(meshRadialExtentZ) || !Finite(worldScaleX) ||
                !Finite(worldScaleZ) || mouthRadius <= 0d ||
                meshRadialExtentX <= 0d || meshRadialExtentZ <= 0d ||
                worldScaleX == 0d || worldScaleZ == 0d)
                return 0d;
            var radiusX = meshRadialExtentX * Math.Abs(worldScaleX);
            var radiusZ = meshRadialExtentZ * Math.Abs(worldScaleZ);
            if (!Finite(radiusX) || !Finite(radiusZ)) return 0d;
            return Math.Min(mouthRadius, Math.Min(radiusX, radiusZ));
        }

        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
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
    // A geometric inlet admission only. The Native transfer command, not
    // this sensor, owns all source/burette material and revision updates.
    public static class ResearchBuretteInletAdmission
    {
        public static bool MayPrime(double buretteUprightDot,
            double pouringOutletDownDot, double outletHeightAboveMouth,
            double lateralDistanceSquared, double mouthRadius,
            bool buretteParked, bool valveClosed)
        {
            return Finite(buretteUprightDot) && Finite(pouringOutletDownDot) &&
                Finite(outletHeightAboveMouth) && Finite(lateralDistanceSquared) &&
                Finite(mouthRadius) && buretteParked && valveClosed &&
                buretteUprightDot >= 0.95 && pouringOutletDownDot >= 0.85 &&
                mouthRadius > 0 && lateralDistanceSquared >= 0 &&
                lateralDistanceSquared <= mouthRadius * mouthRadius &&
                outletHeightAboveMouth >= 0.005 && outletHeightAboveMouth <= 0.15;
        }

        private static bool Finite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }

    // Only geometrical admission. The real reserve/source transfer still
    // needs an accepted Native revision-checked inventory command.
    public static class ResearchReserveBottleRefillAdmission
    {
        public static bool MayRefill(double sourceUpright,
            double reserveOutletDown, double heightAboveSourceMouth,
            double lateralSquared, double sourceMouthRadius,
            double sourceSpeed, bool nativeReservePrepared)
        {
            return nativeReservePrepared &&
                ResearchBuretteInletAdmission.MayPrime(sourceUpright,
                    reserveOutletDown, heightAboveSourceMouth, lateralSquared,
                    sourceMouthRadius, true, true) &&
                !double.IsNaN(sourceSpeed) && !double.IsInfinity(sourceSpeed) &&
                sourceSpeed >= 0 && sourceSpeed <=
                    CaptureAdmission.MaximumReceiverSpeedMetresPerSecond;
        }
    }

}
