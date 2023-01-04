using MonoMod.Utils;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace MonoMod.RuntimeDetour.Platforms {
#if !MONOMOD_INTERNAL
    public
#endif
    unsafe class DetourNativeARMPlatform : IDetourNativePlatform {
        // TODO: Make use of possibly shorter near branches.
        public enum DetourType : byte {
            Thumb,
            ThumbBX,
            AArch32,
            AArch32BX,
            AArch64
        }
        private static readonly uint[] DetourSizes = {
            4 + 4,
            4 + 2 + 2 + 4,
            4 + 4,
            4 + 4 + 4,
            4 + 4 + 8
        };

        // Might be disabled manually when triggering access errors (especially on some armv7 chips).
        // Source: MonoMod Discord server, https://discordapp.com/channels/295566538981769216/295570965663055874/598536798666227712
        public bool ShouldFlushICache = true;

        private static DetourType GetDetourType(IntPtr from, IntPtr to) {
            if (IntPtr.Size >= 8)
                return DetourType.AArch64;

            // The lowest bit is set for Thumb, unset for ARM.
            bool fromThumb = ((long) from & 0x1) == 0x1;
            bool toThumb = ((long) to & 0x1) == 0x1;
            if (fromThumb) {
                if (toThumb) {
                    return DetourType.Thumb;
                } else {
                    return DetourType.ThumbBX;
                }
            } else {
                if (toThumb) {
                    return DetourType.AArch32BX;
                } else {
                    return DetourType.AArch32;
                }
            }
        }

        public NativeDetourData Create(IntPtr from, IntPtr to, byte? type) {
            NativeDetourData detour = new NativeDetourData {
                Method = (IntPtr) ((long) from & ~0x1),
                Target = (IntPtr) ((long) to & ~0x1)
            };
            detour.Size = DetourSizes[detour.Type = type ?? (byte) GetDetourType(from, to)];
            // Console.WriteLine($"{nameof(DetourNativeARMPlatform)} create: {(DetourType) detour.Type} 0x{detour.Method.ToString("X16")} + 0x{detour.Size.ToString("X8")} -> 0x{detour.Target.ToString("X16")}");
            return detour;
        }

        public void Free(NativeDetourData detour) {
            // No extra data.
        }

        public void Apply(NativeDetourData detour) {
            int offs = 0;

            // Console.WriteLine($"{nameof(DetourNativeARMPlatform)} apply: {(DetourType) detour.Type} 0x{detour.Method.ToString("X16")} -> 0x{detour.Target.ToString("X16")}");
            switch ((DetourType) detour.Type) {
                case DetourType.Thumb:
                    // Note: PC is 4 bytes ahead
                    // LDR.W PC, [PC, #0]
                    detour.Method.Write(ref offs, (byte) 0xDF);
                    detour.Method.Write(ref offs, (byte) 0xF8);
                    detour.Method.Write(ref offs, (byte) 0x00);
                    detour.Method.Write(ref offs, (byte) 0xF0);
                    // <to> | 0x1 (-> Thumb)
                    detour.Method.Write(ref offs, (uint) detour.Target | 0x1);
                    break;

                case DetourType.ThumbBX:
                    // Burn a register to stay safe.
                    // Note: PC is 4 bytes ahead
                    // LDR.W R10, [PC, #4]
                    detour.Method.Write(ref offs, (byte) 0xDF);
                    detour.Method.Write(ref offs, (byte) 0xF8);
                    detour.Method.Write(ref offs, (byte) 0x04);
                    detour.Method.Write(ref offs, (byte) 0xA0);
                    // BX R10
                    detour.Method.Write(ref offs, (byte) 0x50);
                    detour.Method.Write(ref offs, (byte) 0x47);
                    // NOP
                    detour.Method.Write(ref offs, (byte) 0x00);
                    detour.Method.Write(ref offs, (byte) 0xBF);
                    // <to> | 0x0 (-> ARM)
                    detour.Method.Write(ref offs, (uint) detour.Target | 0x0);
                    break;

                case DetourType.AArch32:
                    // Note: PC is 8 bytes ahead
                    // LDR PC, [PC, #-4]
                    detour.Method.Write(ref offs, (byte) 0x04);
                    detour.Method.Write(ref offs, (byte) 0xF0);
                    detour.Method.Write(ref offs, (byte) 0x1F);
                    detour.Method.Write(ref offs, (byte) 0xE5);
                    // <to> | 0x0 (-> ARM)
                    detour.Method.Write(ref offs, (uint) detour.Target | 0x0);
                    break;

                case DetourType.AArch32BX:
                    // Burn a register. Required to use BX to change state.
                    // Note: PC is 4 bytes ahead
                    // LDR R8, [PC, #0]
                    detour.Method.Write(ref offs, (byte) 0x00);
                    detour.Method.Write(ref offs, (byte) 0x80);
                    detour.Method.Write(ref offs, (byte) 0x9F);
                    detour.Method.Write(ref offs, (byte) 0xE5);
                    // BX R8
                    detour.Method.Write(ref offs, (byte) 0x18);
                    detour.Method.Write(ref offs, (byte) 0xFF);
                    detour.Method.Write(ref offs, (byte) 0x2F);
                    detour.Method.Write(ref offs, (byte) 0xE1);
                    // <to> | 0x1 (-> Thumb)
                    detour.Method.Write(ref offs, (uint) detour.Target | 0x1);
                    break;

                case DetourType.AArch64:
                    // PC isn't available on arm64.
                    // We need to burn a register and branch instead.
                    // LDR X15, .+8
                    detour.Method.Write(ref offs, (byte) 0x4F);
                    detour.Method.Write(ref offs, (byte) 0x00);
                    detour.Method.Write(ref offs, (byte) 0x00);
                    detour.Method.Write(ref offs, (byte) 0x58);
                    // BR X15
                    detour.Method.Write(ref offs, (byte) 0xE0);
                    detour.Method.Write(ref offs, (byte) 0x01);
                    detour.Method.Write(ref offs, (byte) 0x1F);
                    detour.Method.Write(ref offs, (byte) 0xD6);
                    // <to>
                    detour.Method.Write(ref offs, (ulong) detour.Target);
                    break;

                default:
                    throw new NotSupportedException($"Unknown detour type {detour.Type}");
            }
        }

        public void Copy(IntPtr src, IntPtr dst, byte type) {
            switch ((DetourType) type) {
                case DetourType.Thumb:
                    *(uint*) ((long) dst) = *(uint*) ((long) src);
                    *(uint*) ((long) dst + 4) = *(uint*) ((long) src + 4);
                    break;

                case DetourType.ThumbBX:
                    *(uint*) ((long) dst) = *(uint*) ((long) src);
                    *(ushort*) ((long) dst + 4) = *(ushort*) ((long) src + 4);
                    *(ushort*) ((long) dst + 6) = *(ushort*) ((long) src + 6);
                    *(uint*) ((long) dst + 8) = *(uint*) ((long) src + 8);
                    break;

                case DetourType.AArch32:
                    *(uint*) ((long) dst) = *(uint*) ((long) src);
                    *(uint*) ((long) dst + 4) = *(uint*) ((long) src + 4);
                    break;

                case DetourType.AArch32BX:
                    *(uint*) ((long) dst) = *(uint*) ((long) src);
                    *(uint*) ((long) dst + 4) = *(uint*) ((long) src + 4);
                    *(uint*) ((long) dst + 8) = *(uint*) ((long) src + 8);
                    break;

                case DetourType.AArch64:
                    *(uint*) ((long) dst) = *(uint*) ((long) src);
                    *(uint*) ((long) dst + 4) = *(uint*) ((long) src + 4);
                    *(ulong*) ((long) dst + 8) = *(ulong*) ((long) src + 8);
                    break;

                default:
                    throw new NotSupportedException($"Unknown detour type {type}");
            }
        }

        public void MakeWritable(IntPtr src, uint size) {
            // no-op.
        }

        public void MakeExecutable(IntPtr src, uint size) {
            // no-op.
        }

        public void MakeReadWriteExecutable(IntPtr src, uint size) {
            // no-op.
        }

        public unsafe void FlushICache(IntPtr src, uint size) {
            // On ARM, we must flush the instruction cache.
            // Sadly, mono_arch_flush_icache isn't reliably exported.
            // This thus requires running native code to invoke the syscall.

            if (!ShouldFlushICache)
                return;

            // Emit a native delegate once. It lives as long as the application.
            // It'd be ironic if the flush function would need to be flushed itself...
            byte[] code = IntPtr.Size >= 8 ? _FlushCache64 : _FlushCache32;
            fixed (byte* ptr = code) {
                DetourHelper.Native.MakeExecutable((IntPtr) ptr, (uint) code.Length);
                (Marshal.GetDelegateForFunctionPointer((IntPtr) ptr, typeof(d_flushicache)) as d_flushicache)(src, size);
            }
        }

        public IntPtr MemAlloc(uint size) {
            return Marshal.AllocHGlobal((int) size);
        }

        public void MemFree(IntPtr ptr) {
            Marshal.FreeHGlobal(ptr);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int d_flushicache(IntPtr code, ulong size);

        // The following tools were used to obtain the shellcode.
        // https://godbolt.org/ ARM(64) gcc 8.2
        // http://shell-storm.org/online/Online-Assembler-and-Disassembler/
        // http://alexaltea.github.io/keystone.js/ .map(x => "0x" + x.toString(16).padStart(2, "0")).join(", ")

        /* -O2 -fPIE -march=armv6
        // On ARM non-64, apparently SVC is the newer "Unified Assembler Language" (UAL) syntax.
        // In older versions of mono (before they fully switched to __clear_cache), it was only used for Android.
        // Adapted from mono's instruction flushing code.
        // https://github.com/mono/mono/blob/d2acc1d780d40f0a418347181c5adab533944d90/mono/mini/mini-arm.c#L1195
        void flushicache(void* code, unsigned long size) {
	        const int syscall = 0xf0002;
	        __asm __volatile (
		        "mov	 r0, %0\n"			
		        "mov	 r1, %1\n"
		        "mov	 r7, %2\n"
		        "mov     r2, #0x0\n"
		        "svc     0x00000000\n"
		        :
		        :   "r" (code), "r" (((long) code) + size), "r" (syscall)
		        :   "r0", "r1", "r7", "r2"
		    );
        }
        */
        private readonly byte[] _FlushCache32 = { 0x80, 0x40, 0x2d, 0xe9, 0x00, 0x30, 0xa0, 0xe1, 0x01, 0xc0, 0x80, 0xe0, 0x14, 0xe0, 0x9f, 0xe5, 0x03, 0x00, 0xa0, 0xe1, 0x0c, 0x10, 0xa0, 0xe1, 0x0e, 0x70, 0xa0, 0xe1, 0x00, 0x20, 0xa0, 0xe3, 0x00, 0x00, 0x00, 0xef, 0x80, 0x80, 0xbd, 0xe8, 0x02, 0x00, 0x0f, 0x00 };

        /* -O2 -fPIE -march=armv8-a
        // Adapted from mono's instruction flushing code.
        // https://github.com/mono/mono/blob/cd5e14a3ccaa76e6ba6c58b26823863a2d0a0854/mono/mini/mini-arm64.c#L1997
        void flushicache(void* code, unsigned long size) {
	        unsigned long end = (unsigned long) (((unsigned long) code) + size);
	        unsigned long addr;
	        const unsigned int icache_line_size = 4;
	        const unsigned int dcache_line_size = 4;

	        addr = (unsigned long) code & ~(unsigned long) (dcache_line_size - 1);
	        for (; addr < end; addr += dcache_line_size)
		        asm volatile("dc civac, %0" : : "r" (addr) : "memory");
	        asm volatile("dsb ish" : : : "memory");

	        addr = (unsigned long) code & ~(unsigned long) (icache_line_size - 1);
	        for (; addr < end; addr += icache_line_size)
		        asm volatile("ic ivau, %0" : : "r" (addr) : "memory");

	        asm volatile ("dsb ish" : : : "memory");
	        asm volatile ("isb" : : : "memory");
        }
        */
        private readonly byte[] _FlushCache64 = { 0x01, 0x00, 0x01, 0x8b, 0x00, 0xf4, 0x7e, 0x92, 0x3f, 0x00, 0x00, 0xeb, 0xc9, 0x00, 0x00, 0x54, 0xe2, 0x03, 0x00, 0xaa, 0x22, 0x7e, 0x0b, 0xd5, 0x42, 0x10, 0x00, 0x91, 0x3f, 0x00, 0x02, 0xeb, 0xa8, 0xff, 0xff, 0x54, 0x9f, 0x3b, 0x03, 0xd5, 0x3f, 0x00, 0x00, 0xeb, 0xa9, 0x00, 0x00, 0x54, 0x20, 0x75, 0x0b, 0xd5, 0x00, 0x10, 0x00, 0x91, 0x3f, 0x00, 0x00, 0xeb, 0xa8, 0xff, 0xff, 0x54, 0x9f, 0x3b, 0x03, 0xd5, 0xdf, 0x3f, 0x03, 0xd5, 0xc0, 0x03, 0x5f, 0xd6 };

    }
}
