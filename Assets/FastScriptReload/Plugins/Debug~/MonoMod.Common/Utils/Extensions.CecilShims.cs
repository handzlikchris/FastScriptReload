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

        public static IMetadataTokenProvider ImportReference(this ModuleDefinition mod, IMetadataTokenProvider mtp) {
            if (mtp is TypeReference)
                return mod.ImportReference((TypeReference) mtp);
            if (mtp is FieldReference)
                return mod.ImportReference((FieldReference) mtp);
            if (mtp is MethodReference)
                return mod.ImportReference((MethodReference) mtp);
            return mtp;
        }

#if CECIL0_9
        public static TypeReference ImportReference(this ModuleDefinition mod, TypeReference type)
            => mod.Import(type);
        public static TypeReference ImportReference(this ModuleDefinition mod, Type type, IGenericParameterProvider context)
            => mod.Import(type, context);
        public static FieldReference ImportReference(this ModuleDefinition mod, System.Reflection.FieldInfo field)
            => mod.Import(field);
        public static FieldReference ImportReference(this ModuleDefinition mod, System.Reflection.FieldInfo field, IGenericParameterProvider context)
            => mod.Import(field, context);
        public static MethodReference ImportReference(this ModuleDefinition mod, System.Reflection.MethodBase method)
            => mod.Import(method);
        public static MethodReference ImportReference(this ModuleDefinition mod, System.Reflection.MethodBase method, IGenericParameterProvider context)
            => mod.Import(method, context);
        public static TypeReference ImportReference(this ModuleDefinition mod, TypeReference type, IGenericParameterProvider context)
            => mod.Import(type, context);
        public static TypeReference ImportReference(this ModuleDefinition mod, Type type)
            => mod.Import(type);
        public static FieldReference ImportReference(this ModuleDefinition mod, FieldReference field)
            => mod.Import(field);
        public static MethodReference ImportReference(this ModuleDefinition mod, MethodReference method)
            => mod.Import(method);
        public static MethodReference ImportReference(this ModuleDefinition mod, MethodReference method, IGenericParameterProvider context)
            => mod.Import(method, context);
        public static FieldReference ImportReference(this ModuleDefinition mod, FieldReference field, IGenericParameterProvider context)
            => mod.Import(field, context);
#endif

    }
}
