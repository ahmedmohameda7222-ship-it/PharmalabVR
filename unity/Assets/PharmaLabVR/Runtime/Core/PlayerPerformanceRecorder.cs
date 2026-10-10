using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace PharmaLabVR.Core
{
    public sealed class PlayerPerformanceRecorder : MonoBehaviour
    {
        private const int MaxFrameSamples = 200000;
        private readonly float[] frameMs = new float[MaxFrameSamples];
        private CoreDriver driver;
        private DateTime startedUtc;
        private int nextSample;
        private int holdCount;
        private int computeHoldCount;
        private long peakManagedBytes;
        private long peakAllocatedBytes;
        private string finalSnapshot = "";
        private bool written;
        public int SampleCount { get; private set; }

        public void Configure(CoreDriver core)
        {
            driver = core;
            startedUtc = DateTime.UtcNow;
            core.HoldChanged += OnHoldChanged;
            core.SnapshotChanged += OnSnapshotChanged;
        }

        private void Update()
        {
            var ms = Time.unscaledDeltaTime * 1000f;
            if (float.IsFinite(ms) && ms > 0f)
            {
                frameMs[nextSample] = ms;
                nextSample = (nextSample + 1) % MaxFrameSamples;
                if (SampleCount < MaxFrameSamples) SampleCount++;
            }
            if (Time.frameCount % 60 != 0) return;
            peakManagedBytes = Math.Max(peakManagedBytes, GC.GetTotalMemory(false));
            peakAllocatedBytes = Math.Max(peakAllocatedBytes, Profiler.GetTotalAllocatedMemoryLong());
        }

        private void OnHoldChanged(bool held)
        {
            if (!held) return;
            holdCount++;
            if (driver != null && driver.HoldReason == "Compute") computeHoldCount++;
        }

        private void OnSnapshotChanged(string snapshot) => finalSnapshot = snapshot;

        private void OnApplicationQuit() => WriteCapture();

        private void OnDestroy()
        {
            if (driver != null)
            {
                driver.HoldChanged -= OnHoldChanged;
                driver.SnapshotChanged -= OnSnapshotChanged;
            }
            WriteCapture();
        }

        private void WriteCapture()
        {
            if (written || Application.isEditor || SampleCount == 0) return;
            written = true;
            try
            {
                var ordered = new float[SampleCount];
                var start = SampleCount == MaxFrameSamples ? nextSample : 0;
                for (var index = 0; index < SampleCount; index++)
                    ordered[index] = frameMs[(start + index) % MaxFrameSamples];
                var capture = new Capture
                {
                    schemaVersion = 1,
                    target = Application.platform == RuntimePlatform.Android ? "AndroidVR" :
                        driver != null && driver.CurrentMode == "VR" ? "WindowsVR" : "WindowsDesktop",
                    actualPlayerRun = true,
                    startedUtc = startedUtc.ToString("O"),
                    durationSeconds = (DateTime.UtcNow - startedUtc).TotalSeconds,
                    applicationVersion = Application.version,
                    graphicsDevice = SystemInfo.graphicsDeviceName,
                    frameMs = ordered,
                    totalRecordedFrames = SampleCount,
                    holds = holdCount,
                    computeHolds = computeHoldCount,
                    peakManagedBytes = peakManagedBytes,
                    peakAllocatedBytes = peakAllocatedBytes,
                    finalSnapshot = finalSnapshot
                };
                var directory = Path.Combine(Application.persistentDataPath, "performance");
                Directory.CreateDirectory(directory);
                var fileName = "player-" + startedUtc.ToString("yyyyMMdd-HHmmss-fffffff") + ".json";
                var destination = Path.Combine(directory, fileName);
                var temporary = destination + ".tmp";
                var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(capture));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporary, destination);
            }
            catch (Exception exception) { Debug.LogError("Performance capture could not be written: " + exception.Message); }
        }

        [Serializable]
        private sealed class Capture
        {
            public int schemaVersion;
            public string target;
            public bool actualPlayerRun;
            public string startedUtc;
            public double durationSeconds;
            public string applicationVersion;
            public string graphicsDevice;
            public float[] frameMs;
            public int totalRecordedFrames;
            public int holds;
            public int computeHolds;
            public long peakManagedBytes;
            public long peakAllocatedBytes;
            public string finalSnapshot;
        }
    }
}
