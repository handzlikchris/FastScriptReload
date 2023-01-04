using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using MonoMod.Utils;
using System.Linq;

namespace MonoMod.RuntimeDetour.Platforms {
#if !MONOMOD_INTERNAL
    public
#endif
    class DetourRuntimeMonoPlatform : DetourRuntimeILPlatform {
        private static readonly object[] _NoArgs = new object[0];

        private static readonly MethodInfo _DynamicMethod_CreateDynMethod =
            typeof(DynamicMethod).GetMethod("CreateDynMethod", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo _DynamicMethod_mhandle =
            typeof(DynamicMethod).GetField("mhandle", BindingFlags.NonPublic | BindingFlags.Instance);

        public override bool OnMethodCompiledWillBeCalled => false;
#pragma warning disable CS0067 // Event never fired
        public override event OnMethodCompiledEvent OnMethodCompiled;
#pragma warning restore CS0067

        protected override RuntimeMethodHandle GetMethodHandle(MethodBase method) {
            // Compile the method handle before getting our hands on the final method handle.
            // Note that Mono can return RuntimeMethodInfo instead of DynamicMethod in some places, thus bypassing this.
            // Let's assume that the method was already compiled ahead of this method call if that is the case.
            if (method is DynamicMethod) {
                _DynamicMethod_CreateDynMethod?.Invoke(method, _NoArgs);
                if (_DynamicMethod_mhandle != null)
                    return (RuntimeMethodHandle) _DynamicMethod_mhandle.GetValue(method);
            }

            return method.MethodHandle;
        }

        protected override unsafe void DisableInlining(MethodBase method, RuntimeMethodHandle handle) {
            // https://github.com/mono/mono/blob/34dee0ea4e969d6d5b37cb842fc3b9f73f2dc2ae/mono/metadata/class-internals.h#L64
            ushort* iflags = (ushort*) ((long) handle.Value + 2);
            *iflags |= (ushort) MethodImplOptions.NoInlining;
        }
    }

}
