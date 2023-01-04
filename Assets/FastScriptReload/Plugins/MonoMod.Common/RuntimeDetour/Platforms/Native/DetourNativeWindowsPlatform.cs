using MonoMod.Utils;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MonoMod.RuntimeDetour.Platforms {
#if !MONOMOD_INTERNAL
    public
#endif
    unsafe class DetourNativeWindowsPlatform : IDetourNativePlatform {
        private readonly IDetourNativePlatform Inner;

        public DetourNativeWindowsPlatform(IDetourNativePlatform inner) {
            Inner = inner;
        }

        public void MakeWritable(IntPtr src, uint size) {
            // READWRITE causes an AccessViolationException / TargetInvocationException.
            if (!VirtualProtect(src, (IntPtr) size, PAGE.EXECUTE_READWRITE, out _)) {
                throw LogAllSections(Marshal.GetLastWin32Error(), "MakeWriteable", src, size);
            }
        }

        public void MakeExecutable(IntPtr src, uint size) {
            if (!VirtualProtect(src, (IntPtr) size, PAGE.EXECUTE_READWRITE, out _)) {
                throw LogAllSections(Marshal.GetLastWin32Error(), "MakeExecutable", src, size);
            }
        }

        public void MakeReadWriteExecutable(IntPtr src, uint size) {
            if (!VirtualProtect(src, (IntPtr) size, PAGE.EXECUTE_READWRITE, out _)) {
                throw LogAllSections(Marshal.GetLastWin32Error(), "MakeExecutable", src, size);
            }
        }

        public void FlushICache(IntPtr src, uint size) {
            if (!FlushInstructionCache(GetCurrentProcess(), src, (UIntPtr) size)) {
                throw LogAllSections(Marshal.GetLastWin32Error(), "FlushICache", src, size);
            }
        }

        private Exception LogAllSections(int error, string from, IntPtr src, uint size) {
            Exception ex = new Win32Exception(error);
            if (MMDbgLog.Writer == null)
                return ex;

            MMDbgLog.Log($"{from} failed for 0x{(long) src:X16} + {size} - logging all memory sections");
            MMDbgLog.Log($"reason: {ex.Message}");

            try {
                IntPtr proc = GetCurrentProcess();
                IntPtr addr = (IntPtr) 0x00000000000010000;
                int i = 0;
                while (true) {
                    if (VirtualQueryEx(proc, addr, out MEMORY_BASIC_INFORMATION infoBasic, sizeof(MEMORY_BASIC_INFORMATION)) == 0)
                        break;

                    ulong srcL = (ulong) src;
                    ulong srcR = srcL + size;
                    ulong infoL = (ulong) infoBasic.BaseAddress;
                    ulong infoR = infoL + (ulong) infoBasic.RegionSize;
                    bool overlap = infoL <= srcR && srcL <= infoR;

                    MMDbgLog.Log($"{(overlap ? "*" : "-")} #{i++}");
                    MMDbgLog.Log($"addr: 0x{(long) infoBasic.BaseAddress:X16}");
                    MMDbgLog.Log($"size: 0x{(long) infoBasic.RegionSize:X16}");
                    MMDbgLog.Log($"aaddr: 0x{(long) infoBasic.AllocationBase:X16}");
                    MMDbgLog.Log($"state: {infoBasic.State}");
                    MMDbgLog.Log($"type: {infoBasic.Type}");
                    MMDbgLog.Log($"protect: {infoBasic.Protect}");
                    MMDbgLog.Log($"aprotect: {infoBasic.AllocationProtect}");

                    try {
                        IntPtr addrPrev = addr;
                        addr = (IntPtr) ((ulong) infoBasic.BaseAddress + (ulong) infoBasic.RegionSize);
                        if ((ulong) addr <= (ulong) addrPrev)
                            break;
                    } catch (OverflowException) {
                        MMDbgLog.Log("overflow");
                        break;
                    }
                }

            } catch {
                throw ex;
            }
            return ex;
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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, IntPtr dwSize, PAGE flNewProtect, out PAGE lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

        [Flags]
        private enum PAGE : uint {
            UNSET,
            NOACCESS =
                0b00000000000000000000000000000001,
            READONLY =
                0b00000000000000000000000000000010,
            READWRITE =
                0b00000000000000000000000000000100,
            WRITECOPY =
                0b00000000000000000000000000001000,
            EXECUTE =
                0b00000000000000000000000000010000,
            EXECUTE_READ =
                0b00000000000000000000000000100000,
            EXECUTE_READWRITE =
                0b00000000000000000000000001000000,
            EXECUTE_WRITECOPY =
                0b00000000000000000000000010000000,
            GUARD =
                0b00000000000000000000000100000000,
            NOCACHE =
                0b00000000000000000000001000000000,
            WRITECOMBINE =
                0b00000000000000000000010000000000,
        }

        private enum MEM : uint {
            UNSET,
            MEM_COMMIT =
                0b00000000000000000001000000000000,
            MEM_RESERVE =
                0b00000000000000000010000000000000,
            MEM_FREE =
                0b00000000000000010000000000000000,
            MEM_PRIVATE =
                0b00000000000000100000000000000000,
            MEM_MAPPED =
                0b00000000000001000000000000000000,
            MEM_IMAGE =
                0b00000001000000000000000000000000,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public PAGE AllocationProtect;
            public IntPtr RegionSize;
            public MEM State;
            public PAGE Protect;
            public MEM Type;
        }

    }
}
