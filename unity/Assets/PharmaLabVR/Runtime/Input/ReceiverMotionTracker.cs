using System;

namespace PharmaLabVR.Input
{
    // Pure motion observer for XRI/kinematic receivers that can move even when
    // their Rigidbody.linearVelocity remains zero. Reports world metres/sec.
    // A repeated tool sample in the same frame must NOT erase a prior
    // high-speed reading (both VR hands may query the same receiver).
    public sealed class ReceiverMotionTracker
    {
        private bool hasSample;
        private int lastFrame;
        private double lastTimeS, lastX, lastY, lastZ, lastSpeed;

        public double Observe(int frame, double timeSeconds, double x, double y, double z)
        {
            if (frame < 0 || !Finite(timeSeconds) || !Finite(x) || !Finite(y) || !Finite(z))
                return double.PositiveInfinity;
            if (hasSample && frame == lastFrame) return lastSpeed;
            if (hasSample && (frame < lastFrame || timeSeconds <= lastTimeS))
                return double.PositiveInfinity;
            if (!hasSample)
            {
                hasSample = true;
                lastFrame = frame;
                lastTimeS = timeSeconds;
                lastX = x; lastY = y; lastZ = z;
                lastSpeed = 0.0;
                return 0.0;
            }
            var dx = x - lastX;
            var dy = y - lastY;
            var dz = z - lastZ;
            var speed = Math.Sqrt(dx * dx + dy * dy + dz * dz) /
                        (timeSeconds - lastTimeS);
            if (!Finite(speed)) return double.PositiveInfinity;
            lastFrame = frame;
            lastTimeS = timeSeconds;
            lastX = x; lastY = y; lastZ = z;
            lastSpeed = speed;
            return speed;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
