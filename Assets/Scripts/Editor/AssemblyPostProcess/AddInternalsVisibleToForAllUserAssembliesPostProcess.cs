using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FastScriptReload.Editor.AssemblyPostProcess
{
    [InitializeOnLoad]
    public static class AddInternalsVisibleToForAllUserAssembliesPostProcess
    {
        public static readonly DirectoryInfo AdjustedAssemblyRoot;

        private static readonly Assembly CecilAssembly;
        private static readonly Type AssemblyDefinitionType;
        private static readonly Type ReaderParametersType;
        private static readonly Type CustomAttributeType;
        private static readonly Type CustomAttributeArgumentType;

        static AddInternalsVisibleToForAllUserAssembliesPostProcess()
        {
            AdjustedAssemblyRoot = new DirectoryInfo(Path.Combine(Application.dataPath, "..", "Temp", "Fast Script Reload", "AdjustedDlls"));

            CecilAssembly = typeof(HarmonyLib.Harmony).Assembly;
            AssemblyDefinitionType = CecilAssembly.GetType("Mono.Cecil.AssemblyDefinition");
            ReaderParametersType = CecilAssembly.GetType("Mono.Cecil.ReaderParameters");
            CustomAttributeType = CecilAssembly.GetType("Mono.Cecil.CustomAttribute");
            CustomAttributeArgumentType = CecilAssembly.GetType("Mono.Cecil.CustomAttributeArgument");
        }

        public static string CreateAssemblyWithInternalsContentsVisibleTo(Assembly changedAssembly, string visibleToAssemblyName)
        {
            if (!AdjustedAssemblyRoot.Exists)
                AdjustedAssemblyRoot.Create();

            // var readerParams = new ReaderParameters { ReadWrite = false };
            var readerParams = Activator.CreateInstance(ReaderParametersType);
            ReaderParametersType.GetProperty("ReadWrite").SetValue(readerParams, false);

            // var assembly = AssemblyDefinition.ReadAssembly(path, readerParams);
            var readAssemblyMethod = AssemblyDefinitionType.GetMethod("ReadAssembly", new[] { typeof(string), ReaderParametersType });
            var assemblyDef = readAssemblyMethod.Invoke(null, new[] { changedAssembly.Location, readerParams });

            try
            {
                // var mainModule = assembly.MainModule;
                var mainModule = AssemblyDefinitionType.GetProperty("MainModule").GetValue(assemblyDef);
                var moduleDefinitionType = mainModule.GetType();

                // var attributeCtor = mainModule.ImportReference(typeof(InternalsVisibleToAttribute).GetConstructor(...));
                var importRefMethod = moduleDefinitionType.GetMethod("ImportReference", new[] { typeof(MethodBase) });
                var attrCtorInfo = typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute).GetConstructor(new[] { typeof(string) });
                var attributeCtor = importRefMethod.Invoke(mainModule, new object[] { attrCtorInfo });

                // var attribute = new CustomAttribute(attributeCtor);
                var attribute = Activator.CreateInstance(CustomAttributeType, attributeCtor);

                // var typeSystem = mainModule.TypeSystem;
                var typeSystem = moduleDefinitionType.GetProperty("TypeSystem").GetValue(mainModule);
                var stringTypeRef = typeSystem.GetType().GetProperty("String").GetValue(typeSystem);

                // attribute.ConstructorArguments.Add(new CustomAttributeArgument(mainModule.TypeSystem.String, visibleToAssemblyName));
                var ctorArg = Activator.CreateInstance(CustomAttributeArgumentType, stringTypeRef, visibleToAssemblyName);
                var ctorArgs = (IList)CustomAttributeType.GetProperty("ConstructorArguments").GetValue(attribute);
                ctorArgs.GetType().GetMethod("Add").Invoke(ctorArgs, new[] { ctorArg });

                // assembly.CustomAttributes.Add(attribute);
                var customAttrs = (IList)AssemblyDefinitionType.GetProperty("CustomAttributes").GetValue(assemblyDef);
                customAttrs.GetType().GetMethod("Add").Invoke(customAttrs, new[] { attribute });

                // var newAssemblyPath = ...;
                var assemblyName = AssemblyDefinitionType.GetProperty("Name").GetValue(assemblyDef);
                var assemblyNameStr = (string)assemblyName.GetType().GetProperty("Name").GetValue(assemblyName);
                var newAssemblyPath = new FileInfo(Path.Combine(AdjustedAssemblyRoot.FullName, assemblyNameStr) + ".dll").FullName;

                // assembly.Write(newAssemblyPath);
                AssemblyDefinitionType.GetMethod("Write", new[] { typeof(string) }).Invoke(assemblyDef, new object[] { newAssemblyPath });

                return newAssemblyPath;
            }
            finally
            {
                // AssemblyDefinition is IDisposable
                if (assemblyDef is IDisposable disposable)
                    disposable.Dispose();
            }
        }
    }
}