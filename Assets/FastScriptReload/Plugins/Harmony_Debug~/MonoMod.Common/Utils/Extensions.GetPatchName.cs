using System;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Text;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    static partial class Extensions {

        /// <summary>
        /// Get the "patch name" - the name of the target to patch - for the given member.
        /// </summary>
        /// <param name="mr">The member to get the patch name for.</param>
        /// <returns>The patch name.</returns>
        public static string GetPatchName(this MemberReference mr) {
            return (mr as ICustomAttributeProvider)?.GetPatchName() ?? mr.Name;
        }
        /// <summary>
        /// Get the "patch name" - the name of the target to patch - for the given member.
        /// </summary>
        /// <param name="mr">The member to get the patch name for.</param>
        /// <returns>The patch name.</returns>
        public static string GetPatchFullName(this MemberReference mr) {
            return (mr as ICustomAttributeProvider)?.GetPatchFullName(mr) ?? mr.FullName;
        }

        private static string GetPatchName(this ICustomAttributeProvider cap) {
            string name;

            CustomAttribute patchAttrib = cap.GetCustomAttribute("MonoMod.MonoModPatch");
            if (patchAttrib != null) {
                name = (string) patchAttrib.ConstructorArguments[0].Value;
                int dotIndex = name.LastIndexOf('.');
                if (dotIndex != -1 && dotIndex != name.Length - 1) {
                    name = name.Substring(dotIndex + 1);
                }
                return name;
            }

            // Backwards-compatibility: Check for patch_
            name = ((MemberReference) cap).Name;
            return name.StartsWith("patch_", StringComparison.Ordinal) ? name.Substring(6) : name;
        }
        private static string GetPatchFullName(this ICustomAttributeProvider cap, MemberReference mr) {
            if (cap is TypeReference type) {
                CustomAttribute patchAttrib = cap.GetCustomAttribute("MonoMod.MonoModPatch");
                string name;

                if (patchAttrib != null) {
                    name = (string) patchAttrib.ConstructorArguments[0].Value;
                } else {
                    // Backwards-compatibility: Check for patch_
                    name = ((MemberReference) cap).Name;
                    name = name.StartsWith("patch_", StringComparison.Ordinal) ? name.Substring(6) : name;
                }

                if (name.StartsWith("global::", StringComparison.Ordinal))
                    name = name.Substring(8); // Patch name is refering to a global type.
                else if (name.Contains(".", StringComparison.Ordinal) || name.Contains("/", StringComparison.Ordinal)) { } // Patch name is already a full name.
                else if (!string.IsNullOrEmpty(type.Namespace))
                    name = type.Namespace + "." + name;
                else if (type.IsNested)
                    name = type.DeclaringType.GetPatchFullName() + "/" + name;

                if (mr is TypeSpecification) {
                    // Collect TypeSpecifications and append formats back to front.
                    List<TypeSpecification> formats = new List<TypeSpecification>();
                    TypeSpecification ts = (TypeSpecification) mr;
                    do {
                        formats.Add(ts);
                    } while ((ts = (ts.ElementType as TypeSpecification)) != null);

                    StringBuilder builder = new StringBuilder(name.Length + formats.Count * 4);
                    builder.Append(name);
                    for (int formati = formats.Count - 1; formati > -1; --formati) {
                        ts = formats[formati];

                        if (ts.IsByReference)
                            builder.Append("&");
                        else if (ts.IsPointer)
                            builder.Append("*");
                        else if (ts.IsPinned) { } // FullName not overriden.
                        else if (ts.IsSentinel) { } // FullName not overriden.
                        else if (ts.IsArray) {
                            ArrayType array = (ArrayType) ts;
                            if (array.IsVector)
                                builder.Append("[]");
                            else {
                                builder.Append("[");
                                for (int i = 0; i < array.Dimensions.Count; i++) {
                                    if (i > 0)
                                        builder.Append(",");
                                    builder.Append(array.Dimensions[i].ToString());
                                }
                                builder.Append("]");
                            }
                        } else if (ts.IsRequiredModifier)
                            builder.Append("modreq(").Append(((RequiredModifierType) ts).ModifierType).Append(")");
                        else if (ts.IsOptionalModifier)
                            builder.Append("modopt(").Append(((OptionalModifierType) ts).ModifierType).Append(")");
                        else if (ts.IsGenericInstance) {
                            GenericInstanceType gen = (GenericInstanceType) ts;
                            builder.Append("<");
                            for (int i = 0; i < gen.GenericArguments.Count; i++) {
                                if (i > 0)
                                    builder.Append(",");
                                builder.Append(gen.GenericArguments[i].GetPatchFullName());
                            }
                            builder.Append(">");
                        } else if (ts.IsFunctionPointer) {
                            FunctionPointerType fpt = (FunctionPointerType) ts;
                            builder.Append(" ").Append(fpt.ReturnType.GetPatchFullName()).Append(" *(");
                            if (fpt.HasParameters)
                                for (int i = 0; i < fpt.Parameters.Count; i++) {
                                    ParameterDefinition parameter = fpt.Parameters[i];
                                    if (i > 0)
                                        builder.Append(",");

                                    if (parameter.ParameterType.IsSentinel)
                                        builder.Append("...,");

                                    builder.Append(parameter.ParameterType.FullName);
                                }
                            builder.Append(")");
                        } else
                            throw new NotSupportedException($"MonoMod can't handle TypeSpecification: {type.FullName} ({type.GetType()})");
                    }

                    name = builder.ToString();
                }

                return name;
            }

            if (cap is FieldReference field) {
                return $"{field.FieldType.GetPatchFullName()} {field.DeclaringType.GetPatchFullName()}::{cap.GetPatchName()}";
            }

            if (cap is MethodReference)
                throw new InvalidOperationException("GetPatchFullName not supported on MethodReferences - use GetID instead");

            throw new InvalidOperationException($"GetPatchFullName not supported on type {cap.GetType()}");
        }

    }
}
