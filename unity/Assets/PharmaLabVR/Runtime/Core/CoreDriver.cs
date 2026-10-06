using System;
using UnityEngine;

namespace PharmaLabVR.Core
{
    public sealed class CoreDriver : MonoBehaviour
    {
        [SerializeField] private string branchId = "local-session";
        public event Action<string> SnapshotChanged;
        public CoreSession Session { get; private set; }
        private double accumulator;
        private const double TickSeconds = 0.020;

        private void OnEnable()
        {
            try { Session = new CoreSession(branchId); }
            catch (Exception exception) { Debug.LogError($"Native core unavailable: {exception.Message}"); enabled = false; }
        }

        private void Update()
        {
            if (Session == null) return;
            accumulator += Time.unscaledDeltaTime;
            if (accumulator > TickSeconds * 2.0)
            {
                accumulator = 0.0;
                Debug.LogWarning("TimeDiscontinuityHold: more than two transport ticks accumulated.");
                return;
            }
            while (accumulator >= TickSeconds)
            {
                Session.Step(TickSeconds, (ulong)(Time.realtimeSinceStartupAsDouble * 1_000_000_000.0));
                accumulator -= TickSeconds;
                SnapshotChanged?.Invoke(Session.ReadSnapshot());
            }
        }

        private void OnDisable()
        {
            Session?.Dispose();
            Session = null;
        }
    }
}
