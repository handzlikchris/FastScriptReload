using System;
using System.Reflection;
using MC = Mono.Cecil;
using CIL = Mono.Cecil.Cil;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Text;
using Mono.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    static partial class Extensions {

        private static readonly Dictionary<Type, int> _GetManagedSizeCache = new Dictionary<Type, int>() {
            { typeof(void), 0 }
        };
        private static MethodInfo _GetManagedSizeHelper;
        /// <summary>
        /// Get the managed size of a given type. This matches an IL-level sizeof(t), even if it cannot be determined normally in C#.
        /// Note that sizeof(t) != Marshal.SizeOf(t), f.e. when t is char.
        /// </summary>
        /// <param name="t">The type to get the size from.</param>
        /// <returns>The managed type size.</returns>
        public static int GetManagedSize(this Type t) {
            if (_GetManagedSizeCache.TryGetValue(t, out int size))
                return size;

            if (_GetManagedSizeHelper == null) {
                Assembly asm;

                const string @namespace = "MonoMod.Utils";
                const string @name = "GetManagedSizeHelper";
                const string @fullName = @namespace + "." + @name;

#if !CECIL0_9
                using (
#endif
                ModuleDefinition module = ModuleDefinition.CreateModule(
                    @fullName,
                    new ModuleParameters() {
                        Kind = ModuleKind.Dll,
#if !CECIL0_9 && MONOMOD_UTILS
                        ReflectionImporterProvider = MMReflectionImporter.Provider
#endif
                    }
                )
#if CECIL0_9
                ;
#else
                )
#endif
                {

                    TypeDefinition type = new TypeDefinition(
                        @namespace,
                        @name,
                        MC.TypeAttributes.Public | MC.TypeAttributes.Abstract | MC.TypeAttributes.Sealed
                    ) {
                        BaseType = module.TypeSystem.Object
                    };
                    module.Types.Add(type);

                    MethodDefinition method = new MethodDefinition(@name,
                        MC.MethodAttributes.Public | MC.MethodAttributes.Static | MC.MethodAttributes.HideBySig,
                        module.TypeSystem.Int32
                    );
                    GenericParameter genParam = new GenericParameter("T", method);
                    method.GenericParameters.Add(genParam);
                    type.Methods.Add(method);

                    ILProcessor il = method.Body.GetILProcessor();
                    il.Emit(OpCodes.Sizeof, genParam);
                    il.Emit(OpCodes.Ret);

                    asm = ReflectionHelper.Load(module);
                }

                _GetManagedSizeHelper = asm.GetType(@fullName).GetMethod(@name);
            }

            size =  (_GetManagedSizeHelper.MakeGenericMethod(t).CreateDelegate<Func<int>>() as Func<int>)();
            lock (_GetManagedSizeCache) {
                return _GetManagedSizeCache[t] = size;
            }
        }

        /// <summary>
        /// Get a type which matches what the method should receive via ldarg.0
        /// </summary>
        /// <param name="method">The method to obtain the "this" parameter type from.</param>
        /// <returns>The "this" parameter type.</returns>
        public static Type GetThisParamType(this MethodBase method) {
            Type type = method.DeclaringType;
            if (type.IsValueType)
                type = type.MakeByRefType();
            return type;
        }

        private static readonly Dictionary<MethodBase, Func<IntPtr>> _GetLdftnPointerCache = new Dictionary<MethodBase, Func<IntPtr>>();
        /// <summary>
        /// Get a native function pointer for a given method. This matches an IL-level ldftn.
        /// </summary>
        /// <remarks>
        /// The result of ldftn doesn't always match that of MethodHandle.GetFunctionPointer().
        /// For example, ldftn doesn't JIT-compile the method on mono, which thus keeps the class constructor untouched.
        /// And on .NET, struct overrides (f.e. ToString) have got multiple entry points pointing towards the same code.
        /// </remarks>
        /// <param name="m">The method to get a native function pointer for.</param>
        /// <returns>The native function pointer.</returns>
        public static IntPtr GetLdftnPointer(this MethodBase m) {
            if (_GetLdftnPointerCache.TryGetValue(m, out Func<IntPtr> func))
                return func();

            DynamicMethodDefinition dmd = new DynamicMethodDefinition(
                $"GetLdftnPointer<{m.GetID(simple: true)}>",
                typeof(IntPtr), Type.EmptyTypes
            );

            ILProcessor il = dmd.GetILProcessor();
            il.Emit(OpCodes.Ldftn, dmd.Definition.Module.ImportReference(m));
            il.Emit(OpCodes.Ret);

            lock (_GetLdftnPointerCache) {
                return (_GetLdftnPointerCache[m] = dmd.Generate().CreateDelegate<Func<IntPtr>>() as Func<IntPtr>)();
            }
        }

    }
}
