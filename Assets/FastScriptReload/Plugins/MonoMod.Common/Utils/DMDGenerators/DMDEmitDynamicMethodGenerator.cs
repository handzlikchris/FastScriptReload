using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
using System.Diagnostics;
using System.ComponentModel;
using System.Security.Permissions;
using System.Security;
using System.Diagnostics.SymbolStore;
using System.IO;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    sealed class DMDEmitDynamicMethodGenerator : DMDGenerator<DMDEmitDynamicMethodGenerator> {

        private static readonly FieldInfo _DynamicMethod_returnType =
            typeof(DynamicMethod).GetField("returnType", BindingFlags.NonPublic | BindingFlags.Instance) ??
            typeof(DynamicMethod).GetField("m_returnType", BindingFlags.NonPublic | BindingFlags.Instance);

        protected override MethodInfo _Generate(DynamicMethodDefinition dmd, object context) {
            MethodBase orig = dmd.OriginalMethod;
            MethodDefinition def = dmd.Definition;

            Type[] argTypes;

            if (orig != null) {
                ParameterInfo[] args = orig.GetParameters();
                int offs = 0;
                if (!orig.IsStatic) {
                    offs++;
                    argTypes = new Type[args.Length + 1];
                    argTypes[0] = orig.GetThisParamType();
                } else {
                    argTypes = new Type[args.Length];
                }
                for (int i = 0; i < args.Length; i++)
                    argTypes[i + offs] = args[i].ParameterType;

            } else {
                int offs = 0;
                if (def.HasThis) {
                    offs++;
                    argTypes = new Type[def.Parameters.Count + 1];
                    Type type = def.DeclaringType.ResolveReflection();
                    if (type.IsValueType)
                        type = type.MakeByRefType();
                    argTypes[0] = type;
                } else {
                    argTypes = new Type[def.Parameters.Count];
                }
                for (int i = 0; i < def.Parameters.Count; i++)
                    argTypes[i + offs] = def.Parameters[i].ParameterType.ResolveReflection();
            }

            string name = dmd.Name ?? $"DMD<{orig?.GetID(simple: true) ?? def.GetID(simple: true)}>";
            Type retType = (orig as MethodInfo)?.ReturnType ?? def.ReturnType?.ResolveReflection();

            MMDbgLog.Log($"new DynamicMethod: {retType} {name}({string.Join(",", argTypes.Select(type => type?.ToString()).ToArray())})");
            if (orig != null)
                MMDbgLog.Log($"orig: {(orig as MethodInfo)?.ReturnType?.ToString() ?? "NULL"} {orig.Name}({string.Join(",", orig.GetParameters().Select(arg => arg?.ParameterType?.ToString() ?? "NULL").ToArray())})");
            MMDbgLog.Log($"mdef: {def.ReturnType?.ToString() ?? "NULL"} {name}({string.Join(",", def.Parameters.Select(arg => arg?.ParameterType?.ToString() ?? "NULL").ToArray())})");

            DynamicMethod dm = new DynamicMethod(
                name,
                typeof(void), argTypes,
                orig?.DeclaringType ?? dmd.OwnerType ?? typeof(DynamicMethodDefinition),
                true // If any random errors pop up, try setting this to false first.
            );

            // DynamicMethods don't officially "support" certain return types, such as ByRef types.
            _DynamicMethod_returnType.SetValue(dm, retType);

            ILGenerator il = dm.GetILGenerator();

            _DMDEmit.Generate(dmd, dm, il);

            return dm;
        }

    }
}
