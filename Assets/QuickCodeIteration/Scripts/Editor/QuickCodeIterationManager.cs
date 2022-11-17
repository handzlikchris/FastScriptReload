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
using QuickCodeIteration.Scripts.Runtime;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public class QuickCodeIterationManager
{
    private static List<FileSystemWatcher> _fileWatchers = new List<FileSystemWatcher>();

    private static QuickCodeIterationManager _instance;
    public static QuickCodeIterationManager Instance => _instance ??= new QuickCodeIterationManager();

    private void OnWatchedFileChange(object source, FileSystemEventArgs e)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            DynamicallyUpdateMethodsInWatchedFile(e.FullPath);
            Debug.Log($"File: {e.Name} changed - recompiled (took {stopwatch.ElapsedMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error when updating '{e.FullPath}', {ex}");
        }
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

    static QuickCodeIterationManager()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Instance.SetupTestOnly();
            // Instance.DynamicallyUpdateMethodsInWatchedFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts\Runtime\ClassDoDynamicallyUpdate.cs");
        }
    }

    private void SetupTestOnly()
    {
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts\Runtime\ClassDoDynamicallyUpdate.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts\Runtime\OtherClassToDynamicallyUpdate.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Examples\Scripts\FunctionLibrary.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Examples\Scripts\Graph.cs");
    }
    
    public void DynamicallyUpdateMethodsInWatchedFile(string fullFilePath)
    {
        var fileCode = File.ReadAllText(fullFilePath);
        var dynamicallyLoadedAssemblyWithUpdates = Compile(fileCode); //TODO: make sure to unload old assy and destory

        // AssemblyChangesLoader.DynamicallyUpdateMethodsForCreatedAssembly(fullFilePath, dynamicallyLoadedAssemblyWithUpdates); //TODO: reenable
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
                     .Where(a => excludeAssyNames.All(assyName => !a.FullName.StartsWith(assyName) 
                                                                  && a.GetCustomAttribute<DynamicallyCreatedAssemblyAttribute>() == null))) {
            param.ReferencedAssemblies.Add(assembly.Location);
        }
        
        param.GenerateExecutable = false;
        param.GenerateInMemory = false; //TODO: move back to memory once it can be properly serialized into byte array without file?

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
        
        //TODO: get that refactored so it's not always executing
        CompiledDllSender.Instance.SendDll(File.ReadAllBytes(result.CompiledAssembly.Location));
        
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
}
