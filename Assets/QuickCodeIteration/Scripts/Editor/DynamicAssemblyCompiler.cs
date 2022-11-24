using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CSharp;
using QuickCodeIteration.Scripts.Runtime;
using UnityEditor;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public class DynamicAssemblyCompiler
{
    private static readonly string[] ActiveScriptCompilationDefines;
    static DynamicAssemblyCompiler()
    {
        //needs to be set from main thread
        ActiveScriptCompilationDefines = EditorUserBuildSettings.activeScriptCompilationDefines;
    }
    
    private const int ReferenceLenghtCountWarningThreshold = 32767 - 2000; //windows can accept up to 32767 chars as args, then it starts thorowing exceptions. MCS.exe is adding references via command /r:<full path>
    private const string TypeNameRegexReplacementPattern = @"(class|struct|enum|delegate)(\W+)(?<typeName>\w+)(:| |\r\n|\n|{)";

    public static CompilerResults Compile(List<string> filePathsWithSourceCode, bool compileOnlyInMemory)
    {
        var sw = new Stopwatch();
        sw.Start();

        var fileSourceCode = filePathsWithSourceCode.Select(File.ReadAllText);
        
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
                                                                  && CustomAttributeExtensions.GetCustomAttribute<DynamicallyCreatedAssemblyAttribute>((Assembly)a) == null))) 
        {
            try
            {
                if (string.IsNullOrEmpty(assembly.Location))
                {
                    throw new Exception("Assembly location is null");
                }
                referencesToAdd.Add(assembly.Location);
            }
            catch (Exception)
            {
                Debug.LogWarning($"Unable to add a reference to assembly as unable to get location or null: {assembly.FullName} when hot-reloading, this is likely dynamic assembly and won't cause issues");
            }
        }

        var referencePathCharLenght = referencesToAdd.Sum(r => r.Length);
        if (referencePathCharLenght > ReferenceLenghtCountWarningThreshold)
        {
            Debug.LogWarning("Windows can accept up to 32767 chars as args, then it starts throwing exceptions. Dynamic compilation will use MCS.exe and will add references via command /r:<full path>, " +
                             $"currently your assembly have {referencesToAdd.Count} references which full paths amount to: {referencePathCharLenght} chars." +
                             $"\r\nIf you see this warning likely compilation will fail, you can:" +
                             $"\r\n1) Move your project to be more top-level, as references take full paths, eg 'c:\\my-source\\stuff\\unity\\my-project\\' - this then gets repeated for many references, moving it close to top level will help" +
                             $"\r\n2) Remove some of the assemblies if you don't need them" +
                             "\r\n Please let me know via support email if that's causing you issues, there may be a fix if it's affecting many users, sorry about that!");
            
            //TODO: the process is started from Microsoft.CSharp.CSharpCodeGenerator.FromFileBatch, potentially all it'd be possible to patch that class to maybe copy all
            //assemblies to some top-level location and change parameters to run from this folder, with a working directory set, this would drastically reduce char count used by full refs
            //also mcs.exe allows to compile with -pkg:package1[,packageN], which somewhat bundles multiple references, maybe all unity engine refs could go in there, or all refs in general
        }

        param.ReferencedAssemblies.AddRange(referencesToAdd.ToArray());
        param.GenerateExecutable = false;
        param.GenerateInMemory = compileOnlyInMemory;
        providerOptions.Add(PatchMcsArgsGeneration.PreprocessorDirectivesProviderOptionsKey, string.Join(";", ActiveScriptCompilationDefines));
        
        var dynamicallyCreatedAssemblyAttributeSourceCore = GenerateSourceCodeForAddCustomAttributeToGeneratedAssembly(param, provider, typeof(DynamicallyCreatedAssemblyAttribute), Guid.NewGuid().ToString());
        
        //TODO: regex is quite problematic, use Roslyn instead? lots of dlls to include, something more lightweight
        var sourceCodeWithClassNamesAdjusted = fileSourceCode.Select(fileCode =>
        {
            var sourceCodeWithClassNamesAdjusted = Regex.Replace(fileCode, TypeNameRegexReplacementPattern,
                    "$1$2${typeName}" + AssemblyChangesLoader.ClassnamePatchedPostfix + "$3");
            
            return Hack_EnsureNestedTypeNamesRemainUnchanged(fileCode, sourceCodeWithClassNamesAdjusted);
        });
        var sourceCodeCombined = string.Join(Environment.NewLine, sourceCodeWithClassNamesAdjusted);
        Debug.Log($"Files: {string.Join(",", filePathsWithSourceCode.Select(fn => new FileInfo(fn).Name))} changed - compilation (took {sw.ElapsedMilliseconds}ms)");
        
        return provider.CompileAssemblyFromSource(param, sourceCodeCombined, dynamicallyCreatedAssemblyAttributeSourceCore);
    }

    private static string Hack_EnsureNestedTypeNamesRemainUnchanged(string fileCode,
        string sourceCodeWithClassNamesAdjusted)
    {
        var matches = Regex.Matches(fileCode, TypeNameRegexReplacementPattern);
        var originalNamesOfAdjustedTypes = matches.Select(m => m.Groups["typeName"].Value).Distinct().ToList();
        foreach (var originalTypeName in originalNamesOfAdjustedTypes)
        {
            var matchingType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
                .FirstOrDefault(t =>
                    t.Name ==
                    originalTypeName); //TODO: that's very weak, it's entirely possible to have same class name across different namespace, without proper source code parsing it's difficult to tell

            if (matchingType != null && matchingType.IsNested)
            {
                //TODO: with proper parsing there'd be no need to adjust like that
                sourceCodeWithClassNamesAdjusted =
                    sourceCodeWithClassNamesAdjusted.Replace(
                        matchingType.Name + AssemblyChangesLoader.ClassnamePatchedPostfix, matchingType.Name);
            }
        }

        return sourceCodeWithClassNamesAdjusted;
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