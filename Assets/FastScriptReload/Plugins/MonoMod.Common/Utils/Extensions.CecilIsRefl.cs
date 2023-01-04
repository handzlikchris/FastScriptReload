using System;
using System.Reflection;
using SRE = System.Reflection.Emit;
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

        /// <summary>
        /// Check if the signatures of a given System.Reflection and Mono.Cecil member reference match.
        /// </summary>
        /// <param name="minfo">The System.Reflection member reference.</param>
        /// <param name="mref">The Mono.Cecil member reference.</param>
        /// <returns>True if both references share the same signature, false otherwise.</returns>
        public static bool Is(this MemberInfo minfo, MemberReference mref)
            => mref.Is(minfo);
        /// <summary>
        /// Check if the signatures of a given System.Reflection and Mono.Cecil member reference match.
        /// </summary>
        /// <param name="mref">The Mono.Cecil member reference.</param>
        /// <param name="minfo">The System.Reflection member reference.</param>
        /// <returns>True if both references share the same signature, false otherwise.</returns>
        public static bool Is(this MemberReference mref, MemberInfo minfo) {
            if (mref == null)
                return false;

            TypeReference mrefDecl = mref.DeclaringType;
            if (mrefDecl?.FullName == "<Module>")
                mrefDecl = null;

            if (mref is GenericParameter genParamRef) {
                if (!(minfo is Type genParamInfo))
                    return false;
                
                if (!genParamInfo.IsGenericParameter) {
                    if (genParamRef.Owner is IGenericInstance genParamRefOwner)
                        return genParamRefOwner.GenericArguments[genParamRef.Position].Is(genParamInfo);
                    else
                        return false;
                }

                // Don't check owner as it introduces a circular check.
                /*
                if (!(genParamRef.Owner as MemberReference).Is(genParamInfo.DeclaringMethod ?? (System.Reflection.MemberInfo) genParamInfo.DeclaringType))
                    return false;
                */
                return genParamRef.Position == genParamInfo.GenericParameterPosition;
            }

            if (minfo.DeclaringType != null) {
                if (mrefDecl == null)
                    return false;

                Type declType = minfo.DeclaringType;

                if (minfo is Type) {
                    // Note: type.DeclaringType is supposed to == type.DeclaringType.GetGenericTypeDefinition()
                    // For whatever reason, old versions of mono (f.e. shipped with Unity 5.0.3) break this,
                    // requiring us to call .GetGenericTypeDefinition() manually instead.
                    if (declType.IsGenericType && !declType.IsGenericTypeDefinition)
                        declType = declType.GetGenericTypeDefinition();
                }

                if (!mrefDecl.Is(declType))
                    return false;

            } else if (mrefDecl != null)
                return false;

            // Note: This doesn't work for TypeSpecification, as the reflection-side type.Name changes with some modifiers (f.e. IsArray).
            if (!(mref is TypeSpecification) && mref.Name != minfo.Name)
                return false;

            if (mref is TypeReference typeRef) {
                if (!(minfo is Type typeInfo))
                    return false;

                if (typeInfo.IsGenericParameter)
                    return false;

                if (mref is GenericInstanceType genTypeRef) {
                    if (!typeInfo.IsGenericType)
                        return false;

                    Collection<TypeReference> gparamRefs = genTypeRef.GenericArguments;
                    Type[] gparamInfos = typeInfo.GetGenericArguments();
                    if (gparamRefs.Count != gparamInfos.Length)
                        return false;

                    for (int i = 0; i < gparamRefs.Count; i++) {
                        if (!gparamRefs[i].Is(gparamInfos[i]))
                            return false;
                    }

                    return genTypeRef.ElementType.Is(typeInfo.GetGenericTypeDefinition());

                } else if (typeRef.HasGenericParameters) {
                    if (!typeInfo.IsGenericType)
                        return false;

                    Collection<GenericParameter> gparamRefs = typeRef.GenericParameters;
                    Type[] gparamInfos = typeInfo.GetGenericArguments();
                    if (gparamRefs.Count != gparamInfos.Length)
                        return false;

                    for (int i = 0; i < gparamRefs.Count; i++) {
                        if (!gparamRefs[i].Is(gparamInfos[i]))
                            return false;
                    }

                } else if (typeInfo.IsGenericType)
                    return false;

                if (mref is ArrayType arrayTypeRef) {
                    if (!typeInfo.IsArray)
                        return false;

                    return arrayTypeRef.Dimensions.Count == typeInfo.GetArrayRank() && arrayTypeRef.ElementType.Is(typeInfo.GetElementType());
                }

                if (mref is ByReferenceType byRefTypeRef) {
                    if (!typeInfo.IsByRef)
                        return false;

                    return byRefTypeRef.ElementType.Is(typeInfo.GetElementType());
                }

                if (mref is PointerType ptrTypeRef) {
                    if (!typeInfo.IsPointer)
                        return false;

                    return ptrTypeRef.ElementType.Is(typeInfo.GetElementType());
                }

                if (mref is TypeSpecification typeSpecRef)
                    // Note: There are TypeSpecifications which map to non-ElementType-y reflection Types.
                    return typeSpecRef.ElementType.Is(typeInfo.HasElementType ? typeInfo.GetElementType() : typeInfo);

                // DeclaringType was already checked before.
                // Avoid converting nested type separators between + (.NET) and / (cecil)
                if (mrefDecl != null)
                    return mref.Name == typeInfo.Name;
                return mref.FullName == typeInfo.FullName.Replace("+", "/", StringComparison.Ordinal);

            } else if (minfo is Type)
                return false;

            if (mref is MethodReference methodRef) {
                if (!(minfo is MethodBase methodInfo))
                    return false;

                Collection<ParameterDefinition> paramRefs = methodRef.Parameters;
                ParameterInfo[] paramInfos = methodInfo.GetParameters();
                if (paramRefs.Count != paramInfos.Length)
                    return false;

                if (mref is GenericInstanceMethod genMethodRef) {
                    if (!methodInfo.IsGenericMethod)
                        return false;

                    Collection<TypeReference> gparamRefs = genMethodRef.GenericArguments;
                    Type[] gparamInfos = methodInfo.GetGenericArguments();
                    if (gparamRefs.Count != gparamInfos.Length)
                        return false;

                    for (int i = 0; i < gparamRefs.Count; i++) {
                        if (!gparamRefs[i].Is(gparamInfos[i]))
                            return false;
                    }

                    return genMethodRef.ElementMethod.Is((methodInfo as System.Reflection.MethodInfo)?.GetGenericMethodDefinition() ?? methodInfo);

                } else if (methodRef.HasGenericParameters) {
                    if (!methodInfo.IsGenericMethod)
                        return false;

                    Collection<GenericParameter> gparamRefs = methodRef.GenericParameters;
                    Type[] gparamInfos = methodInfo.GetGenericArguments();
                    if (gparamRefs.Count != gparamInfos.Length)
                        return false;

                    for (int i = 0; i < gparamRefs.Count; i++) {
                        if (!gparamRefs[i].Is(gparamInfos[i]))
                            return false;
                    }

                } else if (methodInfo.IsGenericMethod)
                    return false;

                Relinker resolver = null;
                resolver = (paramMemberRef, ctx) => paramMemberRef is TypeReference paramTypeRef ? ResolveParameter(paramTypeRef) : paramMemberRef;
                TypeReference ResolveParameter(TypeReference paramTypeRef) {
                    if (paramTypeRef is GenericParameter paramGenParamTypeRef) {
                        if (paramGenParamTypeRef.Owner is MethodReference && methodRef is GenericInstanceMethod paramGenMethodRef)
                            return paramGenMethodRef.GenericArguments[paramGenParamTypeRef.Position];

                        if (paramGenParamTypeRef.Owner is TypeReference paramGenParamTypeRefOwnerType && methodRef.DeclaringType is GenericInstanceType genTypeRefRef &&
                            paramGenParamTypeRefOwnerType.FullName == genTypeRefRef.ElementType.FullName) // This is to prevent List<Tuple<...>> checks from incorrectly checking Tuple's args in List.
                            return genTypeRefRef.GenericArguments[paramGenParamTypeRef.Position];

                        return paramTypeRef;
                    }

                    if (paramTypeRef == methodRef.DeclaringType.GetElementType())
                        return methodRef.DeclaringType;

                    return paramTypeRef;
                }

                if (!methodRef.ReturnType.Relink(resolver, null).Is(((methodInfo as System.Reflection.MethodInfo)?.ReturnType ?? typeof(void))) &&
                    !methodRef.ReturnType.Is(((methodInfo as System.Reflection.MethodInfo)?.ReturnType ?? typeof(void))))
                    return false;

                for (int i = 0; i < paramRefs.Count; i++)
                    if (!paramRefs[i].ParameterType.Relink(resolver, null).Is(paramInfos[i].ParameterType) &&
                        !paramRefs[i].ParameterType.Is(paramInfos[i].ParameterType))
                        return false;

                return true;

            } else if (minfo is MethodInfo)
                return false;

            if (mref is FieldReference != minfo is FieldInfo)
                return false;

            if (mref is PropertyReference != minfo is PropertyInfo)
                return false;

            if (mref is EventReference != minfo is EventInfo)
                return false;

            return true;
        }

    }
}
