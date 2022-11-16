using System;
using System.CodeDom;
using System.Reflection;
using HarmonyLib;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CSharp;
using UnityEngine;
using Debug = UnityEngine.Debug;

//TODO: that's an editor script, move
// [InitializeOnLoad]
public class QuickCodeIterationManager: MonoBehaviour
{
    private static List<FileSystemWatcher> _fileWatchers = new List<FileSystemWatcher>();
    
    private void OnWatchedFileChange(object source, FileSystemEventArgs e)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
        DynamicallyUpdateMethodsInWatchedFile(e.FullPath);
        Debug.Log($"File: {e.Name} changed - recompiled (took {stopwatch.ElapsedMilliseconds}ms)");
    }

    public void StartWatchingFile(string fullFilePath)
    {
        //TODO: make sure file is not already watched
        
        var fileWatcher = new FileSystemWatcher();
        var fileToWatch = new FileInfo(fullFilePath);
        fileWatcher.Path = fileToWatch.Directory.FullName;
        fileWatcher.Filter = fileToWatch.Name;
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
        fileWatcher.Changed += OnWatchedFileChange;
        
        fileWatcher.EnableRaisingEvents = true;
        
        _fileWatchers.Add(fileWatcher);
    }

    private void Start()
    {
        SetupTestOnly();
        DynamicallyUpdateMethodsInWatchedFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts\ClassDoDynamicallyUpdate.cs");
    }

    private void SetupTestOnly()
    {
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts\ClassDoDynamicallyUpdate.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts\OtherClassToDynamicallyUpdate.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Examples\Scripts\FunctionLibrary.cs");
    }
    
    public void DynamicallyUpdateMethodsInWatchedFile(string fullFilePath)
    {
        var fileCode = File.ReadAllText(fullFilePath);
        var dynamicallyLoadedAssemblyWithUpdates = Compile(fileCode); //TODO: make sure to unload old assy and destory

        //TODO: how to unload previously generated assembly?
        var allTypesInNonDynamicGeneratedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.GetCustomAttributes<DynamicallyCreatedAssemblyAttribute>().Any())
            .SelectMany(a => a.GetTypes())
            .ToList();

        var excludeMethodsDefinedOnTypes = new List<Type>
        {
            typeof(MonoBehaviour),
            typeof(Behaviour),
            typeof(UnityEngine.Object),
            typeof(Component),
            typeof(System.Object)
        }; //TODO: move out and possibly define a way to exclude all non-client created code? as this will crash editor

        const BindingFlags ALL_METHODS_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
                                                       BindingFlags.Static | BindingFlags.Instance |
                                                       BindingFlags.FlattenHierarchy; //TODO: move out
        
        foreach (var createdType in  dynamicallyLoadedAssemblyWithUpdates.GetTypes()
                     .Where(t => t.IsClass 
                                 && !typeof(Delegate).IsAssignableFrom(t) //don't redirect delegates
                        )
                 )
        {
            var matchingTypeInExistingAssemblies = allTypesInNonDynamicGeneratedAssemblies.SingleOrDefault(t => t.FullName == createdType.FullName);
            if (matchingTypeInExistingAssemblies != null)
            {
                var allMethodsInExistingType = matchingTypeInExistingAssemblies.GetMethods(ALL_METHODS_BINDING_FLAGS)
                    .Where(m => !excludeMethodsDefinedOnTypes.Contains(m.DeclaringType))
                    .ToList();
                foreach (var createdTypeMethodToUpdate in createdType.GetMethods(ALL_METHODS_BINDING_FLAGS)
                             .Where(m => !excludeMethodsDefinedOnTypes.Contains(m.DeclaringType)))
                {
                    var matchingMethodInExistingType = allMethodsInExistingType.SingleOrDefault(m => m.FullDescription() == createdTypeMethodToUpdate.FullDescription());
                    if (matchingMethodInExistingType != null)
                    {
                        Memory.DetourMethod(matchingMethodInExistingType, createdTypeMethodToUpdate);
                    }
                    else
                    {
                        Debug.LogWarning($"Method: {createdTypeMethodToUpdate.FullDescription()} does not exist in initially compiled type: {matchingTypeInExistingAssemblies.FullName}. " +
                                         $"Adding new methods at runtime is not supported. Make sure to add method before initial compilation.");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Unable to find existing type for: {createdType.FullName} from file: '{fullFilePath}'");    
            }
        }
    }
    
    public static Assembly Compile(string source)
    {
        var providerOptions = new Dictionary<string, string>();
        var provider = new CSharpCodeProvider(providerOptions);
        var param = new CompilerParameters();

        var excludeAssyNames = new List<string> //TODO: move out to field/ separate class
        {
            "mscorlib"
        };
        
        // Add ALL of the assembly references
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(a => excludeAssyNames.All(assyName => !a.FullName.StartsWith(assyName)))) {
            param.ReferencedAssemblies.Add(assembly.Location);
        }
        
        param.GenerateExecutable = false;
        param.GenerateInMemory = true;

        var dynamicallyCreatedAssemblyAttributeSourceCore = GenerateSourceCodeForAddCustomAttributeToGeneratedAssembly(param, provider, typeof(DynamicallyCreatedAssemblyAttribute), Guid.NewGuid().ToString());
        
        var result = provider.CompileAssemblyFromSource(param, source, dynamicallyCreatedAssemblyAttributeSourceCore);
    
        if (result.Errors.Count > 0) {
            var msg = new StringBuilder();
            foreach (CompilerError error in result.Errors) {
                msg.AppendFormat("Error ({0}): {1}\n",
                    error.ErrorNumber, error.ErrorText);
            }
            throw new Exception(msg.ToString());
        }
        
        // Return the assembly
        return result.CompiledAssembly;
    }

    private static string GenerateSourceCodeForAddCustomAttributeToGeneratedAssembly(CompilerParameters param, CSharpCodeProvider provider, Type customAttributeType, 
        string customAttributeStringCtorParam) //warn: not very reusable to force single string param like that
    {
        var dynamicallyCreatedAssemblyAttributeAssemblyLocation = typeof(DynamicallyCreatedAssemblyAttribute).Assembly.Location;
        if (param.ReferencedAssemblies.Contains(dynamicallyCreatedAssemblyAttributeAssemblyLocation))
        {
            param.ReferencedAssemblies.Add(dynamicallyCreatedAssemblyAttributeAssemblyLocation);
        }

        var unit = new CodeCompileUnit();
        var attr = new CodeTypeReference(customAttributeType);
        var decl = new CodeAttributeDeclaration(attr, new CodeAttributeArgument(new CodePrimitiveExpression(customAttributeStringCtorParam)));
        unit.AssemblyCustomAttributes.Add(decl);
        var assemblyInfo = new StringWriter();
        provider.GenerateCodeFromCompileUnit(unit, assemblyInfo, new CodeGeneratorOptions());
        return assemblyInfo.ToString();
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public class DynamicallyCreatedAssemblyAttribute : Attribute
    {
        public string GenerationIdentifier { get; }

        public DynamicallyCreatedAssemblyAttribute(string generationIdentifier)
        {
            GenerationIdentifier = generationIdentifier;
        }
    }
}
