using System;
using System.Reflection;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using ExceptionHandler = Mono.Cecil.Cil.ExceptionHandler;

namespace MonoMod.Utils {
    /// <summary>
    /// A DMDGenerator implementation using Mono.Cecil to build an in-memory assembly.
    /// </summary>
#if !MONOMOD_INTERNAL
    public
#endif
    sealed class DMDCecilGenerator : DMDGenerator<DMDCecilGenerator> {

        protected override MethodInfo _Generate(DynamicMethodDefinition dmd, object context) {
            MethodDefinition def = dmd.Definition;
            TypeDefinition typeDef = context as TypeDefinition;

            bool moduleIsTemporary = false;
            ModuleDefinition module = typeDef?.Module;
            HashSet<string> accessChecksIgnored = null;
            if (typeDef == null) {
                moduleIsTemporary = true;
                accessChecksIgnored = new HashSet<string>();

                string name = dmd.GetDumpName("Cecil");
                module = ModuleDefinition.CreateModule(name, new ModuleParameters() {
                    Kind = ModuleKind.Dll,
#if !CECIL0_9
                    ReflectionImporterProvider = MMReflectionImporter.ProviderNoDefault
#endif
                });

                module.Assembly.CustomAttributes.Add(new CustomAttribute(module.ImportReference(DynamicMethodDefinition.c_UnverifiableCodeAttribute)));

                if (dmd.Debug) {
                    CustomAttribute caDebug = new CustomAttribute(module.ImportReference(DynamicMethodDefinition.c_DebuggableAttribute));
                    caDebug.ConstructorArguments.Add(new CustomAttributeArgument(
                        module.ImportReference(typeof(DebuggableAttribute.DebuggingModes)),
                        DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.Default
                    ));
                    module.Assembly.CustomAttributes.Add(caDebug);
                }

                typeDef = new TypeDefinition(
                    "",
                    $"DMD<{dmd.OriginalMethod?.Name?.Replace('.', '_')}>?{GetHashCode()}",
                    Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed | Mono.Cecil.TypeAttributes.Class
                ) {
                    BaseType = module.TypeSystem.Object
                };

                module.Types.Add(typeDef);
            }

            try {

                MethodDefinition clone = null;

                TypeReference tr_IsVolatile = new TypeReference("System.Runtime.CompilerServices", "IsVolatile", module, module.TypeSystem.CoreLibrary);

#pragma warning disable IDE0039 // Use local function
                Relinker relinker = (mtp, ctx) => {
                    if (mtp == def)
                        return clone;
                    return module.ImportReference(mtp);
                };
#pragma warning restore IDE0039 // Use local function

                clone = new MethodDefinition(dmd.Name ?? "_" + def.Name.Replace('.', '_'), def.Attributes, module.TypeSystem.Void) {
                    MethodReturnType = def.MethodReturnType,
                    Attributes = Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                    ImplAttributes = Mono.Cecil.MethodImplAttributes.IL | Mono.Cecil.MethodImplAttributes.Managed,
                    DeclaringType = typeDef,
                    NoInlining = true
                };

                foreach (ParameterDefinition param in def.Parameters)
                    clone.Parameters.Add(param.Clone().Relink(relinker, clone));

                clone.ReturnType = def.ReturnType.Relink(relinker, clone);

                typeDef.Methods.Add(clone);

                clone.HasThis = def.HasThis;
                Mono.Cecil.Cil.MethodBody body = clone.Body = def.Body.Clone(clone);

                foreach (VariableDefinition var in clone.Body.Variables)
                    var.VariableType = var.VariableType.Relink(relinker, clone);

                foreach (ExceptionHandler handler in clone.Body.ExceptionHandlers)
                    if (handler.CatchType != null)
                        handler.CatchType = handler.CatchType.Relink(relinker, clone);

                for (int instri = 0; instri < body.Instructions.Count; instri++) {
                    Instruction instr = body.Instructions[instri];
                    object operand = instr.Operand;

                    // Import references.
                    if (operand is ParameterDefinition param) {
                        operand = clone.Parameters[param.Index];

                    } else if (operand is IMetadataTokenProvider mtp) {
                        operand = mtp.Relink(relinker, clone);

                    }

                    // System.Reflection doesn't contain any volatility info.
                    // System.Reflection.Emit presumably does something similar to this.
                    // Mono.Cecil thus isn't aware of the volatility as part of the imported field reference.
                    // The modifier is still necessary though.
                    // This is done here instead of the copier as Harmony and other users can't track modreqs
                    if (instr.Previous?.OpCode == OpCodes.Volatile &&
                        operand is FieldReference fref &&
                        (fref.FieldType as RequiredModifierType)?.ModifierType != tr_IsVolatile) {
                        fref.FieldType = new RequiredModifierType(tr_IsVolatile, fref.FieldType);
                    }

                    if (operand is DynamicMethodReference dmref) {
                        // TODO: Fix up DynamicMethod inline refs.
                    }

                    if (accessChecksIgnored != null && operand is MemberReference mref) {
                        IMetadataScope asmRef = (mref as TypeReference)?.Scope ?? mref.DeclaringType.Scope;
                        if (!accessChecksIgnored.Contains(asmRef.Name)) {
                            CustomAttribute caAccess = new CustomAttribute(module.ImportReference(DynamicMethodDefinition.c_IgnoresAccessChecksToAttribute));
                            caAccess.ConstructorArguments.Add(new CustomAttributeArgument(
                                module.ImportReference(typeof(DebuggableAttribute.DebuggingModes)),
                                asmRef.Name
                            ));
                            module.Assembly.CustomAttributes.Add(caAccess);
                            accessChecksIgnored.Add(asmRef.Name);
                        }
                    }

                    instr.Operand = operand;
                }

                clone.HasThis = false;

                if (def.HasThis) {
                    TypeReference type = def.DeclaringType;
                    if (type.IsValueType)
                        type = new ByReferenceType(type);
                    clone.Parameters.Insert(0, new ParameterDefinition("<>_this", Mono.Cecil.ParameterAttributes.None, type.Relink(relinker, clone)));
                }

                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MONOMOD_DMD_DUMP"))) {
                    string dir = Path.GetFullPath(Environment.GetEnvironmentVariable("MONOMOD_DMD_DUMP"));
                    string name = module.Name + ".dll";
                    string path = Path.Combine(dir, name);
                    dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    if (File.Exists(path))
                        File.Delete(path);
                    using (Stream fileStream = File.OpenWrite(path))
                        module.Write(fileStream);
                }

                Assembly asm = ReflectionHelper.Load(module);

                return asm.GetType(typeDef.FullName.Replace("+", "\\+", StringComparison.Ordinal), false, false)
                    .GetMethod(clone.Name, BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

            } finally {
#if !CECIL0_9
                if (moduleIsTemporary)
                    module.Dispose();
#endif
            }
        }

    }
}
