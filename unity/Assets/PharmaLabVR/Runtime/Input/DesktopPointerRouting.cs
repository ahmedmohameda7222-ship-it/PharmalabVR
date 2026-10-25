namespace PharmaLabVR.Input
{
    public enum DesktopPointerAction { None, Valve, ToolDrag }
    public enum DesktopReceiverAction { None, Inspect, Pick }

    // Never interpret a measurement click as a receiver grab. The physical
    // receiver must be targeted deliberately with F before any translation.
    public static class DesktopReceiverRouting
    {
        public static DesktopReceiverAction Resolve(bool exactReceiver,
            bool pointerClick, bool fPick)
        {
            if (!exactReceiver || pointerClick == fPick)
                return DesktopReceiverAction.None;
            return pointerClick ? DesktopReceiverAction.Inspect :
                DesktopReceiverAction.Pick;
        }
    }

    // Exclusive input authority: GUI, disabled Desktop adapter and empty-hand
    // gestures must never be converted to a chemical valve or object movement.
    public static class DesktopPointerRouting
    {
        public static DesktopPointerAction Resolve(bool hasHeldTool, bool shiftPressed,
                                                   bool leftPressed, bool overUi, bool enabled) =>
            ResolveForHeldInstrument(hasHeldTool, true, shiftPressed,
                leftPressed, overUi, enabled);

        // Only a held registered burette grants pointer actuation. Auxiliary
        // pipette/bottle physical selection is movement, never Native chemistry.
        public static DesktopPointerAction ResolveForHeldInstrument(bool hasHeldTool,
            bool isNativeBurette, bool shiftPressed, bool leftPressed,
            bool overUi, bool enabled)
        {
            if (!enabled || overUi || !hasHeldTool || !leftPressed)
                return DesktopPointerAction.None;
            return !isNativeBurette || shiftPressed ?
                DesktopPointerAction.ToolDrag : DesktopPointerAction.Valve;
        }

        // Selection click is never reinterpreted as a valve or drag gesture.
        public static DesktopPointerAction ResolveForHeldInstrument(bool hasHeldTool,
            bool isNativeBurette, bool shiftPressed, bool leftPressed,
            bool overUi, bool enabled, bool selectingFromClick) =>
            selectingFromClick ? DesktopPointerAction.None :
                ResolveForHeldInstrument(hasHeldTool, isNativeBurette,
                    shiftPressed, leftPressed, overUi, enabled);

        public static bool ShouldSampleHeldAsNative(bool hasHeldTool,
            bool isNativeBurette) => hasHeldTool && isNativeBurette;
    }
}
