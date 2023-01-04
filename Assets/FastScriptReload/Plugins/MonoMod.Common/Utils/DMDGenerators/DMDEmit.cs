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
using ExceptionHandler = Mono.Cecil.Cil.ExceptionHandler;
using MonoMod.Utils.Cil;

namespace MonoMod.Utils {
    internal static partial class _DMDEmit {

        private static readonly Dictionary<short, System.Reflection.Emit.OpCode> _ReflOpCodes = new Dictionary<short, System.Reflection.Emit.OpCode>();
        private static readonly Dictionary<short, Mono.Cecil.Cil.OpCode> _CecilOpCodes = new Dictionary<short, Mono.Cecil.Cil.OpCode>();

        static _DMDEmit() {
            foreach (FieldInfo field in typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) {
                System.Reflection.Emit.OpCode reflOpCode = (System.Reflection.Emit.OpCode) field.GetValue(null);
                _ReflOpCodes[reflOpCode.Value] = reflOpCode;
            }

            foreach (FieldInfo field in typeof(Mono.Cecil.Cil.OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) {
                Mono.Cecil.Cil.OpCode cecilOpCode = (Mono.Cecil.Cil.OpCode) field.GetValue(null);
                _CecilOpCodes[cecilOpCode.Value] = cecilOpCode;
            }
        }

        public static void Generate(DynamicMethodDefinition dmd, MethodBase _mb, ILGenerator il) {
            MethodDefinition def = dmd.Definition;
            DynamicMethod dm = _mb as DynamicMethod;
#if !NETSTANDARD
            MethodBuilder mb = _mb as MethodBuilder;
            ModuleBuilder moduleBuilder = mb?.Module as ModuleBuilder;
            // moduleBuilder.Assembly sometimes avoids the .Assembly override under mysterious circumstances.
            AssemblyBuilder assemblyBuilder = (mb?.DeclaringType as TypeBuilder)?.Assembly as AssemblyBuilder;
            HashSet<Assembly> accessChecksIgnored = null;
            if (mb != null) {
                accessChecksIgnored = new HashSet<Assembly>();
            }
#endif

#if !CECIL0_9
            MethodDebugInformation defInfo = dmd.Debug ? def.DebugInformation : null;
#endif

            if (dm != null) {
                foreach (ParameterDefinition param in def.Parameters) {
                    dm.DefineParameter(param.Index + 1, (System.Reflection.ParameterAttributes) param.Attributes, param.Name);
                }
            }
#if !NETSTANDARD
            if (mb != null) {
                foreach (ParameterDefinition param in def.Parameters) {
                    mb.DefineParameter(param.Index + 1, (System.Reflection.ParameterAttributes) param.Attributes, param.Name);
                }
            }
#endif

            LocalBuilder[] locals = def.Body.Variables.Select(
                var => {
                    LocalBuilder local = il.DeclareLocal(var.VariableType.ResolveReflection(), var.IsPinned);
#if !NETSTANDARD && !CECIL0_9
                    if (mb != null && defInfo != null && defInfo.TryGetName(var, out string name)) {
                        local.SetLocalSymInfo(name);
                    }
#endif
                    return local;
                }
            ).ToArray();

            // Pre-pass - Set up label map.
            Dictionary<Instruction, Label> labelMap = new Dictionary<Instruction, Label>();
            foreach (Instruction instr in def.Body.Instructions) {
                if (instr.Operand is Instruction[] targets) {
                    foreach (Instruction target in targets)
                        if (!labelMap.ContainsKey(target))
                            labelMap[target] = il.DefineLabel();

                } else if (instr.Operand is Instruction target) {
                    if (!labelMap.ContainsKey(target))
                        labelMap[target] = il.DefineLabel();
                }
            }

#if !NETSTANDARD && !CECIL0_9
            Dictionary<Document, ISymbolDocumentWriter> infoDocCache = mb == null ? null : new Dictionary<Document, ISymbolDocumentWriter>();
#endif

            int paramOffs = def.HasThis ? 1 : 0;
            object[] emitArgs = new object[2];
            bool checkTryEndEarly = false;
            foreach (Instruction instr in def.Body.Instructions) {
                if (labelMap.TryGetValue(instr, out Label label))
                    il.MarkLabel(label);

#if !NETSTANDARD && !CECIL0_9
                SequencePoint instrInfo = defInfo?.GetSequencePoint(instr);
                if (mb != null && instrInfo != null) {
                    if (!infoDocCache.TryGetValue(instrInfo.Document, out ISymbolDocumentWriter infoDoc)) {
                        infoDocCache[instrInfo.Document] = infoDoc = moduleBuilder.DefineDocument(
                            instrInfo.Document.Url,
                            instrInfo.Document.LanguageGuid,
                            instrInfo.Document.LanguageVendorGuid,
                            instrInfo.Document.TypeGuid
                        );
                    }
                    il.MarkSequencePoint(infoDoc, instrInfo.StartLine, instrInfo.StartColumn, instrInfo.EndLine, instrInfo.EndColumn);
                }
#endif

                foreach (ExceptionHandler handler in def.Body.ExceptionHandlers) {
                    if (checkTryEndEarly && handler.HandlerEnd == instr) {
                        il.EndExceptionBlock();
                    }

                    if (handler.TryStart == instr) {
                        il.BeginExceptionBlock();
                    } else if (handler.FilterStart == instr) {
                        il.BeginExceptFilterBlock();
                    } else if (handler.HandlerStart == instr) {
                        switch (handler.HandlerType) {
                            case ExceptionHandlerType.Filter:
                                il.BeginCatchBlock(null);
                                break;
                            case ExceptionHandlerType.Catch:
                                il.BeginCatchBlock(handler.CatchType.ResolveReflection());
                                break;
                            case ExceptionHandlerType.Finally:
                                il.BeginFinallyBlock();
                                break;
                            case ExceptionHandlerType.Fault:
                                il.BeginFaultBlock();
                                break;
                        }

                    }

                    // Avoid duplicate endfilter / endfinally
                    if (handler.HandlerStart == instr.Next) {
                        switch (handler.HandlerType) {
                            case ExceptionHandlerType.Filter:
                                if (instr.OpCode == Mono.Cecil.Cil.OpCodes.Endfilter)
                                    goto SkipEmit;
                                break;
                            case ExceptionHandlerType.Finally:
                                if (instr.OpCode == Mono.Cecil.Cil.OpCodes.Endfinally)
                                    goto SkipEmit;
                                break;
                        }
                    }
                }

                if (instr.OpCode.OperandType == Mono.Cecil.Cil.OperandType.InlineNone)
                    il.Emit(_ReflOpCodes[instr.OpCode.Value]);
                else {
                    object operand = instr.Operand;

                    if (operand is Instruction[] targets) {
                        operand = targets.Select(target => labelMap[target]).ToArray();
                        // Let's hope that the JIT treats the long forms identically to the short forms.
                        instr.OpCode = instr.OpCode.ToLongOp();

                    } else if (operand is Instruction target) {
                        operand = labelMap[target];
                        // Let's hope that the JIT treats the long forms identically to the short forms.
                        instr.OpCode = instr.OpCode.ToLongOp();

                    } else if (operand is VariableDefinition var) {
                        operand = locals[var.Index];

                    } else if (operand is ParameterDefinition param) {
                        operand = param.Index + paramOffs;

                    } else if (operand is MemberReference mref) {
                        MemberInfo member = mref == def ? _mb : mref.ResolveReflection();
                        operand = member;
#if !NETSTANDARD
                        if (mb != null && member != null) {
                            // See DMDGenerator.cs for the explanation of this forced .?
                            Module module = member?.Module;
                            if (module == null)
                                continue;
                            Assembly asm = module.Assembly;
                            if (asm != null && !accessChecksIgnored.Contains(asm)) {
                                // while (member.DeclaringType != null)
                                //     member = member.DeclaringType;
                                assemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(DynamicMethodDefinition.c_IgnoresAccessChecksToAttribute, new object[] {
                                    asm.GetName().Name
                                }));
                                accessChecksIgnored.Add(asm);
                            }
                        }
#endif

                    } else if (operand is CallSite csite) {
                        if (dm != null) {
                            // SignatureHelper in unmanaged contexts cannot be fully made use of for DynamicMethods.
                            _EmitCallSite(dm, il, _ReflOpCodes[instr.OpCode.Value], csite);
                            continue;
                        }
#if !NETSTANDARD
                        operand = csite.ResolveReflection(mb.Module);
#else
                        throw new NotSupportedException();
#endif
                    }

#if !NETSTANDARD
                    if (mb != null && operand is MethodBase called && called.DeclaringType == null) {
                        // "Global" methods (f.e. DynamicMethods) cannot be tokenized.
                        if (instr.OpCode == Mono.Cecil.Cil.OpCodes.Call) {
                            if (called is MethodInfo target && target.IsDynamicMethod()) {
                                // This should be heavily optimizable.
                                operand = _CreateMethodProxy(mb, target);

                            } else {
                                IntPtr ptr = called.GetLdftnPointer();
                                if (IntPtr.Size == 4)
                                    il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4, (int) ptr);
                                else
                                    il.Emit(System.Reflection.Emit.OpCodes.Ldc_I8, (long) ptr);
                                il.Emit(System.Reflection.Emit.OpCodes.Conv_I);
                                instr.OpCode = Mono.Cecil.Cil.OpCodes.Calli;
                                operand = ((MethodReference) instr.Operand).ResolveReflectionSignature(mb.Module);
                            }
                        } else {
                            throw new NotSupportedException($"Unsupported global method operand on opcode {instr.OpCode.Name}");
                        }
                    }
#endif

                    if (operand == null)
                        throw new NullReferenceException($"Unexpected null in {def} @ {instr}");

                    il.DynEmit(_ReflOpCodes[instr.OpCode.Value], operand);
                }

                if (!checkTryEndEarly) {
                    foreach (ExceptionHandler handler in def.Body.ExceptionHandlers) {
                        if (handler.HandlerEnd == instr.Next) {
                            il.EndExceptionBlock();
                        }
                    }
                }

                checkTryEndEarly = false;
                continue;

                SkipEmit:
                checkTryEndEarly = true;
                continue;
            }
        }

        public static void ResolveWithModifiers(TypeReference typeRef, out Type type, out Type[] typeModReq, out Type[] typeModOpt, List<Type> modReq = null, List<Type> modOpt = null) {
            if (modReq == null)
                modReq = new List<Type>();
            else
                modReq.Clear();

            if (modOpt == null)
                modOpt = new List<Type>();
            else
                modOpt.Clear();

            for (
                TypeReference mod = typeRef;
                mod is TypeSpecification modSpec;
                mod = modSpec.ElementType
            ) {
                switch (mod) {
                    case RequiredModifierType paramTypeModReq:
                        modReq.Add(paramTypeModReq.ModifierType.ResolveReflection());
                        break;

                    case OptionalModifierType paramTypeOptReq:
                        modOpt.Add(paramTypeOptReq.ModifierType.ResolveReflection());
                        break;
                }
            }

            type = typeRef.ResolveReflection();
            typeModReq = modReq.ToArray();
            typeModOpt = modOpt.ToArray();
        }

    }
}
