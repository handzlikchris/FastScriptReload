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

namespace FastScriptReload.Editor.Compilation
{
    [InitializeOnLoad]
    public class DynamicCompilationBase
    {
	    public static bool LogHowToFixMessageOnCompilationError;
	    
        protected static readonly string[] ActiveScriptCompilationDefines;
        protected static readonly string DynamicallyCreatedAssemblyAttributeSourceCode = $"[assembly: {typeof(DynamicallyCreatedAssemblyAttribute).FullName}()]";

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

                //WARN: application order is important, eg ctors need to happen before class names as otherwise ctors will not be recognised as ctors
                if (FastScriptReloadManager.Instance.EnableExperimentalThisCallLimitationFix)
                {
					root = new ThisCallRewriter().Visit(root);
                }
                root = new ConstructorRewriter( adjustCtorOnlyForNonNestedTypes: true).Visit(root);
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

#if FastScriptReload_DebugEnabled
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

        class ConstructorRewriter : CSharpSyntaxRewriter
        {
	        private readonly bool _adjustCtorOnlyForNonNestedTypes;
	        
	        public ConstructorRewriter(bool adjustCtorOnlyForNonNestedTypes)
	        {
		        _adjustCtorOnlyForNonNestedTypes = adjustCtorOnlyForNonNestedTypes;
	        }
	        
	        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
	        {
		        if (_adjustCtorOnlyForNonNestedTypes)
		        {
			        var typeNestedLevel = node.Ancestors().Count(a => a is TypeDeclarationSyntax);
			        if (typeNestedLevel == 1)
			        {
				        return AdjustCtorNameForTypeAdjustment(node);
			        }
		        }
		        else
		        {
			        return AdjustCtorNameForTypeAdjustment(node);
		        }

		        return base.VisitConstructorDeclaration(node);
	        }

	        private static SyntaxNode AdjustCtorNameForTypeAdjustment(ConstructorDeclarationSyntax node)
	        {
		        var typeName = (node.Ancestors().First(n => n is TypeDeclarationSyntax) as TypeDeclarationSyntax).Identifier
			        .ToString();
		        if (!typeName.EndsWith(AssemblyChangesLoader.ClassnamePatchedPostfix))
		        {
			        typeName += AssemblyChangesLoader.ClassnamePatchedPostfix;
		        }

		        return node.ReplaceToken(node.Identifier, SyntaxFactory.Identifier(typeName));
	        }
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

			public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
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
				var newIdentifier = SyntaxFactory.Identifier(identifier + AssemblyChangesLoader.ClassnamePatchedPostfix);
				node = node.ReplaceToken(identifier, newIdentifier);
				return node;
			}
		}

		class ThisCallRewriter : CSharpSyntaxRewriter
		{
			public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
			{
				if (node.Parent is ArgumentSyntax)
				{
					var ancestors = node.Ancestors().Where(n => n is TypeDeclarationSyntax).Cast<TypeDeclarationSyntax>().ToList();
					if (ancestors.Count() > 1)
					{
						Debug.LogError($"{ancestors.First().Identifier} - 'this' call is in a nested class / struct - currently that's not supported and will cause compilation error. You can move type out of class / struct in order for this call to be correctly Hot-Reloaded.");
					}
					
					var methodInType = ancestors.First().Identifier.ToString();
					return SyntaxFactory.CastExpression(
						SyntaxFactory.ParseTypeName(methodInType),
						SyntaxFactory.CastExpression(
							SyntaxFactory.ParseTypeName(typeof(object).FullName),
							node
						)
					);
				}
				return base.VisitThisExpression(node);
			}
		}
    }
}