using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp;
using QuickCodeIteration.Scripts.Runtime;
using Debug = UnityEngine.Debug;

public class DynamicAssemblyCompiler
{
    private const int ReferenceLenghtCountWarningThreshold = 32767 - 2000; //windows can accept up to 32767 chars as args, then it starts thorowing exceptions. MCS.exe is adding references via command /r:<full path>

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
            catch (Exception ex)
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
            
            //TODO: the process is started from Microsoft.CSharp.CSharpCodeGenerator.GenerateCodeFromExpression, potentially all it'd be possible to patch that class to maybe copy all
            //assemblies to some top-level location and change parameters to run from this folder, with a working directory set, this would drastically reduce char count used by full refs
            //also mcs.exe allows to compile with -pkg:package1[,packageN], which somewhat bundles multiple references, maybe all unity engine refs could go in there, or all refs in general
        }

        param.ReferencedAssemblies.AddRange(referencesToAdd.ToArray());
        param.GenerateExecutable = false;
        param.GenerateInMemory = compileOnlyInMemory;

        var dynamicallyCreatedAssemblyAttributeSourceCore = GenerateSourceCodeForAddCustomAttributeToGeneratedAssembly(param, provider, typeof(DynamicallyCreatedAssemblyAttribute), Guid.NewGuid().ToString());
        
        //prevent namespace clash, and add new lines to ensure code doesn't end / start with a comment which would cause compilation issues, nested namespaces are fine
        var sourceCodeNestedInNamespaceToPreventSameTypeClash = fileSourceCode.Select(fSc => $"namespace {AssemblyChangesLoader.NAMESPACE_ADDED_FOR_CREATED_CLASS}{Environment.NewLine}{{{fSc} {Environment.NewLine}}}");
        var sourceCodeCombined = string.Join(Environment.NewLine, sourceCodeNestedInNamespaceToPreventSameTypeClash);
        Debug.Log($"Files: {string.Join(",", filePathsWithSourceCode.Select(fn => new FileInfo(fn).Name))} changed - compilation (took {sw.ElapsedMilliseconds}ms)");
        
        return provider.CompileAssemblyFromSource(param, sourceCodeCombined, dynamicallyCreatedAssemblyAttributeSourceCore);
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