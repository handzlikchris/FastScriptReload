#pragma warning disable IDE1006 // Naming Styles


using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MonoMod.RuntimeDetour.Platforms {
#if !MONOMOD_INTERNAL
    public
#endif
    unsafe class DetourNativeMonoPlatform : IDetourNativePlatform {
        private readonly IDetourNativePlatform Inner;

        private readonly long _Pagesize;

        public DetourNativeMonoPlatform(IDetourNativePlatform inner, string libmono) {
            Inner = inner;

            Dictionary<string, List<DynDllMapping>> mappings = new Dictionary<string, List<DynDllMapping>>();
            if (!string.IsNullOrEmpty(libmono))
                mappings.Add("mono", new List<DynDllMapping>() { libmono });
            DynDll.ResolveDynDllImports(this, mappings);

            _Pagesize = mono_pagesize();
        }

        private unsafe void SetMemPerms(IntPtr start, ulong len, MmapProts prot) {
            long pagesize = _Pagesize;
            long startPage = ((long) start) & ~(pagesize - 1);
            long endPage = ((long) start + (long) len + pagesize - 1) & ~(pagesize - 1);

            if (mono_mprotect((IntPtr) startPage, (IntPtr) (endPage - startPage), (int) prot) != 0) {
                int error = Marshal.GetLastWin32Error();
                if (error == 0) {
                    // This can happen on some Android devices.
                    // Let's hope for the best.
                } else {
                    throw new Win32Exception();
                }
            }
        }

        public void MakeWritable(IntPtr src, uint size) {
            // RWX because old versions of mono always use RWX.
            SetMemPerms(src, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC);
        }

        public void MakeExecutable(IntPtr src, uint size) {
            // RWX because old versions of mono always use RWX.
            SetMemPerms(src, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC);
        }

        public void MakeReadWriteExecutable(IntPtr src, uint size) {
            SetMemPerms(src, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC);
        }

        public void FlushICache(IntPtr src, uint size) {
            // mono_arch_flush_icache isn't reliably exported.
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

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int d_mono_pagesize();
        [DynDllImport("mono")]
        private d_mono_pagesize mono_pagesize;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, SetLastError = true)]
        private delegate int d_mono_mprotect(IntPtr addr, IntPtr length, int flags);
        [DynDllImport("mono")]
        private d_mono_mprotect mono_mprotect;

#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value null

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
