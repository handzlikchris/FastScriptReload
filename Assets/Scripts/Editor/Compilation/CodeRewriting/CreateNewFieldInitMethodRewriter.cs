using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImmersiveVrToolsCommon.Runtime.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
	class CreateNewFieldInitMethodRewriter: FastScriptReloadCodeRewriterBase {
		private readonly Dictionary<string, List<string>> _typeToNewFieldDeclarations;
		private static readonly string NewFieldsToCreateValueFnDictionaryFieldName = "__Patched_NewFieldNameToInitialValueFn";
		private static readonly string NewFieldsToGetTypeFnDictionaryFieldName = "__Patched_NewFieldsToGetTypeFnDictionaryFieldName";
		private static readonly string DictionaryFullNamespaceTypeName = "global::System.Collections.Generic.Dictionary";

		public static Dictionary<string, Func<object>> ResolveNewFieldsToCreateValueFn(Type forType)
		{
			return (Dictionary<string, Func<object>>) forType.GetField(NewFieldsToCreateValueFnDictionaryFieldName, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
		}
		
		public static Dictionary<string, Func<object>> ResolveNewFieldsToTypeFn(Type forType)
		{
			return (Dictionary<string, Func<object>>) forType.GetField(NewFieldsToGetTypeFnDictionaryFieldName, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
		}

		public CreateNewFieldInitMethodRewriter(Dictionary<string, List<string>> typeToNewFieldDeclarations, bool writeRewriteReasonAsComment)
			:base(writeRewriteReasonAsComment)
		{
			_typeToNewFieldDeclarations = typeToNewFieldDeclarations;
		}

		public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
		{
			var fullClassName = RoslynUtils.GetMemberFQDN(node, node.Identifier.ToString());
			if (!_typeToNewFieldDeclarations.TryGetValue(fullClassName, out var newClassFields))
			{
				LoggerScoped.LogWarning($"Unable to find new-fields for type: {fullClassName}, this is not an issue if there are no new fields for that type.");
			}

			Func<FieldDeclarationSyntax, ExpressionSyntax> getObjectFnSyntax = fieldDeclarationNode => fieldDeclarationNode.Declaration.Variables[0].Initializer?.Value //value captured from initializer
			                                            ?? SyntaxFactory.DefaultExpression(SyntaxFactory.IdentifierName(fieldDeclarationNode.Declaration.Type.ToString()));
			var withDictionaryFieldNameToInitFieldValue = CreateNewFieldNameToGetObjectFnDictionary(node, newClassFields, getObjectFnSyntax, NewFieldsToCreateValueFnDictionaryFieldName);

			//TODO: slightly odd scenario 'When explicit #nullable enable is used, reference types should be rewritten to typeof(type) for initialization and value types should remain typeof(type?)'
			// Func<FieldDeclarationSyntax, ExpressionSyntax> getObjectTypeFnSyntax = fieldDeclarationNode => SyntaxFactory.TypeOfExpression(
			// 	SyntaxFactory.ParseTypeName(fieldDeclarationNode.Declaration.Type.ToFullString().Replace("?", ""))
			// );
			Func<FieldDeclarationSyntax, ExpressionSyntax> getObjectTypeFnSyntax = fieldDeclarationNode => SyntaxFactory.TypeOfExpression(fieldDeclarationNode.Declaration.Type);
			return CreateNewFieldNameToGetObjectFnDictionary(withDictionaryFieldNameToInitFieldValue, newClassFields, getObjectTypeFnSyntax, NewFieldsToGetTypeFnDictionaryFieldName);
		}

		private ClassDeclarationSyntax CreateNewFieldNameToGetObjectFnDictionary(ClassDeclarationSyntax node,
			List<string> newClassFields, Func<FieldDeclarationSyntax, ExpressionSyntax> getObjectFnSyntax, string dictionaryFieldName)
		{
			var dictionaryKeyToInitValueNodes = newClassFields.SelectMany(fieldName =>
			{
				var fieldDeclarationNode = node.ChildNodes().OfType<FieldDeclarationSyntax>()
					.Single(f => f.Declaration.Variables.First().Identifier.ToString() == fieldName);

				return new[]
				{
					(SyntaxNodeOrToken)SyntaxFactory.AssignmentExpression(
						SyntaxKind.SimpleAssignmentExpression,
						SyntaxFactory.ImplicitElementAccess()
							.WithArgumentList(
								SyntaxFactory.BracketedArgumentList(
									SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
										SyntaxFactory.Argument(
											SyntaxFactory.LiteralExpression(
												SyntaxKind.StringLiteralExpression,
												SyntaxFactory.Literal(fieldDeclarationNode.Declaration.Variables.First()
													.Identifier.ToString())))))), //variable name
						SyntaxFactory.ParenthesizedLambdaExpression(getObjectFnSyntax(fieldDeclarationNode))),
					SyntaxFactory.Token(SyntaxKind.CommaToken) //comma, add for all
				};
			}).ToArray();


			var dictionaryInitializer =
				SyntaxFactory.InitializerExpression(
					SyntaxKind.ObjectInitializerExpression,
					SyntaxFactory.SeparatedList<ExpressionSyntax>(
						dictionaryKeyToInitValueNodes.ToArray()
					));

			var withDictionaryFieldNameToInitFieldValue = node.AddMembers(
				SyntaxFactory.FieldDeclaration(
						SyntaxFactory.VariableDeclaration(
								SyntaxFactory.GenericName(
										SyntaxFactory.Identifier(DictionaryFullNamespaceTypeName))
									.WithTypeArgumentList(
										SyntaxFactory.TypeArgumentList(
											SyntaxFactory.SeparatedList<TypeSyntax>(
												new SyntaxNodeOrToken[]
												{
													SyntaxFactory.PredefinedType(
														SyntaxFactory.Token(SyntaxKind.StringKeyword)),
													SyntaxFactory.Token(SyntaxKind.CommaToken),
													SyntaxFactory.GenericName(
															SyntaxFactory.Identifier("global::System.Func"))
														.WithTypeArgumentList(
															SyntaxFactory.TypeArgumentList(
																SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
																	SyntaxFactory.PredefinedType(
																		SyntaxFactory.Token(SyntaxKind.ObjectKeyword)))))
												}))))
							.WithVariables(
								SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
									SyntaxFactory.VariableDeclarator(
											SyntaxFactory.Identifier(dictionaryFieldName))
										.WithInitializer(
											SyntaxFactory.EqualsValueClause(
												SyntaxFactory.ObjectCreationExpression(
														SyntaxFactory.GenericName(
																SyntaxFactory.Identifier(DictionaryFullNamespaceTypeName))
															.WithTypeArgumentList(
																SyntaxFactory.TypeArgumentList(
																	SyntaxFactory.SeparatedList<TypeSyntax>(
																		new SyntaxNodeOrToken[]
																		{
																			SyntaxFactory.PredefinedType(
																				SyntaxFactory.Token(SyntaxKind.StringKeyword)),
																			SyntaxFactory.Token(SyntaxKind.CommaToken),
																			SyntaxFactory.QualifiedName(
																				SyntaxFactory.IdentifierName("global::System"),
																				SyntaxFactory.GenericName(
																						SyntaxFactory.Identifier("Func"))
																					.WithTypeArgumentList(
																						SyntaxFactory.TypeArgumentList(
																							SyntaxFactory
																								.SingletonSeparatedList<
																									TypeSyntax>(
																									SyntaxFactory
																										.PredefinedType(
																											SyntaxFactory.Token(
																												SyntaxKind
																													.ObjectKeyword))))))
																		}))))
													.WithInitializer(dictionaryInitializer))))))
					.WithModifiers(
						SyntaxFactory.TokenList(
							SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
							SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
					.NormalizeWhitespace()
					.WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed))
					.WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed, SyntaxFactory.ElasticCarriageReturnLineFeed))
			);
			return AddRewriteCommentIfNeeded(withDictionaryFieldNameToInitFieldValue, $"{nameof(CreateNewFieldInitMethodRewriter)}", true);
		}
	}
}