using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using MonoMod.Utils;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace MonoMod.RuntimeDetour.Platforms {
#if !MONOMOD_INTERNAL
    public
#endif
    class DetourRuntimeNETCorePlatform : DetourRuntimeNETPlatform {

        // All of this stuff is for JIT hooking in RuntimeDetour so we can update hooks when a method is re-jitted
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr d_getJit();
        private static d_getJit getJit;

        public DetourRuntimeNETCorePlatform() {
            // Apparently no dirty additionally separate call convention hackery is necessary on .NET Core..?
            // Would be great if someone actually had the time to verify this.
            GlueThiscallInStructRetPtr = GlueThiscallStructRetPtr;
        }

        protected static IntPtr GetJitObject() {
            if (getJit == null) {
                // To make sure we get the right clrjit, we enumerate the process's modules and find the one 
                //   with the name we care about, then use its full path to gat a handle and load symbols.
                Process currentProc = Process.GetCurrentProcess();
                ProcessModule clrjitModule = currentProc.Modules.Cast<ProcessModule>()
                    .FirstOrDefault(m => Path.GetFileNameWithoutExtension(m.FileName).EndsWith("clrjit", StringComparison.Ordinal));
                if (clrjitModule == null)
                    throw new PlatformNotSupportedException();

                if (!DynDll.TryOpenLibrary(clrjitModule.FileName, out IntPtr clrjitPtr))
                    throw new PlatformNotSupportedException();

                if (PlatformHelper.Is(Platform.Windows)) {
                    // we can use this check only on Windows, because only Windows actually has FilveVersionInfo
                    // this is preferred because it checks the version of the JIT, rather than the runtime library
                    //   which is what actually determines the layout.
                    isNet5Jit = clrjitModule.FileVersionInfo.ProductMajorPart >= 5;
                } else {
                    // this gets System.Private.CorLib's major version, which *should* match the runtime version
                    // it is the only method we have at the moment to detect it on non-Windows platforms
                    isNet5Jit = typeof(object).Assembly.GetName().Version.Major >= 5;
                }

                try {
                    getJit = clrjitPtr.GetFunction(nameof(getJit)).AsDelegate<d_getJit>();
                } catch {
                    DynDll.CloseLibrary(clrjitPtr);
                    throw;
                }
            }

            return getJit();
        }

        private static bool isNet5Jit;

        protected static Guid GetJitGuid(IntPtr jit) {
            int getVersionIdentIndex = isNet5Jit ? vtableIndex_ICorJitCompiler_getVersionIdentifier_net5
                                                 : vtableIndex_ICorJitCompiler_getVersionIdentifier;
            d_getVersionIdentifier getVersionIdentifier = ReadObjectVTable(jit, getVersionIdentIndex)
                .AsDelegate<d_getVersionIdentifier>();
            getVersionIdentifier(jit, out Guid guid);
            return guid;
        }

        // The offset to use is determined in GetJitObject where other properties of the JIT are determined
        private const int vtableIndex_ICorJitCompiler_getVersionIdentifier = 4;
        private const int vtableIndex_ICorJitCompiler_getVersionIdentifier_net5 = 2;
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void d_getVersionIdentifier(
            IntPtr thisPtr, // ICorJitCompiler*
            out Guid versionIdentifier
        );

        protected virtual int VTableIndex_ICorJitCompiler_compileMethod => 0;

        protected static unsafe IntPtr* GetVTableEntry(IntPtr @object, int index)
            => (*(IntPtr**) @object) + index;
        protected static unsafe IntPtr ReadObjectVTable(IntPtr @object, int index)
            => *GetVTableEntry(@object, index);

        protected override void DisableInlining(MethodBase method, RuntimeMethodHandle handle) {
            // https://github.com/dotnet/runtime/blob/89965be3ad2be404dc82bd9e688d5dd2a04bcb5f/src/coreclr/src/vm/method.hpp#L178
            // mdcNotInline = 0x2000
            // References to RuntimeMethodHandle (CORINFO_METHOD_HANDLE) pointing to MethodDesc
            // can be traced as far back as https://ntcore.com/files/netint_injection.htm
        }

        protected virtual void InstallJitHooks(IntPtr jitObject) => throw new PlatformNotSupportedException();

        public override bool OnMethodCompiledWillBeCalled => false;
        public override event OnMethodCompiledEvent OnMethodCompiled;

        protected virtual void JitHookCore(
            RuntimeTypeHandle declaringType,
            RuntimeMethodHandle methodHandle,
            IntPtr methodBodyStart, 
            ulong methodBodySize,
            RuntimeTypeHandle[] genericClassArguments,
            RuntimeTypeHandle[] genericMethodArguments
        ) {
            try {
                Type declType = Type.GetTypeFromHandle(declaringType);
                if (genericClassArguments != null && declType.IsGenericTypeDefinition) {
                    declType = declType.MakeGenericType(genericClassArguments.Select(Type.GetTypeFromHandle).ToArray());
                }
                MethodBase method = MethodBase.GetMethodFromHandle(methodHandle, declType.TypeHandle);
                // method is null for P/Invokes, ComImports and other dynamic interop methods.
                // Just to be 100% sure that it ISN'T an already known-but-"hidden" pinned method though...
                if (method == null) {
                    method = GetPin(methodHandle).Method;
                }
                try {
                    OnMethodCompiled?.Invoke(method, methodBodyStart, methodBodySize);
                } catch (Exception e) {
                    MMDbgLog.Log($"Error executing OnMethodCompiled event: {e}");
                }
            } catch (Exception e) {
                MMDbgLog.Log($"Error in JitHookCore: {e}");
            }
        }

        public static DetourRuntimeNETCorePlatform Create() {
            try {
                IntPtr jit = GetJitObject();
                Guid jitGuid = GetJitGuid(jit);

                DetourRuntimeNETCorePlatform platform = null;

                if (jitGuid == DetourRuntimeNET60Platform.JitVersionGuid) {
                    platform = new DetourRuntimeNET60Platform();
                } else if (jitGuid == DetourRuntimeNET50Platform.JitVersionGuid) {
                    platform = new DetourRuntimeNET50Platform();
                } else if (jitGuid == DetourRuntimeNETCore30Platform.JitVersionGuid) {
                    platform = new DetourRuntimeNETCore30Platform();
                }
                // TODO: add more known JIT GUIDs

                if (platform == null)
                    return new DetourRuntimeNETCorePlatform();

                platform?.InstallJitHooks(jit);
                return platform;
            } catch (Exception e) {
                MMDbgLog.Log("Could not get JIT information for the runtime, falling out to the version without JIT hooks");
                MMDbgLog.Log($"Error: {e}");
            }

            return new DetourRuntimeNETCorePlatform();
        }
    }
}
