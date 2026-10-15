namespace PharmaLabVR.Input
{
    public enum DesktopPointerAction { None, Valve, ToolDrag }

    // Exclusive input authority: GUI, disabled Desktop adapter and empty-hand
    // gestures must never be converted to a chemical valve or object movement.
    public static class DesktopPointerRouting
    {
        public static DesktopPointerAction Resolve(bool hasHeldTool, bool shiftPressed,
                                                   bool leftPressed, bool overUi, bool enabled)
        {
            if (!enabled || overUi || !hasHeldTool || !leftPressed)
                return DesktopPointerAction.None;
            return shiftPressed ? DesktopPointerAction.ToolDrag : DesktopPointerAction.Valve;
        }
    }
}
