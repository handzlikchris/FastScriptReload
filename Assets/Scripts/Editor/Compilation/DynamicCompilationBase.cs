using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FastScriptReload.Runtime;
using ImmersiveVRTools.Editor.Common.Cache;
using ImmersiveVrToolsCommon.Runtime.Logging;
using UnityEditor;

namespace FastScriptReload.Editor.Compilation
{
    [InitializeOnLoad]
    public class DynamicCompilationBase
    {
        public static bool LogHowToFixMessageOnCompilationError;
	    public static bool EnableExperimentalThisCallLimitationFix;
        public static List<string> ReferencesExcludedFromHotReload = new List<string>();
        
        public static readonly string[] ActiveScriptCompilationDefines;
        protected static readonly string DynamicallyCreatedAssemblyAttributeSourceCode = $"[assembly: {typeof(DynamicallyCreatedAssemblyAttribute).FullName}()]";
        private static readonly string AssemblyCsharpFullPath;
        
        static DynamicCompilationBase()
        {
            //needs to be set from main thread
            ActiveScriptCompilationDefines = EditorUserBuildSettings.activeScriptCompilationDefines;
            AssemblyCsharpFullPath = SessionStateCache.GetOrCreateString(
	            $"FSR:AssemblyCsharpFullPath", 
	            () => AssetDatabase.FindAssets("Microsoft.CSharp")
					            .Select(g => new System.IO.FileInfo(UnityEngine.Application.dataPath + "/../" + AssetDatabase.GUIDToAssetPath(g)))
					            .First(fi => fi.Name.ToLower() == "Microsoft.CSharp.dll".ToLower()).FullName
	        );

        }

        protected static List<string> ResolveReferencesToAdd(List<string> excludeAssyNames)
        {
            var referencesToAdd = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain
                         .GetAssemblies() //TODO: PERF: just need to load once and cache? or get assembly based on changed file only?
                         .Where(a => excludeAssyNames.All(assyName => !a.FullName.StartsWith(assyName))
												&& CustomAttributeExtensions.GetCustomAttribute<DynamicallyCreatedAssemblyAttribute>((Assembly)a) == null))
            {
                try
                {
                    if (string.IsNullOrEmpty(assembly.Location))
                    {
                        LoggerScoped.LogDebug($"FastScriptReload: Assembly location is null, usually dynamic assembly, harmless.");
                        continue;
                    }

                    referencesToAdd.Add(assembly.Location);
                }
                catch (Exception)
                {
                    LoggerScoped.LogDebug($"Unable to add a reference to assembly as unable to get location or null: {assembly.FullName} when hot-reloading, this is likely dynamic assembly and won't cause issues");
                }
            }
            
            referencesToAdd = referencesToAdd.Where(r => !ReferencesExcludedFromHotReload.Any(rTe => r.EndsWith(rTe))).ToList();

            if (EnableExperimentalThisCallLimitationFix || FastScriptReloadManager.Instance.AssemblyChangesLoaderEditorOptionsNeededInBuild.EnableExperimentalAddedFieldsSupport)
            {
	            IncludeMicrosoftCsharpReferenceToSupportDynamicKeyword(referencesToAdd);
            }

            return referencesToAdd;
        }

        private static void IncludeMicrosoftCsharpReferenceToSupportDynamicKeyword(List<string> referencesToAdd)
        {
	        //TODO: check .net4.5 backend not breaking?
	        //ThisRewriters will cast to dynamic - if using .NET Standard 2.1 - reference is required
	        referencesToAdd.Add(AssemblyCsharpFullPath);
	        // referencesToAdd.Add(@"C:\Program Files\Unity\Hub\Editor\2021.3.12f1\Editor\Data\UnityReferenceAssemblies\unity-4.8-api\Microsoft.CSharp.dll");
        }
    }

    public class CreateSourceCodeCombinedContentsResult
    {
        public string SourceCode { get; }
        public List<string> TypeNamesDefinitions { get; }

        public CreateSourceCodeCombinedContentsResult(string sourceCode, List<string> typeNamesDefinitions)
        {
            SourceCode = sourceCode;
            TypeNamesDefinitions = typeNamesDefinitions;
        }
    }
}