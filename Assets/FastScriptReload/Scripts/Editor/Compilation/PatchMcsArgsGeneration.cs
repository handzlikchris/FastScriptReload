#if FastScriptReload_CompileViaMCS
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;
using FastScriptReload.Runtime;
using HarmonyLib;
using UnityEditor;

[InitializeOnLoad]
[PreventHotReload]
public class PatchMcsArgsGeneration
{
    public const string PreprocessorDirectivesProviderOptionsKey = "PreprocessorDirectives";

    static PatchMcsArgsGeneration()
    {
        var harmony = new Harmony(nameof(PatchMcsArgsGeneration));

        var original = AccessTools.Method("Microsoft.CSharp.CSharpCodeGenerator:BuildArgs");
        var postfix = AccessTools.Method(typeof(PatchMcsArgsGeneration), nameof(BuildArgsPostfix));

        harmony.Patch(original, postfix: new HarmonyMethod(postfix));
    }
    
    //Copied from Microsoft.CSharp.CSharpCodeGenerator.BuildArgs
    private static void BuildArgsPostfix(
        CompilerParameters options,
        string[] fileNames,
        IDictionary<string, string> providerOptions,
        ref string __result)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if (options.GenerateExecutable)
            stringBuilder.Append("/target:exe ");
        else
            stringBuilder.Append("/target:library ");
        string privateBinPath = AppDomain.CurrentDomain.SetupInformation.PrivateBinPath;
        if (privateBinPath != null && privateBinPath.Length > 0)
            stringBuilder.AppendFormat("/lib:\"{0}\" ", (object) privateBinPath);
        if (options.Win32Resource != null)
            stringBuilder.AppendFormat("/win32res:\"{0}\" ", (object) options.Win32Resource);
        if (options.IncludeDebugInformation)
            stringBuilder.Append("/debug+ /optimize- ");
        else
            stringBuilder.Append("/debug- /optimize+ ");
        if (options.TreatWarningsAsErrors)
            stringBuilder.Append("/warnaserror ");
        if (options.WarningLevel >= 0)
            stringBuilder.AppendFormat("/warn:{0} ", (object) options.WarningLevel);
        if (options.OutputAssembly == null || options.OutputAssembly.Length == 0)
        {
            string extension = options.GenerateExecutable ? "exe" : "dll"; //TODO:readd
            // options.OutputAssembly = CSharpCodeGenerator.GetTempFileNameWithExtension(options.TempFiles, extension, !options.GenerateInMemory);
        }
        stringBuilder.AppendFormat("/out:\"{0}\" ", (object) options.OutputAssembly);
        foreach (string referencedAssembly in options.ReferencedAssemblies)
        {
            if (referencedAssembly != null && referencedAssembly.Length != 0)
                stringBuilder.AppendFormat("/r:\"{0}\" ", (object) referencedAssembly);
        }
        if (options.CompilerOptions != null)
        {
            stringBuilder.Append(options.CompilerOptions);
            stringBuilder.Append(" ");
        }
        foreach (string embeddedResource in options.EmbeddedResources)
            stringBuilder.AppendFormat("/resource:\"{0}\" ", (object) embeddedResource);
        foreach (string linkedResource in options.LinkedResources)
            stringBuilder.AppendFormat("/linkresource:\"{0}\" ", (object) linkedResource);
        
        //WARN: that's how it's in source, quite odd, doesn't do much if compiler version specified?
        // if (providerOptions != null && providerOptions.Count > 0)
        // {
        //     string str;
        //     if (!providerOptions.TryGetValue("CompilerVersion", out str))
        //         str = "3.5";
        //     if (str.Length >= 1 && str[0] == 'v')
        //         str = str.Substring(1);
        //     if (str != "2.0")
        //     {
        //     }
        //     else
        //         stringBuilder.Append("/langversion:ISO-2 ");  
        // }
        
        stringBuilder.Append("/langversion:experimental ");   
        
        CustomPatchAdditionAddPreprocessorDirectives(providerOptions, stringBuilder);

        stringBuilder.Append("/noconfig ");
        stringBuilder.Append(" -- ");
        foreach (string fileName in fileNames)
            stringBuilder.AppendFormat("\"{0}\" ", (object) fileName);

        __result = stringBuilder.ToString();
    }

    private static void CustomPatchAdditionAddPreprocessorDirectives(IDictionary<string, string> providerOptions, StringBuilder stringBuilder)
    {
        if (providerOptions != null && providerOptions.Count > 0)
        {
            if (providerOptions.TryGetValue(PreprocessorDirectivesProviderOptionsKey, out var preprocessorDirectives))
            {
                stringBuilder.Append($"/d:\"{preprocessorDirectives}\" ");
            }
        }
    }
}
#endif