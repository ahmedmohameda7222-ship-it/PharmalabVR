using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

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
        private CoreSafeHandle() : base(true) { }
        protected override bool ReleaseHandle() => NativeMethods.DestroyRaw((ulong)handle.ToInt64()) == NativeStatus.Ok;
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
