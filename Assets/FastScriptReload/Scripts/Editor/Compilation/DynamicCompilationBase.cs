using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FastScriptReload.Editor.Compilation.CodeRewriting;
using FastScriptReload.Runtime;
using ImmersiveVRTools.Editor.Common.Cache;
using ImmersiveVRTools.Runtime.Common.Utilities;
using ImmersiveVrToolsCommon.Runtime.Logging;
using Microsoft.CodeAnalysis.CSharp;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace FastScriptReload.Editor.Compilation
{
    [InitializeOnLoad]
    public class DynamicCompilationBase
    {
	    public static bool LogHowToFixMessageOnCompilationError;
	    public static bool EnableExperimentalThisCallLimitationFix;

	    public const string DebuggingInformationComment = 
@"// DEBUGGING READ-ME 
//
// To debug simply add a breakpoint in this file.
// 
// With every code change - new file is generated, currently you'll need to re-set breakpoints after each change.
// You can also:
//    - step into the function that was changed (and that will get you to correct source file)
//    - add a function breakpoint in your IDE (this way you won't have to re-add it every time)
//
// Tool can automatically open dynamically-compiled code file every time to make setting breakpoints easier.
// You can adjust that behaviour via 'Window -> FastScriptReload -> Start Screen -> Debugging -> Do not auto-open generated cs file'.
//
// You can always open generated file when needed by clicking link in console, eg.
// 'FSR: Files: FunctionLibrary.cs changed (click here to debug [in bottom details pane]) - compilation (took 240ms)'


";
	    
        protected static readonly string[] ActiveScriptCompilationDefines;
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
        
        protected static string CreateSourceCodeCombinedContents(IEnumerable<string> fileSourceCode)
        {
            var combinedUsingStatements = new List<string>();
            
            var sourceCodeWithAdjustments = fileSourceCode.Select(fileCode =>
            {
                var tree = CSharpSyntaxTree.ParseText(fileCode);
                var root = tree.GetRoot();
                var rewriter = new HotReloadCompliantRewriter();

                //WARN: application order is important, eg ctors need to happen before class names as otherwise ctors will not be recognised as ctors
                if (FastScriptReloadManager.Instance.EnableExperimentalThisCallLimitationFix)
                {
					root = new ThisCallRewriter().Visit(root);
					root = new ThisAssignmentRewriter().Visit(root);
                }

                if (FastScriptReloadManager.Instance.EnableExperimentalThisCallLimitationFix)
                {
	                var allTypes = ReflectionHelper.GetAllTypes(); //TODO: PERF: can't get all in this manner, just needed for classes in file
	                var fieldsWalker = new FieldsWalker();
	                fieldsWalker.Visit(root);
	                var typeToFieldDeclarations = fieldsWalker.GetTypeToFieldDeclarations();
	                root = new NewFieldsRewriter(typeToFieldDeclarations, allTypes).Visit(root);
                }
                
                root = new ConstructorRewriter( adjustCtorOnlyForNonNestedTypes: true).Visit(root);
                root = rewriter.Visit(root);
                
                combinedUsingStatements.AddRange(rewriter.StrippedUsingDirectives);

                return root.ToFullString();
            }).ToList();

            var sourceCodeCombinedSb = new StringBuilder();
            sourceCodeCombinedSb.Append(DebuggingInformationComment);
            
            foreach (var usingStatement in combinedUsingStatements.Distinct())
            {
                sourceCodeCombinedSb.Append(usingStatement);
            }

            foreach (var sourceCodeWithAdjustment in sourceCodeWithAdjustments)
            {
                sourceCodeCombinedSb.AppendLine(sourceCodeWithAdjustment);
            }
            
            LoggerScoped.LogDebug("Source Code Created:\r\n\r\n" + sourceCodeCombinedSb);
            return sourceCodeCombinedSb.ToString();
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
                        throw new Exception("FastScriptReload: Assembly location is null");
                    }

                    referencesToAdd.Add(assembly.Location);
                }
                catch (Exception)
                {
                    Debug.LogWarning($"FastScriptReload: Unable to add a reference to assembly as unable to get location or null: {assembly.FullName} when hot-reloading, this is likely dynamic assembly and won't cause issues");
                }
            }

            if (EnableExperimentalThisCallLimitationFix)
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
}