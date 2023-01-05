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
using System.Security;
using System.Security.Permissions;
using System.Diagnostics.SymbolStore;
using ExceptionHandler = Mono.Cecil.Cil.ExceptionHandler;
using System.Globalization;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    sealed partial class DynamicMethodDefinition : IDisposable {

        static DynamicMethodDefinition() {
            _InitCopier();
        }

        internal static readonly bool _IsNewMonoSRE = ReflectionHelper.IsMono && typeof(DynamicMethod).GetField("il_info", BindingFlags.NonPublic | BindingFlags.Instance) != null;
        internal static readonly bool _IsOldMonoSRE = ReflectionHelper.IsMono && !_IsNewMonoSRE && typeof(DynamicMethod).GetField("ilgen", BindingFlags.NonPublic | BindingFlags.Instance) != null;

        // If SRE has been stubbed out, prefer Cecil.
        private static bool _PreferCecil =
            (ReflectionHelper.IsMono && (
                // Mono 4.X+
                !_IsNewMonoSRE &&
                // Unity pre 2018
                !_IsOldMonoSRE
            )) ||
                
            (!ReflectionHelper.IsMono && (
                // .NET
                typeof(ILGenerator).Assembly
                .GetType("System.Reflection.Emit.DynamicILGenerator")
                ?.GetField("m_scope", BindingFlags.NonPublic | BindingFlags.Instance) == null
            )) ||
                
            false;

        public static bool IsDynamicILAvailable => !_PreferCecil;

        internal static readonly ConstructorInfo c_DebuggableAttribute = typeof(DebuggableAttribute).GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) });
        internal static readonly ConstructorInfo c_UnverifiableCodeAttribute = typeof(UnverifiableCodeAttribute).GetConstructor(new Type[] { });
        internal static readonly ConstructorInfo c_IgnoresAccessChecksToAttribute = typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute).GetConstructor(new Type[] { typeof(string) });

        internal static readonly Type t__IDMDGenerator = typeof(_IDMDGenerator);
        internal static readonly Dictionary<string, _IDMDGenerator> _DMDGeneratorCache = new Dictionary<string, _IDMDGenerator>();

        [Obsolete("Use OriginalMethod instead.")]
        public MethodBase Method => OriginalMethod;
        public MethodBase OriginalMethod { get; private set; }
        private MethodDefinition _Definition;
        public MethodDefinition Definition => _Definition;
        private ModuleDefinition _Module;
        public ModuleDefinition Module => _Module;

        public string Name;

        public Type OwnerType;

        public bool Debug = false;

        private Guid GUID = Guid.NewGuid();

        private bool _IsDisposed;

        internal DynamicMethodDefinition() {
            Debug = Environment.GetEnvironmentVariable("MONOMOD_DMD_DEBUG") == "1";
        }

        public DynamicMethodDefinition(MethodBase method)
            : this() {
            OriginalMethod = method ?? throw new ArgumentNullException(nameof(method));
            Reload();
        }

        public DynamicMethodDefinition(string name, Type returnType, Type[] parameterTypes)
            : this() {
            Name = name;
            OriginalMethod = null;

            _CreateDynModule(name, returnType, parameterTypes);
        }

        public ILProcessor GetILProcessor() {
            return Definition.Body.GetILProcessor();
        }

        public ILGenerator GetILGenerator() {
            return new Cil.CecilILGenerator(Definition.Body.GetILProcessor()).GetProxy();
        }

        private ModuleDefinition _CreateDynModule(string name, Type returnType, Type[] parameterTypes) {
            ModuleDefinition module = _Module = ModuleDefinition.CreateModule($"DMD:DynModule<{name}>?{GetHashCode()}", new ModuleParameters() {
                Kind = ModuleKind.Dll,
#if !CECIL0_9
                ReflectionImporterProvider = MMReflectionImporter.ProviderNoDefault
#endif
            });

            TypeDefinition type = new TypeDefinition(
                "",
                $"DMD<{name}>?{GetHashCode()}",
                Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class
            );
            module.Types.Add(type);

            MethodDefinition def = _Definition = new MethodDefinition(
                name,
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                returnType != null ? module.ImportReference(returnType) : module.TypeSystem.Void
            );
            foreach (Type paramType in parameterTypes)
                def.Parameters.Add(new ParameterDefinition(module.ImportReference(paramType)));
            type.Methods.Add(def);

            return module;
        }

        public void Reload() {
            MethodBase orig = OriginalMethod;
            if (orig == null)
                throw new InvalidOperationException();

            ModuleDefinition module = null;

            try {
                _Definition = null;

#if !CECIL0_9
                _Module?.Dispose();
#endif
                _Module = null;

                Type[] argTypes;
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

                module = _CreateDynModule(orig.GetID(simple: true), (orig as MethodInfo)?.ReturnType, argTypes);

                _CopyMethodToDefinition();

                MethodDefinition def = Definition;
                if (!orig.IsStatic) {
                    def.Parameters[0].Name = "this";
                }
                for (int i = 0; i < args.Length; i++)
                    def.Parameters[i + offs].Name = args[i].Name;

                _Module = module;
                module = null;
            } catch {
#if !CECIL0_9
                module?.Dispose();
#endif
                throw;
            }
        }

        public MethodInfo Generate()
            => Generate(null);
        public MethodInfo Generate(object context) {
            string typeName = Environment.GetEnvironmentVariable("MONOMOD_DMD_TYPE");

            switch (typeName?.ToLower(CultureInfo.InvariantCulture)) {
                case "dynamicmethod":
                case "dm":
                    return DMDEmitDynamicMethodGenerator.Generate(this, context);

#if !NETSTANDARD
                case "methodbuilder":
                case "mb":
                    return DMDEmitMethodBuilderGenerator.Generate(this, context);
#endif

                case "cecil":
                case "md":
                    return DMDCecilGenerator.Generate(this, context);

                default:
                    Type type = ReflectionHelper.GetType(typeName);
                    if (type != null) {
                        if (!t__IDMDGenerator.IsCompatible(type))
                            throw new ArgumentException($"Invalid DMDGenerator type: {typeName}");
                        if (!_DMDGeneratorCache.TryGetValue(typeName, out _IDMDGenerator gen))
                            _DMDGeneratorCache[typeName] = gen = Activator.CreateInstance(type) as _IDMDGenerator;
                        return gen.Generate(this, context);
                    }

                    if (_PreferCecil)
                        return DMDCecilGenerator.Generate(this, context);

                    if (Debug)
#if NETSTANDARD
                        return DMDCecilGenerator.Generate(this, context);
#else
                        return DMDEmitMethodBuilderGenerator.Generate(this, context);
#endif

                    // In .NET Framework, DynamicILGenerator doesn't support fault and filter blocks.
                    // This is a non-issue in .NET Core, yet it could still be an issue in mono.
                    // https://github.com/dotnet/coreclr/issues/1764
#if NETFRAMEWORK
                    if (Definition.Body.ExceptionHandlers.Any(eh =>
                        eh.HandlerType == ExceptionHandlerType.Fault ||
                        eh.HandlerType == ExceptionHandlerType.Filter
                    ))
#if NETSTANDARD
                        return DMDCecilGenerator.Generate(this, context);
#else
                        return DMDEmitMethodBuilderGenerator.Generate(this, context);
#endif
#endif

                    return DMDEmitDynamicMethodGenerator.Generate(this, context);
            }
        }

        public void Dispose() {
            if (_IsDisposed)
                return;
            _IsDisposed = true;
            _Module.Dispose();
        }

        public string GetDumpName(string type) {
            // TODO: Add {Definition.GetID(withType: false)} without killing MethodBuilder
            return $"DMDASM.{GUID.GetHashCode():X8}{(string.IsNullOrEmpty(type) ? "" : $".{type}")}";
        }

    }
}
