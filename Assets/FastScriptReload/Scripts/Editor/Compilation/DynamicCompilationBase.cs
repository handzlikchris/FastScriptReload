using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FastScriptReload.Editor.NewFields;
using FastScriptReload.Runtime;
using ImmersiveVRTools.Editor.Common.Cache;
using ImmersiveVRTools.Runtime.Common.Utilities;
using ImmersiveVrToolsCommon.Runtime.Logging;
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

                if (FastScriptReloadManager.Instance.EnableExperimentalNewFieldsAccess)
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

		abstract class ThisRewriterBase : CSharpSyntaxRewriter
		{
			protected static SyntaxNode CreateCastedThisExpression(ThisExpressionSyntax node)
			{
				var ancestors = node.Ancestors().Where(n => n is TypeDeclarationSyntax).Cast<TypeDeclarationSyntax>().ToList();
				if (ancestors.Count() > 1)
				{
					Debug.LogWarning($"ThisRewriter: for class: '{ancestors.First().Identifier}' - 'this' call/assignment in nested class / struct. Dynamic cast will be used but this could cause issues in some cases:" +
					               $"\r\n\r\n1) - If called method has multiple overrides, using dynamic will cause compiler issue as it'll no longer be able to pick correct one" +
					               $"\r\n\r\n If you see any issues with that message, please look at 'Limitation' section in documentation as this outlines how to deal with it.");

					//TODO: casting to dynamic seems to be best option (and one that doesn't fail for nested classes), what's the performance overhead?
					return SyntaxFactory.CastExpression(
						SyntaxFactory.ParseTypeName("dynamic"),
						node
					);
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
		}

		class ThisCallRewriter : ThisRewriterBase
		{
			public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
			{
				if (node.Parent is ArgumentSyntax)
				{
					return CreateCastedThisExpression(node);
				}
				return base.VisitThisExpression(node);
			}
		}
		
		class ThisAssignmentRewriter: ThisRewriterBase {
			public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
			{
				if (node.Parent is AssignmentExpressionSyntax) {
					return CreateCastedThisExpression(node);
				}

				return base.VisitThisExpression(node);
			}
		}
		
		class FieldsWalker : CSharpSyntaxWalker {
			private Dictionary<string, List<string>> _typeNameToFieldDeclarations = new Dictionary<string, List<string>>(); 
			
			public override void VisitIdentifierName(IdentifierNameSyntax node)
			{
				base.VisitIdentifierName(node);
			}

			public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
			{
				var className = node.Ancestors().OfType<ClassDeclarationSyntax>().First().Identifier.ToString();
				var fieldName = node.Declaration.Variables.First().Identifier.ToString();
				
				if(!_typeNameToFieldDeclarations.ContainsKey(className)) {
					_typeNameToFieldDeclarations[className] = new List<string>();
				}
				_typeNameToFieldDeclarations[className].Add(fieldName);
				
				base.VisitFieldDeclaration(node);
			}
			
			public Dictionary<string, List<string>> GetTypeToFieldDeclarations() {
				return _typeNameToFieldDeclarations;
			}
		}

		class NewFieldsRewriter : CSharpSyntaxRewriter
		{
			private Dictionary<string, List<string>> _typeToFieldDeclarations; 
			private List<Type> _existingTypes;
			
			public NewFieldsRewriter(Dictionary<string, List<string>> typeToFieldDeclarations, List<Type> existingTypes) {
				_typeToFieldDeclarations = typeToFieldDeclarations;
				_existingTypes = existingTypes;
			}

			public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
			{
				var className = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ToString();
				if(!string.IsNullOrEmpty(className)) {
					var fieldName = node.Identifier.ToString();
					var allNewFieldNamesForClass = _typeToFieldDeclarations[className];
					if(allNewFieldNamesForClass.Contains(fieldName)) {
						var existingType = _existingTypes.FirstOrDefault(t => t.Name == className.Replace("__Patched_", ""));
						if (existingType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null)
						{
							return 
								SyntaxFactory.MemberAccessExpression(
									SyntaxKind.SimpleMemberAccessExpression,
									SyntaxFactory.InvocationExpression(
										SyntaxFactory.MemberAccessExpression(
											SyntaxKind.SimpleMemberAccessExpression,
											//TODO: resolve with nameof
											SyntaxFactory.IdentifierName("FastScriptReload.Editor.NewFields.TemporaryNewFieldValues"),
											SyntaxFactory.GenericName(
												SyntaxFactory.Identifier(nameof(TemporaryNewFieldValues.ResolvePatchedObject)))
											.WithTypeArgumentList(
												SyntaxFactory.TypeArgumentList(
													SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
														SyntaxFactory.IdentifierName(className + AssemblyChangesLoader.ClassnamePatchedPostfix))))))
									.WithArgumentList(
										SyntaxFactory.ArgumentList(
											SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
												SyntaxFactory.Argument(
													SyntaxFactory.ThisExpression())))),
									SyntaxFactory.IdentifierName(fieldName))
									.WithTriviaFrom(node);
						}
					}
				}
				
				return base.VisitIdentifierName(node);
			}
		};
    }
}