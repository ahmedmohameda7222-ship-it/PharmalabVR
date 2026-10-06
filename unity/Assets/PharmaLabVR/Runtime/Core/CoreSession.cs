using System;
using System.Collections.Generic;
using System.Text;

namespace PharmaLabVR.Core
{
    public sealed class CoreSession : IDisposable
    {
        private CoreSafeHandle handle;
        public bool IsOpen => handle != null && !handle.IsInvalid && !handle.IsClosed;

        public CoreSession(string branchId)
        {
            if (NativeMethods.AbiVersion() != 1) throw new NotSupportedException("PharmaLabVR native ABI mismatch.");
            var config = Utf8($"{{\"schemaVersion\":1,\"branchId\":\"{Escape(branchId)}\"}}");
            Ensure(NativeMethods.Create(config, (uint)config.Length, out handle), "create");
        }

        public void Submit(string commandJson) => Ensure(NativeMethods.Submit(handle, Utf8(commandJson), (uint)Utf8(commandJson).Length), "submit");
        public void SubmitInputs(string batchJson) => Ensure(NativeMethods.InputBatch(handle, Utf8(batchJson), (uint)Utf8(batchJson).Length), "input batch");
        public void Step(double deltaSeconds, ulong nowNs) => Ensure(NativeMethods.Step(handle, deltaSeconds, nowNs), "step");
        public string ReadSnapshot() => ReadBuffer(NativeMethods.Snapshot, false);
        public string ExportSession() => ReadBuffer(NativeMethods.Export, false);

        public IReadOnlyList<string> PollEvents()
        {
            var events = new List<string>();
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
    }
}
