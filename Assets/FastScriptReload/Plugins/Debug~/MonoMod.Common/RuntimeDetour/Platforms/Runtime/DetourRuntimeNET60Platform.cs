using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.Platforms;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace MonoMod.RuntimeDetour.Platforms {
    // This is based on the Core 3.0 implementation because they are nearly identical, save for how to get the GUID and call convs
#if !MONOMOD_INTERNAL
    public
#endif
    class DetourRuntimeNET60Platform : DetourRuntimeNETCore30Platform {

        // As of .NET 6, this GUID is found at src/coreclr/inc/jiteeversionguid.h as JITEEVersionIdentifier
        public static new readonly Guid JitVersionGuid = new Guid("5ed35c58-857b-48dd-a818-7c0136dc9f73");

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private unsafe delegate CorJitResult d_compileMethod_thiscall(
            IntPtr thisPtr, // ICorJitCompiler*
            IntPtr corJitInfo, // ICorJitInfo*
            in CORINFO_METHOD_INFO methodInfo, // CORINFO_METHOD_INFO*
            uint flags,
            out byte* nativeEntry,
            out uint nativeSizeOfCode
        );

        private unsafe d_compileMethod_thiscall our_compileMethod;
        private d_compileMethod_thiscall real_compileMethod;

        protected override unsafe CorJitResult InvokeRealCompileMethod(
            IntPtr thisPtr, // ICorJitCompiler*
            IntPtr corJitInfo, // ICorJitInfo*
            in CORINFO_METHOD_INFO methodInfo, // CORINFO_METHOD_INFO*
            uint flags,
            out byte* nativeEntry,
            out uint nativeSizeOfCode
        ) {
            if (real_compileMethod == null)
                return base.InvokeRealCompileMethod(thisPtr, corJitInfo, methodInfo, flags, out nativeEntry, out nativeSizeOfCode);

            return real_compileMethod(thisPtr, corJitInfo, methodInfo, flags, out nativeEntry, out nativeSizeOfCode);
        }

        protected override unsafe IntPtr GetCompileMethodHook(IntPtr real) {
            // On .NET 6.0 Windows x86, compileMethod is using thiscall for some yet-to-be-determined reason.
            if (PlatformHelper.Is(Platform.Windows) && IntPtr.Size == 4) {
                real_compileMethod = real.AsDelegate<d_compileMethod_thiscall>();
                our_compileMethod = CompileMethodHook;
                IntPtr our_compileMethodPtr = Marshal.GetFunctionPointerForDelegate(our_compileMethod);

                // Create a native trampoline to pre-JIT the hook itself
                {
                    NativeDetourData trampolineData = CreateNativeTrampolineTo(our_compileMethodPtr);
                    d_compileMethod_thiscall trampoline = trampolineData.Method.AsDelegate<d_compileMethod_thiscall>();
                    trampoline(IntPtr.Zero, IntPtr.Zero, new CORINFO_METHOD_INFO(), 0, out _, out _);
                    FreeNativeTrampoline(trampolineData);
                }

                return our_compileMethodPtr;
            }

            return base.GetCompileMethodHook(real);
        }

    }
}
