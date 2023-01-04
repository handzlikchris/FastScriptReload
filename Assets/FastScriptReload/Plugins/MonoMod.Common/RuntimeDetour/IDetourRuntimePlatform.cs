using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.Utils;
using System.Linq;

namespace MonoMod.RuntimeDetour {
#if !MONOMOD_INTERNAL
    public
#endif
    interface IDetourRuntimePlatform {
        MethodBase GetIdentifiable(MethodBase method);
        IntPtr GetNativeStart(MethodBase method);
        MethodInfo CreateCopy(MethodBase method);
        bool TryCreateCopy(MethodBase method, out MethodInfo dm);
        void Pin(MethodBase method);
        void Unpin(MethodBase method);
        MethodBase GetDetourTarget(MethodBase from, MethodBase to);
        uint TryMemAllocScratchCloseTo(IntPtr target, out IntPtr ptr, int size);

        bool OnMethodCompiledWillBeCalled { get; }
        event OnMethodCompiledEvent OnMethodCompiled;
    }

#if !MONOMOD_INTERNAL
    public
#endif
    delegate void OnMethodCompiledEvent(MethodBase method, IntPtr codeStart, ulong codeSize);
}
