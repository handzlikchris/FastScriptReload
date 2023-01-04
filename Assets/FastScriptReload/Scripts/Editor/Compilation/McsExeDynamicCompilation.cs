using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if FastScriptReload_CompileViaMCS
public class McsExeDynamicCompilation : DynamicCompilationBase
{
    private const int ReferenceLenghtCountWarningThreshold = 32767 - 2000; //windows can accept up to 32767 chars as args, then it starts thorowing exceptions. MCS.exe is adding references via command /r:<full path>
    
    private static CompileResult Compile(List<string> filePathsWithSourceCode)
    {
        var fileSourceCode = filePathsWithSourceCode.Select(File.ReadAllText);

        var providerOptions = new Dictionary<string, string>();
        var provider = new Microsoft.CSharp.CSharpCodeProvider(providerOptions);
        var param = new  System.CodeDom.Compiler.CompilerParameters();

        var excludeAssyNames = new List<string> 
        {
            "mscorlib"
        };
        var referencesToAdd = ResolveReferencesToAdd(excludeAssyNames);

        var referencePathCharLenght = referencesToAdd.Sum(r => r.Length);
        if (referencePathCharLenght > ReferenceLenghtCountWarningThreshold)
        {
            Debug.LogWarning(
                "Windows can accept up to 32767 chars as args, then it starts throwing exceptions. Dynamic compilation will use MCS.exe and will add references via command /r:<full path>, " +
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
        param.GenerateInMemory = false;
        providerOptions.Add(PatchMcsArgsGeneration.PreprocessorDirectivesProviderOptionsKey,
            string.Join(";", ActiveScriptCompilationDefines));
        
        var sourceCodeCombined = CreateSourceCodeCombinedContents(fileSourceCode);
        var result = provider.CompileAssemblyFromSource(param, sourceCodeCombined, DynamicallyCreatedAssemblyAttributeSourceCode);
        var errors = new List<string>();
        foreach (var error in result.Errors)
        {
            errors.Add(error.ToString());
        }
        return new CompileResult(
            result.CompiledAssembly.FullName,
            errors,
            result.NativeCompilerReturnValue,
            result.CompiledAssembly,
            sourceCodeCombined,
            string.Empty
        );
    }
}
#endif