#if !CECIL0_9
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
using System.IO;
using System.Security;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    sealed class MMReflectionImporter : IReflectionImporter {

        private class _Provider : IReflectionImporterProvider {
            public bool? UseDefault;
            public IReflectionImporter GetReflectionImporter(ModuleDefinition module) {
                MMReflectionImporter importer = new MMReflectionImporter(module);
                if (UseDefault != null)
                    importer.UseDefault = UseDefault.Value;
                return importer;
            }
        }
        
        // Not all generics are equal: in some cases a type with a generic parameter should be 
        // considered as a TypeReference with GenericParameters. For instance Bar<T> in
        //
        // class Foo<T> : Bar<T>
        //
        // In other cases, a type should be considered as a GenericInstanceType.
        // For instance `self` in
        //
        // class Foo<T> { static Foo<T> self; }
        //
        // Because in Reflection API both cases yield technically the same TypeInfo, we
        // differentiate then during resolving to allow proper resolving of the second example
        // The same thing is done in Cecil, so we port a simplified version of it
        private enum GenericImportKind {
            Open,
            Definition
        }

        public static readonly IReflectionImporterProvider Provider = new _Provider();
        public static readonly IReflectionImporterProvider ProviderNoDefault = new _Provider() { UseDefault = false };

        private readonly ModuleDefinition Module;
        private readonly DefaultReflectionImporter Default;

        private readonly Dictionary<Assembly, AssemblyNameReference> CachedAsms = new Dictionary<Assembly, AssemblyNameReference>();
        private readonly Dictionary<Module, TypeReference> CachedModuleTypes = new Dictionary<Module, TypeReference>();
        private readonly Dictionary<Type, TypeReference> CachedTypes = new Dictionary<Type, TypeReference>();
        private readonly Dictionary<FieldInfo, FieldReference> CachedFields = new Dictionary<FieldInfo, FieldReference>();
        private readonly Dictionary<MethodBase, MethodReference> CachedMethods = new Dictionary<MethodBase, MethodReference>();

        public bool UseDefault = false;

        private readonly Dictionary<Type, TypeReference> ElementTypes;

        public MMReflectionImporter(ModuleDefinition module) {
            Module = module;
            Default = new DefaultReflectionImporter(module);

            ElementTypes = new Dictionary<Type, TypeReference>() {
                { typeof(void), module.TypeSystem.Void },
                { typeof(bool), module.TypeSystem.Boolean },
                { typeof(char), module.TypeSystem.Char },
                { typeof(sbyte), module.TypeSystem.SByte },
                { typeof(byte), module.TypeSystem.Byte },
                { typeof(short), module.TypeSystem.Int16 },
                { typeof(ushort), module.TypeSystem.UInt16 },
                { typeof(int), module.TypeSystem.Int32 },
                { typeof(uint), module.TypeSystem.UInt32 },
                { typeof(long), module.TypeSystem.Int64 },
                { typeof(ulong), module.TypeSystem.UInt64 },
                { typeof(float), module.TypeSystem.Single },
                { typeof(double), module.TypeSystem.Double },
                { typeof(string), module.TypeSystem.String },
                { typeof(TypedReference), module.TypeSystem.TypedReference },
                { typeof(IntPtr), module.TypeSystem.IntPtr },
                { typeof(UIntPtr), module.TypeSystem.UIntPtr },
                { typeof(object), module.TypeSystem.Object },
            };
        }

        private bool TryGetCachedType(Type type, out TypeReference typeRef, GenericImportKind importKind) {
            if (importKind == GenericImportKind.Definition) {
                typeRef = null;
                return false;
            }

            return CachedTypes.TryGetValue(type, out typeRef);
        }

        private TypeReference SetCachedType(Type type, TypeReference typeRef, GenericImportKind importKind) {
            if (importKind == GenericImportKind.Definition)
                return typeRef;

            return CachedTypes[type] = typeRef;
        }

        [Obsolete("Please use the Assembly overload instead.")]
        public AssemblyNameReference ImportReference(AssemblyName asm) {
            // Multiple ALCs are pain and you should feel bad if you're not using the Assembly overload. - ade
            return Default.ImportReference(asm);
        }

        public AssemblyNameReference ImportReference(Assembly asm) {
            if (CachedAsms.TryGetValue(asm, out AssemblyNameReference asmRef))
                return asmRef;

            asmRef = Default.ImportReference(asm.GetName());
            // It's possible to load multiple assemblies with the same name but different contents!
            // Assembly load contexts are pain. (And this can even happen without ALCs!)
            asmRef.ApplyRuntimeHash(asm);
            return CachedAsms[asm] = asmRef;
        }

        public TypeReference ImportModuleType(Module module, IGenericParameterProvider context) {
            if (CachedModuleTypes.TryGetValue(module, out TypeReference typeRef))
                return typeRef;

            // See https://github.com/jbevain/cecil/blob/06da31930ff100cef48aef677c4ceeee858e6c04/Mono.Cecil/ModuleDefinition.cs#L1018
            return CachedModuleTypes[module] = new TypeReference(
                string.Empty,
                "<Module>",
                Module,
                ImportReference(module.Assembly)
            );
        }

        public TypeReference ImportReference(Type type, IGenericParameterProvider context) {
            return _ImportReference(type, context, context != null ? GenericImportKind.Open : GenericImportKind.Definition);
        }

        private bool _IsGenericInstance(Type type, GenericImportKind importKind) {
            return type.IsGenericType && !type.IsGenericTypeDefinition ||
                   type.IsGenericType && type.IsGenericTypeDefinition && importKind == GenericImportKind.Open;
        }

        private GenericInstanceType _ImportGenericInstance(Type type, IGenericParameterProvider context, TypeReference typeRef) {
            GenericInstanceType git = new GenericInstanceType(typeRef);
            foreach (Type arg in type.GetGenericArguments())
                git.GenericArguments.Add(_ImportReference(arg, context));
            return git;
        }

        private TypeReference _ImportReference(Type type, IGenericParameterProvider context, GenericImportKind importKind = GenericImportKind.Open) {
            if (TryGetCachedType(type, out TypeReference typeRef, importKind)) {
                return _IsGenericInstance(type, importKind) ? _ImportGenericInstance(type, context, typeRef) : typeRef;
            }

            if (UseDefault)
                return SetCachedType(type, Default.ImportReference(type, context), importKind);

            if (type.HasElementType) {
                if (type.IsByRef)
                    return SetCachedType(type, new ByReferenceType(_ImportReference(type.GetElementType(), context)), importKind);

                if (type.IsPointer)
                    return SetCachedType(type, new PointerType(_ImportReference(type.GetElementType(), context)), importKind);

                if (type.IsArray) {
                    ArrayType at = new ArrayType(_ImportReference(type.GetElementType(), context), type.GetArrayRank());
                    if (type != type.GetElementType().MakeArrayType()) {
                        // Non-SzArray
                        // TODO: Find a way to get the bounds without instantiating the array type!
                        /*
                        Array a = Array.CreateInstance(type, new int[type.GetArrayRank()]);
                        if (
                            at.Rank > 1
                            && a.IsFixedSize
                        ) {
                            for (int i = 0; i < at.Rank; i++)
                                at.Dimensions[i] = new ArrayDimension(a.GetLowerBound(i), a.GetUpperBound(i));
                        }
                        */
                        // For now, always assume [0...,0...,
                        // Array.CreateInstance only accepts lower bounds anyway.
                        for (int i = 0; i < at.Rank; i++)
                            at.Dimensions[i] = new ArrayDimension(0, null);
                    }
                    return CachedTypes[type] = at;
                }
            }
            
            if (_IsGenericInstance(type, importKind)) {
                return _ImportGenericInstance(type, context,
                    _ImportReference(type.GetGenericTypeDefinition(), context, GenericImportKind.Definition));
            }

            if (type.IsGenericParameter)
                return SetCachedType(type, ImportGenericParameter(type, context), importKind);

            if (ElementTypes.TryGetValue(type, out typeRef))
                return SetCachedType(type, typeRef, importKind);

            typeRef = new TypeReference(
				string.Empty,
				type.Name,
				Module,
				ImportReference(type.Assembly),
                type.IsValueType
            );

            if (type.IsNested)
                typeRef.DeclaringType = _ImportReference(type.DeclaringType, context, importKind);
            else if (type.Namespace != null)
                typeRef.Namespace = type.Namespace;

            if (type.IsGenericType)
                foreach (Type param in type.GetGenericArguments())
                    typeRef.GenericParameters.Add(new GenericParameter(param.Name, typeRef));

            return SetCachedType(type, typeRef, importKind);
        }

        private static TypeReference ImportGenericParameter(Type type, IGenericParameterProvider context) {
            if (context is MethodReference ctxMethodRef) {
                MethodBase dclMethod = type.DeclaringMethod;
                if (dclMethod != null) {
                    return ctxMethodRef.GenericParameters[type.GenericParameterPosition];
                } else {
                    context = ctxMethodRef.DeclaringType;
                }
            }

            Type dclType = type.DeclaringType;
            if (dclType == null)
                throw new InvalidOperationException();

            if (context is TypeReference ctxTypeRef) {
                while (ctxTypeRef != null) {
                    TypeReference ctxTypeRefEl = ctxTypeRef.GetElementType();
                    if (ctxTypeRefEl.Is(dclType))
                        return ctxTypeRefEl.GenericParameters[type.GenericParameterPosition];

                    if (ctxTypeRef.Is(dclType))
                        return ctxTypeRef.GenericParameters[type.GenericParameterPosition];

                    ctxTypeRef = ctxTypeRef.DeclaringType;
                    continue;
                }
            }

            throw new NotSupportedException();
        }

        public FieldReference ImportReference(FieldInfo field, IGenericParameterProvider context) {
            if (CachedFields.TryGetValue(field, out FieldReference fieldRef))
                return fieldRef;

            if (UseDefault)
                return CachedFields[field] = Default.ImportReference(field, context);

            Type declType = field.DeclaringType;
            TypeReference declaringType = declType != null ? ImportReference(declType, context) : ImportModuleType(field.Module, context);

            FieldInfo fieldOrig = field;
            if (declType != null && declType.IsGenericType) {
                // In methods of generic types, all generic parameters are already filled in.
                // Meanwhile, cecil requires generic parameter references.
                // Luckily the metadata tokens match up.
                field = field.Module.ResolveField(field.MetadataToken);
            }

            return CachedFields[fieldOrig] = new FieldReference(
                field.Name,
                _ImportReference(field.FieldType, declaringType),
                declaringType
            );
        }

        public MethodReference ImportReference(MethodBase method, IGenericParameterProvider context) {
            return _ImportReference(method, context,
                context != null ? GenericImportKind.Open : GenericImportKind.Definition);
        }

        private MethodReference _ImportReference(MethodBase method, IGenericParameterProvider context, GenericImportKind importKind) {
            if (CachedMethods.TryGetValue(method, out MethodReference methodRef) && importKind == GenericImportKind.Open)
                return methodRef;

            if (method is MethodInfo target && target.IsDynamicMethod())
                return new DynamicMethodReference(Module, target);

            if (UseDefault)
                return CachedMethods[method] = Default.ImportReference(method, context);

            if (method.IsGenericMethod && !method.IsGenericMethodDefinition ||
                method.IsGenericMethod && method.IsGenericMethodDefinition && importKind == GenericImportKind.Open) {
                GenericInstanceMethod gim = new GenericInstanceMethod(_ImportReference((method as MethodInfo).GetGenericMethodDefinition(), context, GenericImportKind.Definition));
                foreach (Type arg in method.GetGenericArguments())
                    // Generic arguments for the generic instance are often given by the next higher provider.
                    gim.GenericArguments.Add(_ImportReference(arg, context));

                return CachedMethods[method] = gim;
            }

            Type declType = method.DeclaringType;
            methodRef = new MethodReference(
                method.Name,
                _ImportReference(typeof(void), context),
                declType != null ? _ImportReference(declType, context, GenericImportKind.Definition) : ImportModuleType(method.Module, context)
            );

            methodRef.HasThis = (method.CallingConvention & CallingConventions.HasThis) != 0;
            methodRef.ExplicitThis = (method.CallingConvention & CallingConventions.ExplicitThis) != 0;
            if ((method.CallingConvention & CallingConventions.VarArgs) != 0)
                methodRef.CallingConvention = MethodCallingConvention.VarArg;

            MethodBase methodOrig = method;
            if (declType != null && declType.IsGenericType) {
                // In methods of generic types, all generic parameters are already filled in.
                // Meanwhile, cecil requires generic parameter references.
                // Luckily the metadata tokens match up.
                method = method.Module.ResolveMethod(method.MetadataToken);
            }

            if (method.IsGenericMethodDefinition)
                foreach (Type param in method.GetGenericArguments())
                    methodRef.GenericParameters.Add(new GenericParameter(param.Name, methodRef));

            methodRef.ReturnType = _ImportReference((method as MethodInfo)?.ReturnType ?? typeof(void), methodRef);

            foreach (ParameterInfo param in method.GetParameters())
                methodRef.Parameters.Add(new ParameterDefinition(
                    param.Name,
                    (Mono.Cecil.ParameterAttributes) param.Attributes,
                    _ImportReference(param.ParameterType, methodRef)
                ));

            return CachedMethods[methodOrig] = methodRef;
        }

    }
}
#endif
