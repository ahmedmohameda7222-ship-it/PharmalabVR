using System;

namespace PharmaLabVR.Input
{
    // Shared, pure policy so a pointer gesture never changes both world
    // apparatus and camera in the same frame. Values are research UX bounds.
    public enum DesktopWheelTarget { None, HeldToolHeight, CameraZoom }

    public static class DesktopWheelRouting
    {
        private const double WheelUnitsPerNotch = 120.0;
        private const double MaxHeightMetresPerFrame = 0.02;
        private const double FovDegreesPerNotch = 5.0;

        public static DesktopWheelTarget Resolve(bool hasHeldTool, bool pointerOverUi, double wheelDelta)
        {
            if (pointerOverUi || !Finite(wheelDelta) || wheelDelta == 0.0)
                return DesktopWheelTarget.None;
            return hasHeldTool ? DesktopWheelTarget.HeldToolHeight : DesktopWheelTarget.CameraZoom;
        }

        public static double HeightDeltaMetres(double wheelDelta)
        {
            return Finite(wheelDelta) ?
                Clamp(wheelDelta / WheelUnitsPerNotch, -1.0, 1.0) *
                    MaxHeightMetresPerFrame : 0.0;
        }

        public static double ZoomFieldOfViewDegrees(double previousFovDegrees, double wheelDelta)
        {
            if (!Finite(previousFovDegrees) || !Finite(wheelDelta))
                return Finite(previousFovDegrees) ? Clamp(previousFovDegrees, 35.0, 85.0) : 60.0;
            return Clamp(previousFovDegrees -
                Clamp(wheelDelta / WheelUnitsPerNotch, -1.0, 1.0) * FovDegreesPerNotch,
                35.0, 85.0);
        }

        private static double Clamp(double value, double min, double max) =>
            Math.Max(min, Math.Min(max, value));

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
