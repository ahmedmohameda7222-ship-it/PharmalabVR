using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UnityEngine;

namespace PharmaLabVR.Core
{
    internal enum NativeStatus : int
    {
        Ok = 0,
        BufferTooSmall = 1,
        NoEvent = 2,
        InvalidArgument = 3,
        InvalidHandle = 4,
        UnsupportedVersion = 5,
        Busy = 6,
        InternalError = 7
    }

    internal sealed class CoreSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private static readonly ConcurrentQueue<ulong> Retired = new();
        private CoreSafeHandle() : base(true) { }
        protected override bool ReleaseHandle()
        {
            var raw = (ulong)handle.ToInt64();
            var status = NativeMethods.DestroyRaw(raw);
            if (status == NativeStatus.Busy)
            {
                // Native retains the context and its engine until the in-flight
                // solve returns. Keep the handle for bounded later retries.
                Retired.Enqueue(raw);
                return true;
            }
            return status == NativeStatus.Ok || status == NativeStatus.InvalidHandle;
        }

        internal static int PendingRetirements => Retired.Count;

        internal static void RetryRetirements()
        {
            var count = Retired.Count;
            for (var index = 0; index < count; index++)
            {
                if (!Retired.TryDequeue(out var raw)) break;
                var status = NativeMethods.DestroyRaw(raw);
                if (status == NativeStatus.Busy) Retired.Enqueue(raw);
                else if (status != NativeStatus.Ok && status != NativeStatus.InvalidHandle)
                    Debug.LogError($"Native session retirement failed: {status}");
            }
        }
    }

    internal sealed class NativeRetirementPump : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var objectName = "PharmaLabVR Native Retirement";
            var existing = GameObject.Find(objectName);
            if (existing != null) return;
            var pump = new GameObject(objectName);
            DontDestroyOnLoad(pump);
            pump.AddComponent<NativeRetirementPump>();
        }

        private void Update() => CoreSafeHandle.RetryRetirements();

        private void OnApplicationQuit()
        {
            CoreSafeHandle.RetryRetirements();
            if (CoreSafeHandle.PendingRetirements > 0)
                Debug.LogWarning("A native scientific solve did not finish before application exit; restart is required.");
        }
    }

    internal static class NativeMethods
    {
        private const string Library = "pharmalab_core";

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "plv_abi_version")]
        internal static extern uint AbiVersion();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "plv_create")]
        internal static extern NativeStatus Create(byte[] config, uint size, out CoreSafeHandle handle);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "plv_destroy")]
        private static extern NativeStatus Destroy(ulong handle);
        internal static NativeStatus DestroyRaw(ulong handle) => Destroy(handle);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "plv_submit")]
        internal static extern NativeStatus Submit(CoreSafeHandle handle, byte[] command, uint size);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "plv_input_batch")]
        internal static extern NativeStatus InputBatch(CoreSafeHandle handle, byte[] samples, uint size);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "plv_step")]
        internal static extern NativeStatus Step(CoreSafeHandle handle, double deltaSeconds, ulong monotonicNowNs);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "plv_snapshot")]
        internal static extern NativeStatus Snapshot(CoreSafeHandle handle, byte[] output, uint capacity, out uint required);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "plv_poll")]
        internal static extern NativeStatus Poll(CoreSafeHandle handle, byte[] output, uint capacity, out uint required);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "plv_export")]
        internal static extern NativeStatus Export(CoreSafeHandle handle, byte[] output, uint capacity, out uint required);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "plv_import")]
        internal static extern NativeStatus Import(byte[] json, uint size, out CoreSafeHandle handle);
    }
}
