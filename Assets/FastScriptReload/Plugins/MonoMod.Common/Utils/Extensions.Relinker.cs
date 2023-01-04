using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ExceptionHandler = Mono.Cecil.Cil.ExceptionHandler;

namespace MonoMod.Utils {
    /// <summary>
    /// The relinker callback delegate type.
    /// </summary>
    /// <param name="mtp">The reference (metadata token provider) to relink.</param>
    /// <param name="context">The generic context provided to relink generic references.</param>
    /// <returns>A relinked reference.</returns>
    public delegate IMetadataTokenProvider Relinker(IMetadataTokenProvider mtp, IGenericParameterProvider context);
#if !MONOMOD_INTERNAL
    public
#endif
    static partial class Extensions {

        /// <summary>
        /// Clone the given method definition.
        /// </summary>
        /// <param name="o">The original method.</param>
        /// <param name="c">The method definition to apply the cloning process onto, or null to create a new method.</param>
        /// <returns>A clone of the original method.</returns>
        public static MethodDefinition Clone(this MethodDefinition o, MethodDefinition c = null) {
            if (o == null)
                return null;
            if (c == null)
                c = new MethodDefinition(o.Name, o.Attributes, o.ReturnType);
            c.Name = o.Name;
            c.Attributes = o.Attributes;
            c.ReturnType = o.ReturnType;
            c.DeclaringType = o.DeclaringType;
            c.MetadataToken = c.MetadataToken;
            c.Body = o.Body?.Clone(c);
            c.Attributes = o.Attributes;
            c.ImplAttributes = o.ImplAttributes;
            c.PInvokeInfo = o.PInvokeInfo;
            c.IsPreserveSig = o.IsPreserveSig;
            c.IsPInvokeImpl = o.IsPInvokeImpl;

            foreach (GenericParameter genParam in o.GenericParameters)
                c.GenericParameters.Add(genParam.Clone());

            foreach (ParameterDefinition param in o.Parameters)
                c.Parameters.Add(param.Clone());

            foreach (CustomAttribute attrib in o.CustomAttributes)
                c.CustomAttributes.Add(attrib.Clone());

            foreach (MethodReference @override in o.Overrides)
                c.Overrides.Add(@override);

            if (c.Body != null) {
                int foundIndex;
                foreach (Instruction ci in c.Body.Instructions) {
                    if (ci.Operand is GenericParameter genParam && (foundIndex = o.GenericParameters.IndexOf(genParam)) != -1) {
                        ci.Operand = c.GenericParameters[foundIndex];
                    } else if (ci.Operand is ParameterDefinition param && (foundIndex = o.Parameters.IndexOf(param)) != -1) {
                        ci.Operand = c.Parameters[foundIndex];
                    }
                }
            }

            return c;
        }

        /// <summary>
        /// Clone the given method body.
        /// </summary>
        /// <param name="bo">The original method body.</param>
        /// <param name="m">The method which will own the newly cloned method body.</param>
        /// <returns>A clone of the original method body.</returns>
        public static MethodBody Clone(this MethodBody bo, MethodDefinition m) {
            if (bo == null)
                return null;

            MethodBody bc = new MethodBody(m);
            bc.MaxStackSize = bo.MaxStackSize;
            bc.InitLocals = bo.InitLocals;
            bc.LocalVarToken = bo.LocalVarToken;

            bc.Instructions.AddRange(bo.Instructions.Select(o => {
                Instruction c = Instruction.Create(OpCodes.Nop);
                c.OpCode = o.OpCode;
                c.Operand = o.Operand;
                c.Offset = o.Offset;
                return c;
            }));

            foreach (Instruction c in bc.Instructions) {
                if (c.Operand is Instruction target) {
                    c.Operand = bc.Instructions[bo.Instructions.IndexOf(target)];
                } else if (c.Operand is Instruction[] targets) {
                    c.Operand = targets.Select(i => bc.Instructions[bo.Instructions.IndexOf(i)]).ToArray();
                }
            }

            bc.ExceptionHandlers.AddRange(bo.ExceptionHandlers.Select(o => {
                ExceptionHandler c = new ExceptionHandler(o.HandlerType);
                c.TryStart = o.TryStart == null ? null : bc.Instructions[bo.Instructions.IndexOf(o.TryStart)];
                c.TryEnd = o.TryEnd == null ? null : bc.Instructions[bo.Instructions.IndexOf(o.TryEnd)];
                c.FilterStart = o.FilterStart == null ? null : bc.Instructions[bo.Instructions.IndexOf(o.FilterStart)];
                c.HandlerStart = o.HandlerStart == null ? null : bc.Instructions[bo.Instructions.IndexOf(o.HandlerStart)];
                c.HandlerEnd = o.HandlerEnd == null ? null : bc.Instructions[bo.Instructions.IndexOf(o.HandlerEnd)];
                c.CatchType = o.CatchType;
                return c;
            }));

            bc.Variables.AddRange(bo.Variables.Select(o => {
                VariableDefinition c = new VariableDefinition(o.VariableType);
                return c;
            }));

#if !CECIL0_9
            m.CustomDebugInformations.AddRange(bo.Method.CustomDebugInformations); // Abstract. TODO: Implement deep CustomDebugInformations copy.
            m.DebugInformation.SequencePoints.AddRange(bo.Method.DebugInformation.SequencePoints.Select(o => {
                SequencePoint c = new SequencePoint(bc.Instructions.FirstOrDefault(i => i.Offset == o.Offset), o.Document);
                c.StartLine = o.StartLine;
                c.StartColumn = o.StartColumn;
                c.EndLine = o.EndLine;
                c.EndColumn = o.EndColumn;
                return c;
            }));
#endif

            return bc;
        }

