using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FastScriptReload.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace FastScriptReload.Editor
{
    [InitializeOnLoad]
    public class DynamicCompilationBase
    {
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
                var root = tree.GetRoot();
                var rewriter = new HotReloadCompliantRewriter();
                root = rewriter.Visit(root);
                combinedUsingStatements.AddRange(rewriter.StrippedUsingDirectives);

                return root.ToFullString();
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

        class HotReloadCompliantRewriter : CSharpSyntaxRewriter
        {
            public List<string> StrippedUsingDirectives = new List<string>();
            
            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                return AddPatchedPostfixToTopLevelDeclarations(node, node.Identifier);
                //if subclasses need to be adjusted, it's done via recursion.
                // foreach (var childNode in node.ChildNodes().OfType<ClassDeclarationSyntax>())
                // {
                //     var changed = Visit(childNode);
                //     node = node.ReplaceNode(childNode, changed);
                // }
            }
            
            public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
            {
                return AddPatchedPostfixToTopLevelDeclarations(node, node.Identifier);
            }

            public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
            {
                return AddPatchedPostfixToTopLevelDeclarations(node, node.Identifier);
            }

            public override SyntaxNode VisitDelegateDeclaration(DelegateDeclarationSyntax node)
            {
                return AddPatchedPostfixToTopLevelDeclarations(node, node.Identifier);
            }

            public override SyntaxNode VisitUsingDirective(UsingDirectiveSyntax node)
            {
                if (node.Parent is CompilationUnitSyntax)
                {
                    StrippedUsingDirectives.Add(node.ToFullString());
                    return null;
                }

                return base.VisitUsingDirective(node);
            }
            
            private static SyntaxNode AddPatchedPostfixToTopLevelDeclarations(CSharpSyntaxNode node, SyntaxToken identifier)
            {
                var newIdentifier = SyntaxFactory.Identifier(identifier + AssemblyChangesLoader.ClassnamePatchedPostfix + " ");
                node = node.ReplaceToken(identifier, newIdentifier);
                return node;
            }
        }
    }
}