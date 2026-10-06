using System;
using System.Collections.Generic;
using PharmaLabVR.Input;
using UnityEngine;

namespace PharmaLabVR.Core
{
    public sealed class CoreDriver : MonoBehaviour
    {
        [SerializeField] private string branchId = "local-session";
        [SerializeField] private MonoBehaviour[] inputAdapterBehaviours;
        public event Action<string> SnapshotChanged;
        public event Action<bool> HoldChanged;
        public CoreSession Session { get; private set; }
        public bool IsTimeHeld { get; private set; }
        private double accumulator;
        private const double TickSeconds = 0.020;

        public void ConfigureInputs(params MonoBehaviour[] adapters) => inputAdapterBehaviours = adapters;

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
                IsTimeHeld = true;
                HoldChanged?.Invoke(true);
                Debug.LogWarning("TimeDiscontinuityHold: more than two transport ticks accumulated.");
                return;
            }
            if (IsTimeHeld) return;
            while (accumulator >= TickSeconds)
            {
                var nowNs = (ulong)(Time.realtimeSinceStartupAsDouble * 1_000_000_000.0);
                var samples = new List<LabInputSample>();
                if (inputAdapterBehaviours != null)
                    foreach (var behaviour in inputAdapterBehaviours)
                        if (behaviour != null && behaviour.isActiveAndEnabled && behaviour is ILabInputAdapter adapter)
                            samples.AddRange(adapter.SampleInputs(nowNs));
                Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(samples));
                Session.Step(TickSeconds, nowNs);
                accumulator -= TickSeconds;
                SnapshotChanged?.Invoke(Session.ReadSnapshot());
            }
        }

        public void ResumeAfterTimeHold()
        {
            accumulator = 0.0;
            IsTimeHeld = false;
            HoldChanged?.Invoke(false);
        }

        public void ReplaceSession(CoreSession replacement)
        {
            if (replacement == null || !replacement.IsOpen) throw new ArgumentException("Replacement session is not open.", nameof(replacement));
            var previous = Session;
            Session = replacement;
            accumulator = 0.0;
            IsTimeHeld = false;
            previous?.Dispose();
            SnapshotChanged?.Invoke(Session.ReadSnapshot());
        }

        private void OnDisable()
        {
            Session?.Dispose();
            Session = null;
        }
    }
}
