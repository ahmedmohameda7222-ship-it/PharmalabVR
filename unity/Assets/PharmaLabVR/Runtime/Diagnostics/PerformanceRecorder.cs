using System;
using System.Collections.Generic;
using UnityEngine;

namespace PharmaLabVR.Diagnostics
{
    public sealed class PerformanceRecorder : MonoBehaviour
    {
        private readonly List<float> frameMilliseconds = new(8192);
        public int Capacity = 8192;
        private void Update()
        {
            if (frameMilliseconds.Count == Capacity) frameMilliseconds.RemoveAt(0);
            frameMilliseconds.Add(Time.unscaledDeltaTime * 1000f);
        }
        public float Percentile(float percentile)
        {
            if (frameMilliseconds.Count == 0) return 0f;
            var copy = frameMilliseconds.ToArray(); Array.Sort(copy);
            return copy[Mathf.Clamp(Mathf.CeilToInt(percentile * copy.Length) - 1, 0, copy.Length - 1)];
        }
    }
}
