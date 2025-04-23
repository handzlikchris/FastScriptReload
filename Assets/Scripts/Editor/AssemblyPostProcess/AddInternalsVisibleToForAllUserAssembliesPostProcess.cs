using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using UnityEditor;
using UnityEngine;

namespace FastScriptReload.Editor.AssemblyPostProcess
{
    [InitializeOnLoad]
    public static class AddInternalsVisibleToForAllUserAssembliesPostProcess
    {
        public static readonly DirectoryInfo AdjustedAssemblyRoot;

        static AddInternalsVisibleToForAllUserAssembliesPostProcess()
        {
            AdjustedAssemblyRoot = new DirectoryInfo(Path.Combine(Application.dataPath, "..", "Temp", "Fast Script Reload", "AdjustedDlls"));
        }

        public static string CreateAssemblyWithInternalsContentsVisibleTo(Assembly changedAssembly, string visibleToAssemblyName)
        {
            if (!AdjustedAssemblyRoot.Exists)
                AdjustedAssemblyRoot.Create();

            var assemblyResolver = new UnityMonoEditorAssemblyResolver(changedAssembly.Location);
            using (var assembly = AssemblyDefinition.ReadAssembly(changedAssembly.Location, new ReaderParameters { ReadWrite = false, AssemblyResolver = assemblyResolver }))
            {
                var mainModule = assembly.MainModule;

                var attributeCtor = mainModule.ImportReference(
                    typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute).GetConstructor(new[] { typeof(string) })
                );

                var attribute = new CustomAttribute(attributeCtor);
                attribute.ConstructorArguments.Add(
                    new CustomAttributeArgument(mainModule.TypeSystem.String, visibleToAssemblyName)
                );

                assembly.CustomAttributes.Add(attribute);

                var newAssemblyPath = new FileInfo(Path.Combine(AdjustedAssemblyRoot.FullName, assembly.Name.Name) + ".dll").FullName;
                assembly.Write(newAssemblyPath);

                return newAssemblyPath;
            }
        }
    }

    class UnityMonoEditorAssemblyResolver : IAssemblyResolver
    {
        static readonly HashSet<string> s_unityEditorSearchDirs;
        static UnityMonoEditorAssemblyResolver()
        {
            // s_unityEditorSearchDirs will be populated on each domain reload
            s_unityEditorSearchDirs = new();
            foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (loadedAssembly.IsDynamic) continue;
                if (!File.Exists(loadedAssembly.Location)) continue;
                var assemblyDir = Path.GetDirectoryName(loadedAssembly.Location);
                s_unityEditorSearchDirs.Add(assemblyDir);
            }
        }

        private Dictionary<string, AssemblyDefinition> cache;
        private string majorSearchDir;

        public UnityMonoEditorAssemblyResolver(string targetAssemblyPath)
        {
            cache = new();
            majorSearchDir = Path.GetDirectoryName(targetAssemblyPath);
        }

        private AssemblyDefinition GetAssembly(string file, ReaderParameters parameters)
        {
            parameters.AssemblyResolver ??= this;
            return ModuleDefinition.ReadModule(file, parameters).Assembly;
        }

        private bool TryResolveAssemblyInDirectory(string directory, ReaderParameters parameters, AssemblyNameReference name, out AssemblyDefinition assembly)
        {
            const string AssemblyExtension = ".dll";
            var filepath = Path.Combine(directory, name.Name + AssemblyExtension);
            assembly = null;
            if (File.Exists(filepath))
            {
                try
                {
                    assembly = GetAssembly(filepath, parameters);
                    return true;
                }
                catch { }
            }

            return false;
        }

        private AssemblyDefinition ResolveInternalCacheLess(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (TryResolveAssemblyInDirectory(majorSearchDir, parameters, name, out var assembly))
            {
                return assembly;
            }

            foreach (var dir in s_unityEditorSearchDirs)
            {
                if (TryResolveAssemblyInDirectory(dir, parameters, name, out assembly))
                {
                    return assembly;
                }
            }

            throw new AssemblyResolutionException(name);
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            return Resolve(name, new ReaderParameters());
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (!cache.TryGetValue(name.FullName, out var value))
            {
                cache[name.FullName] = value = ResolveInternalCacheLess(name, parameters);
            }

            return value;
        }

        public void Dispose()
        {
            foreach (var (path, assembly) in cache)
            {
                assembly.Dispose();
            }

            cache.Clear();
        }

    }
}