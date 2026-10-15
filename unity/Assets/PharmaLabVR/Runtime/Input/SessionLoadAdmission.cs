namespace PharmaLabVR.Input
{
    // Pure import admission before any Native handle replacement.
    // A loaded Native session is already in the SessionLoad hold; mode
    // switching must remain an explicit BeginModeChange command.
    public static class SessionLoadAdmission
    {
        public static bool MayReplace(string currentMode, string savedMode,
            bool hasToolDock, bool toolParked)
        {
            if (currentMode != "Desktop" && currentMode != "VR")
                return false;
            if (savedMode != currentMode)
                return false;
            return currentMode == "Desktop" || (hasToolDock && toolParked);
        }
    }
}
