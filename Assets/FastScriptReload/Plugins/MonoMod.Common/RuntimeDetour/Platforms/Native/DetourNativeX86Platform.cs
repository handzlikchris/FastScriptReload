using MonoMod.Utils;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MonoMod.RuntimeDetour.Platforms {
#if !MONOMOD_INTERNAL
    public
#endif
    unsafe class DetourNativeX86Platform : IDetourNativePlatform {
        public enum DetourType : byte {
            Rel32,
            Abs32,
            Abs64,
            Abs64Split
        }
        private static readonly uint[] DetourSizes = {
            1 + 4,
            1 + 4 + 1,
            1 + 1 + 4 + 8,
            1 + 1 + 4
        };

        private static bool Is32Bit(long to)
            // JMP rel32 is "sign extended to 64-bits"
            => (((ulong) to) & 0x000000007FFFFFFFUL) == ((ulong) to);

        private static DetourType GetDetourType(IntPtr from, IntPtr to, ref IntPtr extra) {
            long rel = (long) to - ((long) from + 5);
            /* Note: Check -rel as well, as f.e. FFFFFFFFF58545C0 -> FFFFFFFFF5827030 ends up with rel = FFFFFFFFFFFD2A6B
             * This is critical for some 32-bit environments, as in that case, an Abs64 detour gets emitted on x86 instead!
             * Checking for -rel ensures that backwards jumps are handled properly as well, using Rel32 detours.
             */
            if (Is32Bit(rel) || Is32Bit(-rel)) {
                unsafe {
                    if (*((byte*) from + 5) != 0x5f) // because Rel32 uses an E9 jump, the byte that would be immediately following the jump
                        return DetourType.Rel32;     //   must not be 0x5f, otherwise it would be picked up by DetourRuntimeNETPlatform line 130
                }
            }

            if (Is32Bit((long) to))
                return DetourType.Abs32;

            // Sometimes we can use runtime-specific tricks to allocate some memory close to the from pointer.
            // This only works for managed methods, but it helps with avoiding overwriting adjacent memory.
            if ((DetourHelper.Runtime?.TryMemAllocScratchCloseTo(from, out extra, 8) ?? 0) >= 8) {
                rel = (long) extra - ((long) from + 6);
                if (Is32Bit(rel) || Is32Bit(-rel))
                    return DetourType.Abs64Split;
            }

            return DetourType.Abs64;
        }

        public NativeDetourData Create(IntPtr from, IntPtr to, byte? type) {
            NativeDetourData detour = new NativeDetourData {
                Method = from,
                Target = to
            };
            detour.Size = DetourSizes[detour.Type = type ?? (byte) GetDetourType(from, to, ref detour.Extra)];
            // Console.WriteLine($"{nameof(DetourNativeX86Platform)} create: {(DetourType) detour.Type} 0x{detour.Method.ToString("X16")} + 0x{detour.Size.ToString("X8")} -> 0x{detour.Target.ToString("X16")}");
            return detour;
        }

        public void Free(NativeDetourData detour) {
            if ((DetourType) detour.Type == DetourType.Abs64Split) {
                // There's currently no way to free the scratch mem.
            }
        }

        public void Apply(NativeDetourData detour) {
            int offs = 0;

            // Console.WriteLine($"{nameof(DetourNativeX86Platform)} apply: {(DetourType) detour.Type} 0x{detour.Method.ToString("X16")} -> 0x{detour.Target.ToString("X16")}");
            switch ((DetourType) detour.Type) {
                case DetourType.Rel32:
                    // JMP DeltaNextInstr
                    detour.Method.Write(ref offs, (byte) 0xE9);
                    detour.Method.Write(ref offs, (uint) (int) (
                        (long) detour.Target - ((long) detour.Method + offs + sizeof(uint))
                    ));
                    break;

                case DetourType.Abs32:
                    // Registerless PUSH + RET "absolute jump."
                    // PUSH <to>
                    detour.Method.Write(ref offs, (byte) 0x68);
                    detour.Method.Write(ref offs, (uint) detour.Target);
                    // RET
                    detour.Method.Write(ref offs, (byte) 0xC3);
                    break;

                case DetourType.Abs64:
                case DetourType.Abs64Split:
                    // PUSH can only push 32-bit values and MOV RAX, <to>; JMP RAX voids RAX.
                    // Registerless JMP [rip+0] + data "absolute jump."
                    // JMP [rip+0]
                    detour.Method.Write(ref offs, (byte) 0xFF);
                    detour.Method.Write(ref offs, (byte) 0x25);
                    if ((DetourType) detour.Type == DetourType.Abs64Split) {
                        detour.Method.Write(ref offs, (uint) (int) (
                            (long) detour.Extra - ((long) detour.Method + offs + sizeof(uint))
                        ));
                        // <to>
                        offs = 0;
                        detour.Extra.Write(ref offs, (ulong) detour.Target);
                    } else {
                        detour.Method.Write(ref offs, (uint) 0x00000000);
                        // <to>
                        detour.Method.Write(ref offs, (ulong) detour.Target);
                    }
                    break;

                default:
                    throw new NotSupportedException($"Unknown detour type {detour.Type}");
            }
        }

        public void Copy(IntPtr src, IntPtr dst, byte type) {
            switch ((DetourType) type) {
                case DetourType.Rel32:
                    *(uint*) ((long) dst) = *(uint*) ((long) src);
                    *(byte*) ((long) dst + 4) = *(byte*) ((long) src + 4);
                    break;

                case DetourType.Abs32:
                case DetourType.Abs64Split:
                    *(uint*) ((long) dst) = *(uint*) ((long) src);
                    *(ushort*) ((long) dst + 4) = *(ushort*) ((long) src + 4);
                    break;

                case DetourType.Abs64:
                    *(ulong*) ((long) dst) = *(ulong*) ((long) src);
                    *(uint*) ((long) dst + 8) = *(uint*) ((long) src + 8);
                    *(ushort*) ((long) dst + 12) = *(ushort*) ((long) src + 12);
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

        public void FlushICache(IntPtr src, uint size) {
            // On X86, apparently a call / ret is enough to flush the entire cache.
        }

        public IntPtr MemAlloc(uint size) {
            return Marshal.AllocHGlobal((int) size);
        }

        public void MemFree(IntPtr ptr) {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
