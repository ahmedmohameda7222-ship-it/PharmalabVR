namespace PharmaLabVR.Input
{
    // Safety admission for XR neutral/recovery input. The Native Core still
    // determines whether Continue is legal based on its own held-state ledger.
    public static class XRTrackingAdmission
    {
        public static bool HasFreshTracking(bool headTracked, bool leftHandTracked, bool rightHandTracked)
        {
            return headTracked && leftHandTracked && rightHandTracked;
        }
    }
}