        private static readonly System.Reflection.FieldInfo f_GenericParameter_position = typeof(GenericParameter).GetField("position", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.FieldInfo f_GenericParameter_type = typeof(GenericParameter).GetField("type", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        /// <summary>
        /// Force-update a generic parameter's position and type.
        /// </summary>
        /// <param name="param">The generic parameter to update.</param>
        /// <param name="position">The new position.</param>
        /// <param name="type">The new type.</param>
        /// <returns>The updated generic parameter.</returns>
        public static GenericParameter Update(this GenericParameter param, int position, GenericParameterType type) {
            f_GenericParameter_position.SetValue(param, position);
            f_GenericParameter_type.SetValue(param, type);
            return param;
        }

        /// <summary>
        /// Resolve a given generic parameter in another context.
        /// </summary>
        /// <param name="provider">The new context.</param>
        /// <param name="orig">The original generic parameter.</param>
        /// <returns>A generic parameter provided by the given context which matches the original generic parameter.</returns>
        public static GenericParameter ResolveGenericParameter(this IGenericParameterProvider provider, GenericParameter orig) {
            // This can be true for T[,].Get in "Enter the Gungeon"
            if (provider is GenericParameter && ((GenericParameter) provider).Name == orig.Name)
                return (GenericParameter) provider;

            foreach (GenericParameter param in provider.GenericParameters)
                if (param.Name == orig.Name)
                    return param;

            int index = orig.Position;
            if (provider is MethodReference && orig.DeclaringMethod != null) {
                if (index < provider.GenericParameters.Count)
                    return provider.GenericParameters[index];
                else
                    return orig.Clone().Update(index, GenericParameterType.Method);
            }

            if (provider is TypeReference && orig.DeclaringType != null)
                if (index < provider.GenericParameters.Count)
                    return provider.GenericParameters[index];
                else
                    return orig.Clone().Update(index, GenericParameterType.Type);

            return
                (provider as TypeSpecification)?.ElementType.ResolveGenericParameter(orig) ??
                (provider as MemberReference)?.DeclaringType?.ResolveGenericParameter(orig);
        }

        /// <summary>
        /// Relink the given member reference (metadata token provider).
        /// </summary>
        /// <param name="mtp">The reference to relink.</param>
        /// <param name="relinker">The relinker to use during the relinking process.</param>
        /// <param name="context">The generic context provided to relink generic references.</param>
        /// <returns>A relinked reference.</returns>
        public static IMetadataTokenProvider Relink(this IMetadataTokenProvider mtp, Relinker relinker, IGenericParameterProvider context) {
            if (mtp is TypeReference) return ((TypeReference) mtp).Relink(relinker, context);
#if !CECIL0_10
            if (mtp is GenericParameterConstraint) return ((GenericParameterConstraint) mtp).Relink(relinker, context);
#endif
            if (mtp is MethodReference) return ((MethodReference) mtp).Relink(relinker, context);
            if (mtp is FieldReference) return ((FieldReference) mtp).Relink(relinker, context);
            if (mtp is ParameterDefinition) return ((ParameterDefinition) mtp).Relink(relinker, context);
            if (mtp is CallSite) return ((CallSite) mtp).Relink(relinker, context);
            throw new InvalidOperationException($"MonoMod can't handle metadata token providers of the type {mtp.GetType()}");
        }

        /// <summary>
        /// Relink the given type reference.
        /// </summary>
        /// <param name="type">The reference to relink.</param>
        /// <param name="relinker">The relinker to use during the relinking process.</param>
        /// <param name="context">The generic context provided to relink generic references.</param>
        /// <returns>A relinked reference.</returns>
        public static TypeReference Relink(this TypeReference type, Relinker relinker, IGenericParameterProvider context) {
            if (type == null)
                return null;

            if (type is TypeSpecification ts) {
                TypeReference relinkedElem = ts.ElementType.Relink(relinker, context);

                if (type.IsSentinel)
                    return new SentinelType(relinkedElem);

                if (type.IsByReference)
                    return new ByReferenceType(relinkedElem);

                if (type.IsPointer)
                    return new PointerType(relinkedElem);

                if (type.IsPinned)
                    return new PinnedType(relinkedElem);

                if (type.IsArray) {
                    ArrayType at = new ArrayType(relinkedElem, ((ArrayType) type).Rank);
                    for (int i = 0; i < at.Rank; i++)
                        // It's a struct.
                        at.Dimensions[i] = ((ArrayType) type).Dimensions[i];
                    return at;
                }

                if (type.IsRequiredModifier)
                    return new RequiredModifierType(((RequiredModifierType) type).ModifierType.Relink(relinker, context), relinkedElem);

                if (type.IsOptionalModifier)
                    return new OptionalModifierType(((OptionalModifierType) type).ModifierType.Relink(relinker, context), relinkedElem);

                if (type.IsGenericInstance) {
                    GenericInstanceType git = new GenericInstanceType(relinkedElem);
                    foreach (TypeReference genArg in ((GenericInstanceType) type).GenericArguments)
                        git.GenericArguments.Add(genArg?.Relink(relinker, context));
                    return git;
                }

                if (type.IsFunctionPointer) {
                    FunctionPointerType fp = (FunctionPointerType) type;
                    fp.ReturnType = fp.ReturnType.Relink(relinker, context);
                    for (int i = 0; i < fp.Parameters.Count; i++)
                        fp.Parameters[i].ParameterType = fp.Parameters[i].ParameterType.Relink(relinker, context);
                    return fp;
                }

                throw new NotSupportedException($"MonoMod can't handle TypeSpecification: {type.FullName} ({type.GetType()})");
            }

            if (type.IsGenericParameter && context != null) {
                GenericParameter genParam = context.ResolveGenericParameter((GenericParameter) type);
                if (genParam == null)
                    throw new RelinkTargetNotFoundException($"{RelinkTargetNotFoundException.DefaultMessage} {type.FullName} (context: {context})", type, context);
                for (int i = 0; i < genParam.Constraints.Count; i++)
                    if (!genParam.Constraints[i].GetConstraintType().IsGenericInstance) // That is somehow possible and causes a stack overflow.
                        genParam.Constraints[i] = genParam.Constraints[i].Relink(relinker, context);
                return genParam;
            }

            return (TypeReference) relinker(type, context);
        }

#if !CECIL0_10
        /// <summary>
        /// Relink the given type reference.
        /// </summary>
        /// <param name="constraint">The reference to relink.</param>
        /// <param name="relinker">The relinker to use during the relinking process.</param>
        /// <param name="context">The generic context provided to relink generic references.</param>
        /// <returns>A relinked reference.</returns>
        public static GenericParameterConstraint Relink(this GenericParameterConstraint constraint, Relinker relinker, IGenericParameterProvider context) {
            if (constraint == null)
                return null;

            GenericParameterConstraint relink = new GenericParameterConstraint(constraint.ConstraintType.Relink(relinker, context));

            foreach (CustomAttribute attrib in constraint.CustomAttributes)
                relink.CustomAttributes.Add(attrib.Relink(relinker, context));

            return relink;
        }
#endif

        /// <summary>
        /// Relink the given method reference.
        /// </summary>
        /// <param name="method">The reference to relink.</param>
        /// <param name="relinker">The relinker to use during the relinking process.</param>
        /// <param name="context">The generic context provided to relink generic references.</param>
        /// <returns>A relinked reference.</returns>
        public static IMetadataTokenProvider Relink(this MethodReference method, Relinker relinker, IGenericParameterProvider context) {
            if (method.IsGenericInstance) {
                GenericInstanceMethod methodg = (GenericInstanceMethod) method;
                GenericInstanceMethod gim = new GenericInstanceMethod((MethodReference) methodg.ElementMethod.Relink(relinker, context));
                foreach (TypeReference arg in methodg.GenericArguments)
                    // Generic arguments for the generic instance are often given by the next higher provider.
                    gim.GenericArguments.Add(arg.Relink(relinker, context));

                return (MethodReference) relinker(gim, context);
            }

            MethodReference relink = new MethodReference(method.Name, method.ReturnType, method.DeclaringType.Relink(relinker, context));

            relink.CallingConvention = method.CallingConvention;
            relink.ExplicitThis = method.ExplicitThis;
            relink.HasThis = method.HasThis;

            foreach (GenericParameter param in method.GenericParameters)
                relink.GenericParameters.Add(param.Relink(relinker, context));

            relink.ReturnType = relink.ReturnType?.Relink(relinker, relink);

            foreach (ParameterDefinition param in method.Parameters) {
                param.ParameterType = param.ParameterType.Relink(relinker, method);
                relink.Parameters.Add(param);
            }

            return (MethodReference) relinker(relink, context);
        }

        /// <summary>
        /// Relink the given callsite.
        /// </summary>
        /// <param name="method">The reference to relink.</param>
        /// <param name="relinker">The relinker to use during the relinking process.</param>
        /// <param name="context">The generic context provided to relink generic references.</param>
        /// <returns>A relinked reference.</returns>
        public static CallSite Relink(this CallSite method, Relinker relinker, IGenericParameterProvider context) {
            CallSite relink = new CallSite(method.ReturnType);

            relink.CallingConvention = method.CallingConvention;
            relink.ExplicitThis = method.ExplicitThis;
            relink.HasThis = method.HasThis;

            relink.ReturnType = relink.ReturnType?.Relink(relinker, context);

            foreach (ParameterDefinition param in method.Parameters) {
                param.ParameterType = param.ParameterType.Relink(relinker, context);
                relink.Parameters.Add(param);
            }

            return (CallSite) relinker(relink, context);
        }

        /// <summary>
        /// Relink the given field reference.
        /// </summary>
        /// <param name="field">The reference to relink.</param>
        /// <param name="relinker">The relinker to use during the relinking process.</param>
        /// <param name="context">The generic context provided to relink generic references.</param>
        /// <returns>A relinked reference.</returns>
        public static IMetadataTokenProvider Relink(this FieldReference field, Relinker relinker, IGenericParameterProvider context) {
            TypeReference declaringType = field.DeclaringType.Relink(relinker, context);
            return relinker(new FieldReference(field.Name, field.FieldType.Relink(relinker, declaringType), declaringType), context);
        }

        /// <summary>
        /// Relink the given parameter definition.
        /// </summary>
        /// <param name="param">The reference to relink.</param>
        /// <param name="relinker">The relinker to use during the relinking process.</param>
        /// <param name="context">The generic context provided to relink generic references.</param>
        /// <returns>A relinked reference.</returns>
        public static ParameterDefinition Relink(this ParameterDefinition param, Relinker relinker, IGenericParameterProvider context) {
            param = (param.Method as MethodReference)?.Parameters[param.Index] ?? param;
            ParameterDefinition newParam = new ParameterDefinition(param.Name, param.Attributes, param.ParameterType.Relink(relinker, context)) {
                IsIn = param.IsIn,
                IsLcid = param.IsLcid,
                IsOptional = param.IsOptional,
                IsOut = param.IsOut,
                IsReturnValue = param.IsReturnValue,
                MarshalInfo = param.MarshalInfo
            };
            if (param.HasConstant)
                newParam.Constant = param.Constant;
            return newParam;
        }

        /// <summary>
        /// Clone the given parameter definition.
        /// </summary>
        /// <param name="param">The original parameter definition.</param>
        /// <returns>A clone of the original parameter definition.</returns>
        public static ParameterDefinition Clone(this ParameterDefinition param) {
            ParameterDefinition newParam = new ParameterDefinition(param.Name, param.Attributes, param.ParameterType) {
                IsIn = param.IsIn,
                IsLcid = param.IsLcid,
                IsOptional = param.IsOptional,
                IsOut = param.IsOut,
                IsReturnValue = param.IsReturnValue,
                MarshalInfo = param.MarshalInfo
            };
            if (param.HasConstant)
                newParam.Constant = param.Constant;
            foreach (CustomAttribute attrib in param.CustomAttributes)
                newParam.CustomAttributes.Add(attrib.Clone());
            return newParam;
        }

        /// <summary>
        /// Relink the given custom attribute.
        /// </summary>
        /// <param name="attrib">The reference to relink.</param>
        /// <param name="relinker">The relinker to use during the relinking process.</param>
        /// <param name="context">The generic context provided to relink generic references.</param>
        /// <returns>A relinked reference.</returns>
        public static CustomAttribute Relink(this CustomAttribute attrib, Relinker relinker, IGenericParameterProvider context) {
            CustomAttribute newAttrib = new CustomAttribute((MethodReference) attrib.Constructor.Relink(relinker, context));
            foreach (CustomAttributeArgument attribArg in attrib.ConstructorArguments)
                newAttrib.ConstructorArguments.Add(new CustomAttributeArgument(attribArg.Type.Relink(relinker, context), attribArg.Value));
            foreach (CustomAttributeNamedArgument attribArg in attrib.Fields)
                newAttrib.Fields.Add(new CustomAttributeNamedArgument(attribArg.Name,
                    new CustomAttributeArgument(attribArg.Argument.Type.Relink(relinker, context), attribArg.Argument.Value))
                );
            foreach (CustomAttributeNamedArgument attribArg in attrib.Properties)
                newAttrib.Properties.Add(new CustomAttributeNamedArgument(attribArg.Name,
                    new CustomAttributeArgument(attribArg.Argument.Type.Relink(relinker, context), attribArg.Argument.Value))
                );
            return newAttrib;
        }

        /// <summary>
        /// Clone the given custom attribute.
        /// </summary>
        /// <param name="attrib">The original custom attribute.</param>
        /// <returns>A clone of the original custom attribute.</returns>
        public static CustomAttribute Clone(this CustomAttribute attrib) {
            CustomAttribute newAttrib = new CustomAttribute(attrib.Constructor);
            foreach (CustomAttributeArgument attribArg in attrib.ConstructorArguments)
                newAttrib.ConstructorArguments.Add(new CustomAttributeArgument(attribArg.Type, attribArg.Value));
            foreach (CustomAttributeNamedArgument attribArg in attrib.Fields)
                newAttrib.Fields.Add(new CustomAttributeNamedArgument(attribArg.Name,
                    new CustomAttributeArgument(attribArg.Argument.Type, attribArg.Argument.Value))
                );
            foreach (CustomAttributeNamedArgument attribArg in attrib.Properties)
                newAttrib.Properties.Add(new CustomAttributeNamedArgument(attribArg.Name,
                    new CustomAttributeArgument(attribArg.Argument.Type, attribArg.Argument.Value))
                );
            return newAttrib;
        }

        /// <summary>
        /// Relink the given generic parameter reference.
        /// </summary>
        /// <param name="param">The reference to relink.</param>
        /// <param name="relinker">The relinker to use during the relinking process.</param>
        /// <param name="context">The generic context provided to relink generic references.</param>
        /// <returns>A relinked reference.</returns>
        public static GenericParameter Relink(this GenericParameter param, Relinker relinker, IGenericParameterProvider context) {
            GenericParameter newParam = new GenericParameter(param.Name, param.Owner) {
                Attributes = param.Attributes
            }.Update(param.Position, param.Type);
            foreach (CustomAttribute attr in param.CustomAttributes)
                newParam.CustomAttributes.Add(attr.Relink(relinker, context));
#pragma warning disable IDE0008 // TypeReference in cecil 0.10, GenericParameterConstraint in cecil 0.11
            foreach (var constraint in param.Constraints)
#pragma warning restore IDE0008
                newParam.Constraints.Add(constraint.Relink(relinker, context));
            return newParam;
        }

        /// <summary>
        /// Clone the given generic parameter.
        /// </summary>
        /// <param name="param">The original generic parameter.</param>
        /// <returns>A clone of the original generic parameter.</returns>
        public static GenericParameter Clone(this GenericParameter param) {
            GenericParameter newParam = new GenericParameter(param.Name, param.Owner) {
                Attributes = param.Attributes
            }.Update(param.Position, param.Type);
            foreach (CustomAttribute attr in param.CustomAttributes)
                newParam.CustomAttributes.Add(attr.Clone());
#pragma warning disable IDE0008 // TypeReference in cecil 0.10, GenericParameterConstraint in cecil 0.11
            foreach (var constraint in param.Constraints)
#pragma warning restore IDE0008
                newParam.Constraints.Add(constraint);
            return newParam;
        }

    }
}
