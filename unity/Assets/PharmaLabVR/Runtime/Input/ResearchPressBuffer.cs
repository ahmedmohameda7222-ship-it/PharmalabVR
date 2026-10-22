using System;

namespace PharmaLabVR.Input
{
    // The input system exposes WasPressedThisFrame at Unity frame rate;
    // Native simulation samples at a slower fixed tick. Hold at most one
    // short-lived request until a Native tick can authoritatively admit it.
    // Pending intent does not commit any material or generate a receipt.
    public sealed class ResearchPressBuffer
    {
        private const ulong MaxAgeFrames = 12;
        private ulong capturedFrame;
        public string PendingToolId { get; private set; }

        public void Capture(string toolId, ulong frame, bool admitted)
        {
            if (!admitted || string.IsNullOrWhiteSpace(toolId)) return;
            if (PendingToolId != null && frame >= capturedFrame &&
                frame - capturedFrame <= MaxAgeFrames) return;
            PendingToolId = toolId;
            capturedFrame = frame;
        }

        public bool TryConsume(ulong frame, string toolId, bool admitted)
        {
            var success = admitted && PendingToolId != null &&
                string.Equals(PendingToolId, toolId, StringComparison.Ordinal) &&
                frame >= capturedFrame && frame - capturedFrame <= MaxAgeFrames;
            Clear(); // mismatch, expired, or blocked inputs cannot replay later
            return success;
        }

        public void Clear()
        {
            PendingToolId = null;
            capturedFrame = 0;
        }
    }

    // A Native SessionLoad/CheckpointRestart changes scientific authority. An
    // old grip, held tool, trigger or pointer button cannot carry permission
    // into that session. Re-arm only after observed neutral input, *not*
    // merely after a successful Native Continue command.
    public sealed class ResearchRestorationGestureGate
    {
        public bool IsArmed { get; private set; } = true;

        public void RequireFreshNeutralInput() => IsArmed = false;

        public bool ObserveNeutral(bool allOldToolsReleased, bool controlsNeutral,
            bool freshTracking)
        {
            if (!IsArmed && allOldToolsReleased && controlsNeutral && freshTracking)
                IsArmed = true;
            return IsArmed;
        }
    }
}
