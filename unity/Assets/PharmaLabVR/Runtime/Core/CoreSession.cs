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
        public string BranchId => branchId;
        public static int PendingRetirements => CoreSafeHandle.PendingRetirements;

        public CoreSession(string branchId, string initialMode = "Desktop", string databasePath = "", string databaseIdentity = "",
            string scienceBudgetMode = "Precision", string automaticCheckpointPath = "")
        {
            if (initialMode != "Desktop" && initialMode != "VR") throw new ArgumentOutOfRangeException(nameof(initialMode));
            if (scienceBudgetMode != "Precision" && scienceBudgetMode != "Bulk")
                throw new ArgumentOutOfRangeException(nameof(scienceBudgetMode));
            if (!string.IsNullOrEmpty(databasePath) || !string.IsNullOrEmpty(databaseIdentity))
                ScientificPackage.VerifyPinnedFile(databasePath, databaseIdentity);
            if (NativeMethods.AbiVersion() != 1) throw new NotSupportedException("PharmaLabVR native ABI mismatch.");
            string checkpointFragment = "";
            if (!string.IsNullOrEmpty(automaticCheckpointPath))
            {
                if (automaticCheckpointPath.Length > 1024 ||
                    automaticCheckpointPath.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0 ||
                    !Path.IsPathRooted(automaticCheckpointPath) ||
                    !Directory.Exists(Path.GetDirectoryName(automaticCheckpointPath)))
                    throw new ArgumentException("Checkpoint path requires an existing absolute parent.",
                        nameof(automaticCheckpointPath));
                checkpointFragment = ",\"checkpointPath\":\"" + Escape(automaticCheckpointPath) + "\"";
            }
            var config = Utf8($"{{\"schemaVersion\":1,\"branchId\":\"{Escape(branchId)}\",\"initialMode\":\"{initialMode}\",\"databasePath\":\"{Escape(databasePath)}\",\"databaseIdentity\":\"{Escape(databaseIdentity)}\",\"scienceBudgetMode\":\"{scienceBudgetMode}\"{checkpointFragment}}}");
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
            var package = JsonUtility.FromJson<ScientificPackageEnvelope>(json)?.scientificPackage;
            if (package == null) throw new ScientificPackageMismatchException("Save scientific package is absent.");
            if (expectedScientificDatabasePath != null && package.path != expectedScientificDatabasePath)
                throw new ScientificPackageMismatchException("Save scientific package does not match this installation.");
            if (!string.IsNullOrEmpty(package.path) || !string.IsNullOrEmpty(package.identity))
                ScientificPackage.VerifyPinnedFile(package.path, package.identity);
            else if (expectedScientificDatabasePath != null)
                throw new ScientificPackageMismatchException("Save has no scientific package for live recomputation.");
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

        public void ConfigureAutomaticCheckpoint(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || absolutePath.Length > 1024 ||
                absolutePath.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0 ||
                !Path.IsPathRooted(absolutePath) ||
                !Directory.Exists(Path.GetDirectoryName(absolutePath)))
                throw new ArgumentException("Checkpoint path requires an existing absolute parent.",
                    nameof(absolutePath));
            var bytes = Utf8(absolutePath);
            Ensure(NativeMethods.ConfigureCheckpoint(handle, bytes, (uint)bytes.Length),
                "configure automatic checkpoint");
        }

        // Explicit fsynced Native snapshot checkpoint. The runtime must never
        // equate this with automatic P11.01 journal/checkpoint scheduling.
        public void WriteNativeCheckpoint(string absolutePath)
        {
            if (!IsOpen) throw new ObjectDisposedException(nameof(CoreSession));
            if (string.IsNullOrWhiteSpace(absolutePath) ||
                absolutePath.IndexOf('\0') >= 0 ||
                !Path.IsPathRooted(absolutePath))
                throw new ArgumentException("A valid absolute Native checkpoint path is required.",
                    nameof(absolutePath));
            var bytes = Utf8(absolutePath);
            if (bytes.Length == 0 || bytes.Length > 1024)
                throw new ArgumentOutOfRangeException(nameof(absolutePath));
            Ensure(NativeMethods.Checkpoint(handle, bytes, (uint)bytes.Length), "checkpoint");
        }

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
