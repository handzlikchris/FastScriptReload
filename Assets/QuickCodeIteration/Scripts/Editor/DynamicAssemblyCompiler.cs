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
                referencesToAdd.Add(assembly.Location);;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Unable to add a reference to assembly as unable to get location or null: {assembly.FullName} when hot-reloading, this is likely dynamic assembly and won't cause issues");
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