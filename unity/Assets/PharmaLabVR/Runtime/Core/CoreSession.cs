using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace PharmaLabVR.Core
{
    public sealed class CoreSession : IDisposable
    {
        private CoreSafeHandle handle;
        private readonly string branchId;
        private ulong nextCommandSequence;
        private readonly Queue<string> pendingEvents = new();
        public bool IsOpen => handle != null && !handle.IsInvalid && !handle.IsClosed;
        public static int PendingRetirements => CoreSafeHandle.PendingRetirements;

        public CoreSession(string branchId, string initialMode = "Desktop", string databasePath = "", string databaseIdentity = "")
        {
            if (initialMode != "Desktop" && initialMode != "VR") throw new ArgumentOutOfRangeException(nameof(initialMode));
            if (NativeMethods.AbiVersion() != 1) throw new NotSupportedException("PharmaLabVR native ABI mismatch.");
            var config = Utf8($"{{\"schemaVersion\":1,\"branchId\":\"{Escape(branchId)}\",\"initialMode\":\"{initialMode}\",\"databasePath\":\"{Escape(databasePath)}\",\"databaseIdentity\":\"{Escape(databaseIdentity)}\"}}");
            Ensure(NativeMethods.Create(config, (uint)config.Length, out handle), "create");
            this.branchId = branchId;
            nextCommandSequence = 1;
        }

        private CoreSession(CoreSafeHandle importedHandle, string importedBranchId, ulong importedNextSequence)
        {
            handle = importedHandle;
            branchId = importedBranchId;
            nextCommandSequence = importedNextSequence;
        }

        public static CoreSession ImportSession(string json, string expectedScientificDatabasePath = null)
        {
            if (NativeMethods.AbiVersion() != 1) throw new NotSupportedException("PharmaLabVR native ABI mismatch.");
            if (expectedScientificDatabasePath != null)
            {
                var package = JsonUtility.FromJson<ScientificPackageEnvelope>(json)?.scientificPackage;
                if (package == null || package.path != expectedScientificDatabasePath ||
                    package.identity != ScientificPackage.DatabaseSha256)
                    throw new InvalidDataException("Save scientific package does not match this installation.");
                ScientificPackage.Verify(File.ReadAllBytes(expectedScientificDatabasePath));
            }
            var bytes = Utf8(json);
            Ensure(NativeMethods.Import(bytes, (uint)bytes.Length, out var imported), "import");
            var envelope = JsonUtility.FromJson<SessionEnvelope>(json);
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.branchId) ||
                !ulong.TryParse(envelope.nextCommandSequence, out var nextSequence))
            {
                imported.Dispose();
                throw new InvalidOperationException("Imported session identity is invalid.");
            }
            return new CoreSession(imported, envelope.branchId, nextSequence);
        }

        public string SubmitOrdered(string type, string payloadJson = "{}", string expectedMaterialRevisionsJson = "{}")
        {
            var sequence = nextCommandSequence.ToString();
            var command = $"{{\"schemaVersion\":1,\"branchId\":\"{Escape(branchId)}\",\"commandSequence\":\"{sequence}\",\"type\":\"{Escape(type)}\",\"expectedMaterialRevisions\":{expectedMaterialRevisionsJson},\"payload\":{payloadJson}}}";
            Submit(command);
            while (true)
            {
                var value = ReadBuffer(NativeMethods.Poll, true);
                if (value == null) break;
                var outcome = JsonUtility.FromJson<CommandOutcomeEnvelope>(value);
                if (outcome != null && outcome.type == "CommandOutcome" && outcome.commandSequence == sequence)
                {
                    nextCommandSequence++;
                    return value;
                }
                pendingEvents.Enqueue(value);
            }
            throw new InvalidOperationException("Native command did not emit a terminal outcome.");
        }

        public void Submit(string commandJson)
        {
            var bytes = Utf8(commandJson);
            Ensure(NativeMethods.Submit(handle, bytes, (uint)bytes.Length), "submit");
        }
        public void SubmitInputs(string batchJson)
        {
            var bytes = Utf8(batchJson);
            Ensure(NativeMethods.InputBatch(handle, bytes, (uint)bytes.Length), "input batch");
        }
        public void Step(double deltaSeconds, ulong nowNs) => Ensure(NativeMethods.Step(handle, deltaSeconds, nowNs), "step");
        public string ReadSnapshot() => ReadBuffer(NativeMethods.Snapshot, false);
        public string ExportSession() => ReadBuffer(NativeMethods.Export, false);

        public IReadOnlyList<string> PollEvents()
        {
            var events = new List<string>();
            while (pendingEvents.Count > 0) events.Add(pendingEvents.Dequeue());
            while (true)
            {
                var value = ReadBuffer(NativeMethods.Poll, true);
                if (value == null) return events;
                events.Add(value);
            }
        }

        private delegate NativeStatus BufferCall(CoreSafeHandle value, byte[] output, uint capacity, out uint required);

        private string ReadBuffer(BufferCall call, bool allowNoEvent)
        {
            byte[] buffer = null;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var status = call(handle, buffer, (uint)(buffer?.Length ?? 0), out var required);
                if (allowNoEvent && status == NativeStatus.NoEvent) return null;
                if (status == NativeStatus.Ok) return Encoding.UTF8.GetString(buffer, 0, checked((int)required - 1));
                if (status != NativeStatus.BufferTooSmall || required == 0 || required > 16 * 1024 * 1024) Ensure(status, "buffer read");
                buffer = new byte[required];
            }
            throw new InvalidOperationException("Native snapshot changed size repeatedly.");
        }

        private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value ?? throw new ArgumentNullException(nameof(value)));
        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        private static void Ensure(NativeStatus status, string operation)
        {
            if (status != NativeStatus.Ok) throw new InvalidOperationException($"Native {operation} failed: {status}");
        }

        public void Dispose()
        {
            handle?.Dispose();
            handle = null;
        }

        [Serializable]
        private sealed class SessionEnvelope
        {
            public string branchId;
            public string nextCommandSequence;
        }

        [Serializable]
        private sealed class ScientificPackageEnvelope
        {
            public ScientificPackageRecord scientificPackage;
        }

        [Serializable]
        private sealed class ScientificPackageRecord
        {
            public string path;
            public string identity;
        }

        [Serializable]
        private sealed class CommandOutcomeEnvelope
        {
            public string type;
            public string commandSequence;
        }
    }
}
