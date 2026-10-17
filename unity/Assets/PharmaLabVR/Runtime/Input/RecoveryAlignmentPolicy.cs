namespace PharmaLabVR.Input
{
    // Recenter is presentation-only. A Native Pause or Hold is required;
    // tracking and parked-body state must come from the real Unity adapters.
    public static class RecoveryAlignmentPolicy
    {
        public static bool CanRecenter(bool vrMode, bool nativePaused, bool nativeHeld,
            bool headTracked, bool bodyParked)
        {
            return vrMode && (nativePaused || nativeHeld) && headTracked && bodyParked;
        }

        public static int SnapDegrees(int direction)
        {
            if (direction != -1 && direction != 1)
                throw new System.ArgumentOutOfRangeException(nameof(direction));
            return 30 * direction;
        }
    }
}
