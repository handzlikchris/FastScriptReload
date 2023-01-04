using System;
using System.Reflection;
using System.Linq.Expressions;
using MonoMod.Utils;
using Mono.Cecil.Cil;

namespace MonoMod.RuntimeDetour {
    /// <summary>
    /// The data forming a "raw" native detour, created and consumed by DetourManager.Native.
    /// </summary>
#if !MONOMOD_INTERNAL
    public
#endif
    struct NativeDetourData {
        /// <summary>
        /// The method to detour from. Set when the structure is created by the IDetourNativePlatform.
        /// </summary>
        public IntPtr Method;
        /// <summary>
        /// The target method to be called instead. Set when the structure is created by the IDetourNativePlatform.
        /// </summary>
        public IntPtr Target;

        /// <summary>
        /// The type of the detour. Determined when the structure is created by the IDetourNativePlatform.
        /// </summary>
        public byte Type;

        /// <summary>
        /// The size of the detour. Calculated when the structure is created by the IDetourNativePlatform.
        /// </summary>
        public uint Size;

        /// <summary>
        /// DetourManager.Native-specific data.
        /// </summary>
        public IntPtr Extra;
    }
}
