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
}
