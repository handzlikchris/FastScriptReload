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
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public class QuickCodeIterationManager
{
    private static List<FileSystemWatcher> _fileWatchers = new List<FileSystemWatcher>();
    private static PlayModeStateChange LastPlayModeStateChange;

    private static QuickCodeIterationManager _instance;
    public static QuickCodeIterationManager Instance => _instance ??= new QuickCodeIterationManager();

    private void OnWatchedFileChange(object source, FileSystemEventArgs e)
    {
        if (LastPlayModeStateChange != PlayModeStateChange.EnteredPlayMode)
        {
#if QuickCodeIterationManager_DebugEnabled
            Debug.Log($"Application not playing, change to: {e.Name} won't be compiled and hot reloaded"); //TODO: remove when not in testing?
#endif
            return;
        }
        
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
        EditorApplication.playModeStateChanged += OnEditorApplicationOnplayModeStateChanged;
        
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Instance.SetupTestOnly();
            // Instance.DynamicallyUpdateMethodsInWatchedFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts\Runtime\ClassDoDynamicallyUpdate.cs");
        }
    }

    private static void OnEditorApplicationOnplayModeStateChanged(PlayModeStateChange obj)
    {
        LastPlayModeStateChange = obj;
    }

    private void SetupTestOnly()
    {
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts\Runtime\ClassDoDynamicallyUpdate.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts\Runtime\OtherClassToDynamicallyUpdate.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Examples\Scripts\FunctionLibrary.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Examples\Scripts\Graph.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Examples\Scripts\ExistingSingletonTest.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Examples\Scripts\SingletonAccessorTest.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\Scripts\ExistingSingletonTest.cs");
        StartWatchingFile(@"E:\_src-unity\QuickCodeIteration\Assets\Scripts\OtherSingletonTest.cs");
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
        
        var referencesToAdd = new List<string>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(a => excludeAssyNames.All(assyName => !a.FullName.StartsWith(assyName) 
                                                                  && a.GetCustomAttribute<DynamicallyCreatedAssemblyAttribute>() == null))) 
        {
            try
            {
                if (string.IsNullOrEmpty(assembly.Location))
                {
                    throw new Exception("Assembly location is null");
                }
                referencesToAdd.Add(assembly.Location);;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unable to add a reference to assembly as unable to get location or null: {assembly.FullName} when hot-reloading, this is likely dynamic assembly and won't cause issues");
            }
        }

        //TODO: work out why 120? is it for every project, or also dependant on other factors like actual project location?
        //TODO: it's not 120 - for main project it seemed to be but for this one is not, something else is at play - need to work out
        const int MaxPathCharsInReferenceLocationBeforeExceptionThrown = 250; 
        foreach (var referenceToAdd in referencesToAdd.Where(r => r.Length < MaxPathCharsInReferenceLocationBeforeExceptionThrown))
        {
            param.ReferencedAssemblies.Add(referenceToAdd);
        }
        
        var referencesOverPathLenghtLimit = referencesToAdd.Where(r => r.Length >= MaxPathCharsInReferenceLocationBeforeExceptionThrown).ToList();
        if (referencesOverPathLenghtLimit.Count > 0)
        {
            Debug.LogWarning($"Assembly references locations are over allowed {MaxPathCharsInReferenceLocationBeforeExceptionThrown} this seems to be existing limitation which will prevent assembly from being compiled," +
                             $"currently there's no known fix - if possible moving those assembles (probably whole project) to root level of drive and shortening project folder name could help." +
                             $"\r\nReferences:{string.Join(Environment.NewLine, referencesOverPathLenghtLimit)}");
        }
        
        param.GenerateExecutable = false;
        param.GenerateInMemory = false; //TODO: move back to memory once it can be properly serialized into byte array without file?

        var dynamicallyCreatedAssemblyAttributeSourceCore = GenerateSourceCodeForAddCustomAttributeToGeneratedAssembly(param, provider, typeof(DynamicallyCreatedAssemblyAttribute), Guid.NewGuid().ToString());
        
        //prevent namespace clash, and add new lines to ensure code doesn't end / start with a comment which would cause compilation issues, nested namespaces are fine
        var sourceCodeNestedInNamespaceToPreventSameTypeClash = $"namespace {AssemblyChangesLoader.NAMESPACE_ADDED_FOR_CREATED_CLASS}{Environment.NewLine}{{{source} {Environment.NewLine}}}";
        var result = provider.CompileAssemblyFromSource(param, sourceCodeNestedInNamespaceToPreventSameTypeClash, dynamicallyCreatedAssemblyAttributeSourceCore);
    
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
