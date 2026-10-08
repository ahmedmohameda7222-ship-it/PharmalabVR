using System;
using System.Collections.Generic;
using PharmaLabVR.Input;
using PharmaLabVR.UI;
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
            try
            {
                var initialMode = BootMenu.RequestedMode == ApplicationMode.VirtualReality ? "VR" : "Desktop";
                Session = new CoreSession(branchId, initialMode);
                BootstrapResearchSession();
            }
            catch (Exception exception) { Debug.LogError($"Native core unavailable: {exception.Message}"); enabled = false; }
        }

        private void Update()
        {
            if (Session == null) return;
            accumulator += Time.unscaledDeltaTime;
            if (accumulator > TickSeconds * 2.0)
            {
                var nowNs = NowNs();
                Session.Step(accumulator, nowNs);
                accumulator = 0.0;
                IsTimeHeld = true;
                HoldChanged?.Invoke(true);
                Debug.LogWarning("TimeDiscontinuityHold: more than two transport ticks accumulated.");
                PublishSnapshot();
                return;
            }
            if (IsTimeHeld)
            {
                if (accumulator < TickSeconds) return;
                var heldNowNs = NowNs();
                Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(CollectSamples(heldNowNs)));
                Session.Step(0.0, heldNowNs);
                accumulator = 0.0;
                PublishSnapshot();
                return;
            }
            while (accumulator >= TickSeconds)
            {
                var nowNs = NowNs();
                Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(CollectSamples(nowNs)));
                Session.Step(TickSeconds, nowNs);
                accumulator -= TickSeconds;
                PublishSnapshot();
            }
        }

        public bool ResumeAfterTimeHold()
        {
            var outcomeJson = Session.SubmitOrdered("Continue");
            var outcome = JsonUtility.FromJson<CommandOutcome>(outcomeJson);
            if (outcome == null || !outcome.accepted) return false;
            if (inputAdapterBehaviours != null)
                foreach (var behaviour in inputAdapterBehaviours)
                    if (behaviour is ILabInputAdapter adapter) adapter.ResetBaselines();
            accumulator = 0.0;
            IsTimeHeld = false;
            HoldChanged?.Invoke(false);
            PublishSnapshot();
            return true;
        }

        public bool BeginModeChange(ApplicationMode mode)
        {
            if (Session == null) return false;
            var nativeMode = mode == ApplicationMode.VirtualReality ? "VR" : "Desktop";
            var outcomeJson = Session.SubmitOrdered("BeginModeChange", $"{{\"mode\":\"{nativeMode}\"}}");
            var outcome = JsonUtility.FromJson<CommandOutcome>(outcomeJson);
            if (outcome == null || !outcome.accepted) return false;
            IsTimeHeld = true;
            accumulator = 0.0;
            HoldChanged?.Invoke(true);
            PublishSnapshot();
            return true;
        }

        public bool CreateCheckpoint(string checkpointId)
        {
            if (Session == null) return false;
            var outcome = JsonUtility.FromJson<CommandOutcome>(
                Session.SubmitOrdered("CreateCheckpoint", $"{{\"checkpointId\":\"{EscapeJson(checkpointId)}\"}}"));
            return outcome != null && outcome.accepted;
        }

        public bool RestartCheckpoint(string checkpointId)
        {
            if (Session == null) return false;
            var outcome = JsonUtility.FromJson<CommandOutcome>(
                Session.SubmitOrdered("RestartCheckpoint", $"{{\"checkpointId\":\"{EscapeJson(checkpointId)}\"}}"));
            if (outcome == null || !outcome.accepted) return false;
            IsTimeHeld = true;
            accumulator = 0.0;
            HoldChanged?.Invoke(true);
            PublishSnapshot();
            return true;
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

        private void BootstrapResearchSession()
        {
            RequireAccepted(Session.SubmitOrdered("CreateVessel", "{\"id\":\"source\",\"capacityM3\":0.00005}"));
            RequireAccepted(Session.SubmitOrdered("CreateVessel", "{\"id\":\"receiver\",\"capacityM3\":0.0001}"));
            RequireAccepted(Session.SubmitOrdered("CreateSink", "{\"id\":\"spill\"}"));
            RequireAccepted(Session.SubmitOrdered(
                "PrepareStock",
                "{\"vesselId\":\"source\",\"stockKind\":\"SodiumChloride\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.000025}",
                "{\"source\":\"0\"}"));
            RequireAccepted(Session.SubmitOrdered(
                "PlaceTool",
                "{\"toolId\":\"research-tool\",\"sourceInventoryId\":\"source\",\"overflowSinkId\":\"spill\",\"coordinateFrame\":\"lab\",\"geometryProfileHash\":\"burette-50ml-research-v1\",\"profileRevision\":\"1\"}"));
        }

        private static void RequireAccepted(string outcomeJson)
        {
            var outcome = JsonUtility.FromJson<CommandOutcome>(outcomeJson);
            if (outcome == null || !outcome.accepted)
                throw new InvalidOperationException($"Native setup rejected: {outcome?.code ?? "invalid outcome"}");
        }

        private List<LabInputSample> CollectSamples(ulong nowNs)
        {
            var samples = new List<LabInputSample>();
            if (inputAdapterBehaviours != null)
                foreach (var behaviour in inputAdapterBehaviours)
                    if (behaviour != null && behaviour.isActiveAndEnabled && behaviour is ILabInputAdapter adapter)
                        samples.AddRange(adapter.SampleInputs(nowNs));
            return samples;
        }

        private void PublishSnapshot()
        {
            var snapshot = Session.ReadSnapshot();
            var state = JsonUtility.FromJson<SnapshotEnvelope>(snapshot);
            var held = state?.hold != null && state.hold.active;
            if (held != IsTimeHeld)
            {
                IsTimeHeld = held;
                HoldChanged?.Invoke(held);
            }
            SnapshotChanged?.Invoke(snapshot);
        }

        private static ulong NowNs() => (ulong)(Time.realtimeSinceStartupAsDouble * 1_000_000_000.0);
        private static string EscapeJson(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private sealed class CommandOutcome
        {
            public bool accepted;
            public string code;
        }

        [Serializable]
        private sealed class SnapshotEnvelope
        {
            public HoldEnvelope hold;
        }

        [Serializable]
        private sealed class HoldEnvelope
        {
            public bool active;
        }

        private void OnDisable()
        {
            Session?.Dispose();
            Session = null;
        }
    }
}
