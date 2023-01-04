#pragma warning disable IDE1006 // Naming Styles

using MonoMod.Utils;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MonoMod.RuntimeDetour.Platforms {
#if !MONOMOD_INTERNAL
    public
#endif
    unsafe class DetourNativeLibcPlatform : IDetourNativePlatform {
        private readonly IDetourNativePlatform Inner;

        private readonly long _Pagesize;

        public DetourNativeLibcPlatform(IDetourNativePlatform inner) {
            Inner = inner;

            // Environment.SystemPageSize is part of .NET Framework 4.0+ and .NET Standard 2.0+
#if NETSTANDARD
            _Pagesize = Environment.SystemPageSize;
#else
            PropertyInfo p_SystemPageSize = typeof(Environment).GetProperty("SystemPageSize");
            if (p_SystemPageSize == null)
                throw new NotSupportedException("Unsupported runtime");
            _Pagesize = (int) p_SystemPageSize.GetValue(null, new object[0]);
#endif
        }

        private unsafe void SetMemPerms(IntPtr start, ulong len, MmapProts prot) {
            long pagesize = _Pagesize;
            long startPage = ((long) start) & ~(pagesize - 1);
            long endPage = ((long) start + (long) len + pagesize - 1) & ~(pagesize - 1);

            if (mprotect((IntPtr) startPage, (IntPtr) (endPage - startPage), prot) != 0)
                throw new Win32Exception();
        }

        public void MakeWritable(IntPtr src, uint size) {
            // RWX for sanity.
            SetMemPerms(src, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC);
        }

        public void MakeExecutable(IntPtr src, uint size) {
            // RWX for sanity.
            SetMemPerms(src, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC);
        }

        public void MakeReadWriteExecutable(IntPtr src, uint size) {
            SetMemPerms(src, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC);
        }

        public void FlushICache(IntPtr src, uint size) {
            // The cache would be flushed via a syscall, which isn't exposed by libc.
            Inner.FlushICache(src, size);
        }

        public NativeDetourData Create(IntPtr from, IntPtr to, byte? type) {
            return Inner.Create(from, to, type);
        }

        public void Free(NativeDetourData detour) {
            Inner.Free(detour);
        }

        public void Apply(NativeDetourData detour) {
            Inner.Apply(detour);
        }

        public void Copy(IntPtr src, IntPtr dst, byte type) {
            Inner.Copy(src, dst, type);
        }

        public IntPtr MemAlloc(uint size) {
            return Inner.MemAlloc(size);
        }

        public void MemFree(IntPtr ptr) {
            Inner.MemFree(ptr);
        }

        [DllImport("libc", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mprotect(IntPtr start, IntPtr len, MmapProts prot);

        [Flags]
        private enum MmapProts : int {
            PROT_READ = 0x1,
            PROT_WRITE = 0x2,
            PROT_EXEC = 0x4,
            PROT_NONE = 0x0,
            PROT_GROWSDOWN = 0x01000000,
            PROT_GROWSUP = 0x02000000,
        }
    }
}
