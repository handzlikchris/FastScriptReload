using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuickCodeIteration.Scripts.Runtime;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace QuickCodeIteration.Scripts.Editor
{
    [InitializeOnLoad]
    public class DynamicCompilationBase
    {
        //TODO: delegates will likely fail
        private const string TypeNameRegexReplacementPattern = @"(class|struct|enum)(\W+)(?<typeName>\w+)(:| |\r\n|\n|{)";
        
        protected static readonly string[] ActiveScriptCompilationDefines;
        protected static readonly string DynamicallyCreatedAssemblyAttributeSourceCode = $"[assembly: QuickCodeIteration.Scripts.Runtime.DynamicallyCreatedAssemblyAttribute()]";

        static DynamicCompilationBase()
        {
            //needs to be set from main thread
            ActiveScriptCompilationDefines = EditorUserBuildSettings.activeScriptCompilationDefines;
        }
        
        protected static string CreateSourceCodeCombinedContents(IEnumerable<string> fileSourceCode)
        {
            var combinedUsingStatements = new List<string>();
            
            var sourceCodeWithAdjustments = fileSourceCode.Select(fileCode =>
            {
                var tree = CSharpSyntaxTree.ParseText(fileCode);
	
                var originalRoot = tree.GetRoot();
                var updatedRoot = originalRoot;
                
                //TODO: do same for structs / delegates / not nested enums?
                foreach (var klass in updatedRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList())
                {
                    var existingIdentifier = klass.ChildTokens().First(k => k.RawKind == (int)SyntaxKind.IdentifierToken);

                    var newIdentifier = SyntaxFactory.Identifier(existingIdentifier.Value + AssemblyChangesLoader.ClassnamePatchedPostfix + " ");
                    var updateClass = klass.ReplaceToken(existingIdentifier, newIdentifier);
	
                    updatedRoot = updatedRoot.ReplaceNode(klass, updateClass); //PERF: is updating in this manner performant?
                }

                //top level using nodes need to remain on top of the file, otherwise it'll result in compilation error and won't be properly parsed
                var topLevelUsingNodes = updatedRoot.DescendantNodes().OfType<UsingDirectiveSyntax>().Where(u => u.Parent is CompilationUnitSyntax).ToList();
                combinedUsingStatements.AddRange(topLevelUsingNodes.Select(n => n.ToFullString()));
                updatedRoot = updatedRoot.RemoveNodes(topLevelUsingNodes, SyntaxRemoveOptions.KeepEndOfLine);
	
                return updatedRoot.ToFullString();
                
                // var sourceCodeWithClassNamesAdjusted = Regex.Replace(fileCode, TypeNameRegexReplacementPattern, "$1$2${typeName}" + AssemblyChangesLoader.ClassnamePatchedPostfix + "$3");
                //
                // return Hack_EnsureNestedTypeNamesRemainUnchanged(fileCode, sourceCodeWithClassNamesAdjusted);
            }).ToList();

            var sourceCodeCombined = new StringBuilder();
            foreach (var usingStatement in combinedUsingStatements.Distinct())
            {
                sourceCodeCombined.Append(usingStatement);
            }

            foreach (var sourceCodeWithAdjustment in sourceCodeWithAdjustments)
            {
                sourceCodeCombined.AppendLine(sourceCodeWithAdjustment);
            }

#if QuickCodeIterationManager_DebugEnabled
            Debug.Log("Soruce Code Created:\r\n\r\n" + sourceCodeCombined);
#endif
            return sourceCodeCombined.ToString();
        }

        protected static List<string> ResolveReferencesToAdd(List<string> excludeAssyNames)
        {
            var referencesToAdd = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain
                         .GetAssemblies() //TODO: PERF: just need to load once and cache? or get assembly based on changed file only?
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

            return referencesToAdd;
        }

        protected static string Hack_EnsureNestedTypeNamesRemainUnchanged(string fileCode, string sourceCodeWithClassNamesAdjusted)
        {
            var matches = Regex.Matches(fileCode, TypeNameRegexReplacementPattern);
            var originalNamesOfAdjustedTypes = matches.Cast<Match>().Select(m => m.Groups["typeName"].Value).Distinct().ToList();
            foreach (var originalTypeName in originalNamesOfAdjustedTypes)
            {
                var matchingType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.Name == originalTypeName); //TODO: that's very weak, it's entirely possible to have same class name across different namespace, without proper source code parsing it's difficult to tell

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
    }
}