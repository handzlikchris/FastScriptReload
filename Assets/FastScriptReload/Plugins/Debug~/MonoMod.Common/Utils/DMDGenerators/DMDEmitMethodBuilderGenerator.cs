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
    sealed class DMDEmitMethodBuilderGenerator : DMDGenerator<DMDEmitMethodBuilderGenerator> {

        private static readonly bool _MBCanRunAndCollect = Enum.IsDefined(typeof(AssemblyBuilderAccess), "RunAndCollect");

        protected override MethodInfo _Generate(DynamicMethodDefinition dmd, object context) {
            TypeBuilder typeBuilder = context as TypeBuilder;
            MethodBuilder method = GenerateMethodBuilder(dmd, typeBuilder);
            typeBuilder = (TypeBuilder) method.DeclaringType;
            Type type = typeBuilder.CreateType();
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MONOMOD_DMD_DUMP"))) {
                string path = method.Module.FullyQualifiedName;
                string name = Path.GetFileName(path);
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                if (File.Exists(path))
                    File.Delete(path);
                ((AssemblyBuilder) typeBuilder.Assembly).Save(name);
            }
            return type.GetMethod(method.Name, BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        }

        public static MethodBuilder GenerateMethodBuilder(DynamicMethodDefinition dmd, TypeBuilder typeBuilder) {
            MethodBase orig = dmd.OriginalMethod;
            MethodDefinition def = dmd.Definition;

            if (typeBuilder == null) {
                string dumpDir = Environment.GetEnvironmentVariable("MONOMOD_DMD_DUMP");
                if (string.IsNullOrEmpty(dumpDir)) {
                    dumpDir = null;
                } else {
                    dumpDir = Path.GetFullPath(dumpDir);
                }
                bool collect = string.IsNullOrEmpty(dumpDir) && _MBCanRunAndCollect;
                AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(
                    new AssemblyName() {
                        Name = dmd.GetDumpName("MethodBuilder")
                    },
                    collect ? (AssemblyBuilderAccess) 9 : AssemblyBuilderAccess.RunAndSave,
                    dumpDir
                );

                ab.SetCustomAttribute(new CustomAttributeBuilder(DynamicMethodDefinition.c_UnverifiableCodeAttribute, new object[] {
                }));

                if (dmd.Debug) {
                    ab.SetCustomAttribute(new CustomAttributeBuilder(DynamicMethodDefinition.c_DebuggableAttribute, new object[] {
                        DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.Default
                    }));
                }

                // Note: Debugging can fail on mono if Mono.CompilerServices.SymbolWriter.dll cannot be found,
                // or if Mono.CompilerServices.SymbolWriter.SymbolWriterImpl can't be found inside of that.
                // https://github.com/mono/mono/blob/f879e35e3ed7496d819bd766deb8be6992d068ed/mcs/class/corlib/System.Reflection.Emit/ModuleBuilder.cs#L146
                ModuleBuilder module = ab.DefineDynamicModule($"{ab.GetName().Name}.dll", $"{ab.GetName().Name}.dll", dmd.Debug);
                typeBuilder = module.DefineType(
                    $"DMD<{orig?.GetID(simple: true)?.Replace('.', '_')}>?{dmd.GetHashCode()}",
                    System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Abstract | System.Reflection.TypeAttributes.Sealed | System.Reflection.TypeAttributes.Class
                );
            }

            Type[] argTypes;
            Type[][] argTypesModReq;
            Type[][] argTypesModOpt;

            if (orig != null) {
                ParameterInfo[] args = orig.GetParameters();
                int offs = 0;
                if (!orig.IsStatic) {
                    offs++;
                    argTypes = new Type[args.Length + 1];
                    argTypesModReq = new Type[args.Length + 1][];
                    argTypesModOpt = new Type[args.Length + 1][];
                    argTypes[0] = orig.GetThisParamType();
                    argTypesModReq[0] = Type.EmptyTypes;
                    argTypesModOpt[0] = Type.EmptyTypes;
                } else {
                    argTypes = new Type[args.Length];
                    argTypesModReq = new Type[args.Length][];
                    argTypesModOpt = new Type[args.Length][];
                }

                for (int i = 0; i < args.Length; i++) {
                    argTypes[i + offs] = args[i].ParameterType;
                    argTypesModReq[i + offs] = args[i].GetRequiredCustomModifiers();
                    argTypesModOpt[i + offs] = args[i].GetOptionalCustomModifiers();
                }

            } else {
                int offs = 0;
                if (def.HasThis) {
                    offs++;
                    argTypes = new Type[def.Parameters.Count + 1];
                    argTypesModReq = new Type[def.Parameters.Count + 1][];
                    argTypesModOpt = new Type[def.Parameters.Count + 1][];
                    Type type = def.DeclaringType.ResolveReflection();
                    if (type.IsValueType)
                        type = type.MakeByRefType();
                    argTypes[0] = type;
                    argTypesModReq[0] = Type.EmptyTypes;
                    argTypesModOpt[0] = Type.EmptyTypes;
                } else {
                    argTypes = new Type[def.Parameters.Count];
                    argTypesModReq = new Type[def.Parameters.Count][];
                    argTypesModOpt = new Type[def.Parameters.Count][];
                }

                List<Type> modReq = new List<Type>();
                List<Type> modOpt = new List<Type>();

                for (int i = 0; i < def.Parameters.Count; i++) {
                    _DMDEmit.ResolveWithModifiers(def.Parameters[i].ParameterType, out Type paramType, out Type[] paramTypeModReq, out Type[] paramTypeModOpt, modReq, modOpt);
                    argTypes[i + offs] = paramType;
                    argTypesModReq[i + offs] = paramTypeModReq;
                    argTypesModOpt[i + offs] = paramTypeModOpt;
                }
            }

            // Required because the return type modifiers aren't easily accessible via reflection.
            _DMDEmit.ResolveWithModifiers(def.ReturnType, out Type returnType, out Type[] returnTypeModReq, out Type[] returnTypeModOpt);

            MethodBuilder mb = typeBuilder.DefineMethod(
                dmd.Name ?? (orig?.Name ?? def.Name).Replace('.', '_'),
                System.Reflection.MethodAttributes.HideBySig | System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static,
                CallingConventions.Standard,
                returnType, returnTypeModReq, returnTypeModOpt,
                argTypes, argTypesModReq, argTypesModOpt
            );
            ILGenerator il = mb.GetILGenerator();

            _DMDEmit.Generate(dmd, mb, il);

            return mb;
        }

    }
}
#endif
