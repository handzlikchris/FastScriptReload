#if !NETSTANDARD
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace MonoMod.Utils {
    internal static partial class _DMDEmit {

        private readonly static MethodInfo m_MethodBase_InvokeSimple = typeof(MethodBase).GetMethod(
            "Invoke", BindingFlags.Public | BindingFlags.Instance, null,
            new Type[] { typeof(object), typeof(object[]) },
            null
        );

        private static MethodBuilder _CreateMethodProxy(MethodBuilder context, MethodInfo target) {
            TypeBuilder tb = (TypeBuilder) context.DeclaringType;
            string name = $".dmdproxy<{target.Name.Replace('.', '_')}>?{target.GetHashCode()}";
            MethodBuilder mb;

            // System.NotSupportedException: The invoked member is not supported before the type is created.
            /*
            mb = tb.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static) as MethodBuilder;
            if (mb != null)
                return mb;
            */

            Type[] args = target.GetParameters().Select(param => param.ParameterType).ToArray();
            mb = tb.DefineMethod(
                name,
                System.Reflection.MethodAttributes.HideBySig | System.Reflection.MethodAttributes.Private | System.Reflection.MethodAttributes.Static,
                CallingConventions.Standard,
                target.ReturnType,
                args
            );
            ILGenerator il = mb.GetILGenerator();

            // Load the DynamicMethod reference first.
            il.EmitReference(target);

            // Load any other arguments on top of that.
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldc_I4, args.Length);
            il.Emit(OpCodes.Newarr, typeof(object));

            for (int i = 0; i < args.Length; i++) {
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldc_I4, i);

                il.Emit(OpCodes.Ldarg, i);

                Type argType = args[i];
                bool argIsByRef = argType.IsByRef;
                if (argIsByRef)
                    argType = argType.GetElementType();
                bool argIsValueType = argType.IsValueType;
                if (argIsValueType) {
                    il.Emit(OpCodes.Box, argType);
                }

                il.Emit(OpCodes.Stelem_Ref);
            }

            // Invoke the delegate and return its result.
            il.Emit(OpCodes.Callvirt, m_MethodBase_InvokeSimple);

            if (target.ReturnType == typeof(void))
                il.Emit(OpCodes.Pop);
            else if (target.ReturnType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, target.ReturnType);
            il.Emit(OpCodes.Ret);

            return mb;
        }

    }
}
#endif
