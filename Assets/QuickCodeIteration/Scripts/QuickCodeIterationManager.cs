using System;
using System.Reflection;
using HarmonyLib;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CSharp;
using UnityEditor;
using UnityEngine;

//TODO: that's an editor script, move
[InitializeOnLoad]
public class QuickCodeIterationManager: MonoBehaviour
{
    private static FileSystemWatcher _fileWatcher;
    
    private static void OnChanged(object source, FileSystemEventArgs e)
    {
        Debug.Log("File changed");
        UpdateWatchedMethods(e.FullPath);
    }
    
    static QuickCodeIterationManager()
    {
        _fileWatcher = new FileSystemWatcher();
        _fileWatcher.Path = @"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts";
        _fileWatcher.Filter = "ClassDoDynamicallyUpdate.cs";

        // Watch for changes in LastAccess and LastWrite times, and
        // the renaming of files or directories.
        _fileWatcher.NotifyFilter = NotifyFilters.LastWrite;

        // Add event handlers
        _fileWatcher.Changed += OnChanged;

        // Begin watching
        _fileWatcher.EnableRaisingEvents = true;
        
        var fileCode = File.ReadAllText(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts\ClassDoDynamicallyUpdate_TestUpdate.cs");
        var dynamicallyLoadedAssemblyWithUpdates = Compile(fileCode); //TODO: make sure to unload old assy and destory

        // var harmony = new Harmony("QuickCodeIterationManager");
        var methodName = "Update"; 
        var methodToOverride = typeof(ClassDoDynamicallyUpdate).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        var replacement = dynamicallyLoadedAssemblyWithUpdates.GetType("ClassDoDynamicallyUpdate_TestUpdate").GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Memory.DetourMethod(methodToOverride, replacement);
        // harmony.Patch(methodToOverride,
        //     // new HarmonyMethod(assembly.GetType("QuickCodeIterationManagerPatchImplementationDynamic").GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public))
        //     // transpiler: CreateTranspilerWithNewCode(methodToOverride)
        //     transpiler: new HarmonyMethod(typeof(QuickCodeIterationManager).GetMethod(nameof(ReplaceWithTargetMethodTranspiler), BindingFlags.Static | BindingFlags.NonPublic))
        // );
    }

    public static void UpdateWatchedMethods(string changedFilePath)
    {
        var fileCode = File.ReadAllText(changedFilePath); //TODO: iterate to correctly add/replace to assembly 

        //TODO: ideally would get class name / location in some different fashion, Roslyn? quite a few assys to add with that
        // var className = dynamicallyLoadedAssemblyWithUpdates.DefinedTypes.First(); //TODO: need to work out hwo to get type if it's in combined assy? perhaps it should not be? one assy for 1 watched file?

        var classNameRegexPattern = @"(class)(\W+)(?<className>\w+)(:| |\r\n|\n|{)";
        var className = Regex.Match(fileCode, classNameRegexPattern).Groups["className"];

        const string patchedClassPostfix = "_Patched";
        var fileCodeWithClassNamePostfix = Regex.Replace(fileCode, classNameRegexPattern, "$1$2${className}" + patchedClassPostfix + "$3");
        
        var dynamicallyLoadedAssemblyWithUpdates = Compile(fileCodeWithClassNamePostfix); //TODO: make sure to unload old assy and destory
        
        var methodName = "Update"; //TODO: later it'll iterate over all watched methods and patch updates
        var methodToOverride = typeof(ClassDoDynamicallyUpdate).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        var replacement = dynamicallyLoadedAssemblyWithUpdates.GetType($"{className}{patchedClassPostfix}").GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        
        Memory.DetourMethod(methodToOverride, replacement);
    }

    [ContextMenu(nameof(DynamicallyUpdateTestUpdate))]
    public void DynamicallyUpdateTestUpdate()
    {
        var fileCode = File.ReadAllText(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Scripts\ClassDoDynamicallyUpdate_RuntimeUpdate.cs");
        var dynamicallyLoadedAssemblyWithUpdates = Compile(fileCode); //TODO: make sure to unload old assy and destory
        
        var methodName = "Update"; 
        var methodToOverride = typeof(ClassDoDynamicallyUpdate).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        var replacement = dynamicallyLoadedAssemblyWithUpdates.GetType("ClassDoDynamicallyUpdate_RuntimeUpdate").GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Memory.DetourMethod(methodToOverride, replacement);
    }
    
    public static Assembly Compile(string source)
    {
        var provider = new CSharpCodeProvider();
        var param = new CompilerParameters();

        var excludeAssyNames = new List<string>
        {
            "mscorlib"
        };
        
        // Add ALL of the assembly references
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(a => excludeAssyNames.All(assyName => !a.FullName.StartsWith(assyName)))) {
            param.ReferencedAssemblies.Add(assembly.Location);
        }
    
        // Add specific assembly references
        //param.ReferencedAssemblies.Add("System.dll");
        //param.ReferencedAssemblies.Add("CSharp.dll");
        //param.ReferencedAssemblies.Add("UnityEngines.dll");
    
        // Generate a dll in memory
        param.GenerateExecutable = false;
        param.GenerateInMemory = true;
    
        // Compile the source
        var result = provider.CompileAssemblyFromSource(param, source);
    
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

    // [HarmonyPatch(typeof(QuickCodeIterationManager))]
    // [HarmonyPatch("Start")]
    // class QuickCodeIterationManagerPatchImplementation
    // {
    //     [HarmonyPrefix]
    //     public static void Prefix(QuickCodeIterationManager __instance)
    //     {
    //         Debug.Log($"Start Prefix");
    //     }
    // }
}
