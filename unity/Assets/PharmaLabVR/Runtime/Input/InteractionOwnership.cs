namespace PharmaLabVR.Input
{
    // Exclusive ownership policy shared by the native-input adapter and tests.
    // Grabbing by an XR ray, a missing tracked controller or a UI-owned trigger
    // may never be mistaken for direct body grip and valve actuation.
    public static class InteractionOwnership
    {
        public static bool MayActuateValve(bool directBodyGrip, bool controllerTracked,
            bool uiOwnsTrigger)
        {
            return directBodyGrip && controllerTracked && !uiOwnsTrigger;
        }
    }
    public static class ResearchXRReceiverInspectionAdmission
    {
        public static bool MayInspect(bool nativeReady, bool tracked,
            bool allInstrumentGripsReleased, bool uiOwnsTrigger,
            double distanceToReceiverColliderMetres)
        {
            return nativeReady && tracked && allInstrumentGripsReleased &&
                !uiOwnsTrigger &&
                !double.IsNaN(distanceToReceiverColliderMetres) &&
                !double.IsInfinity(distanceToReceiverColliderMetres) &&
                distanceToReceiverColliderMetres >= 0 &&
                distanceToReceiverColliderMetres <= 0.08;
        }
    }

}
