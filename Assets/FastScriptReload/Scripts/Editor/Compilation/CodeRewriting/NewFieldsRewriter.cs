using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FastScriptReload.Runtime;
using FastScriptReload.Scripts.Runtime;
using ImmersiveVrToolsCommon.Runtime.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
	class NewFieldsRewriter : FastScriptReloadCodeRewriterBase
	{
		private readonly Dictionary<string, List<string>> _typeToNewFieldDeclarations;

		public NewFieldsRewriter(Dictionary<string, List<string>> typeToNewFieldDeclarations, bool writeRewriteReasonAsComment) 
			:base(writeRewriteReasonAsComment)
		{
			_typeToNewFieldDeclarations = typeToNewFieldDeclarations;
		}

		public static List<MemberInfo> GetReplaceableMembers(Type type)
		{ //TODO: later other might need to be included? props?
			return type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Cast<MemberInfo>().ToList();
		}


		public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
		{
			if (node.Expression.ToString() == "nameof")
			{
				var classNode = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
				if (classNode != null)
				{
					var fullClassName = RoslynUtils.GetMemberFQDN(classNode, classNode.Identifier.ToString());
					if (!string.IsNullOrEmpty(fullClassName))
					{
						var nameofExpressionParts = node.ArgumentList.Arguments.First().ToFullString().Split('.'); //nameof could have multiple . like NewFieldCustomClass.FieldInThatClass
						var fieldName = nameofExpressionParts.First();  // should take first part only to determine if new field eg. 'NewFieldCustomClass'
						if (_typeToNewFieldDeclarations.TryGetValue(fullClassName, out var allNewFieldNamesForClass))
						{
							if (allNewFieldNamesForClass.Contains(fieldName))
							{
								return AddRewriteCommentIfNeeded(
									SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(nameofExpressionParts.Last())), // should take last part only to for actual string eg. 'FieldInThatClass'
									$"{nameof(NewFieldsRewriter)}:{nameof(VisitInvocationExpression)}");
							}
							
						}
					}
				}
			}

			return base.VisitInvocationExpression(node);
		}

		public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
		{
			var classNode = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
			if (classNode != null)
			{
				var fullClassName = RoslynUtils.GetMemberFQDN(classNode, classNode.Identifier.ToString());
				if (!string.IsNullOrEmpty(fullClassName))
				{
					var fieldName = node.Identifier.ToString();
					if (_typeToNewFieldDeclarations.TryGetValue(fullClassName, out var allNewFieldNamesForClass))
					{
						if (allNewFieldNamesForClass.Contains(fieldName))
						{
							var isNameOfExpression = node.Ancestors().OfType<InvocationExpressionSyntax>().Any(e => e.Expression.ToString() == "nameof");
							if (!isNameOfExpression) //nameof expression will be rewritten via VisitInvocationExpression
							{
								return
								AddRewriteCommentIfNeeded(
									SyntaxFactory.MemberAccessExpression(
											SyntaxKind.SimpleMemberAccessExpression,
											SyntaxFactory.InvocationExpression(
													SyntaxFactory.MemberAccessExpression(
														SyntaxKind.SimpleMemberAccessExpression,
														SyntaxFactory.IdentifierName(typeof(TemporaryNewFieldValues).FullName),
														SyntaxFactory.GenericName(
																SyntaxFactory.Identifier(nameof(TemporaryNewFieldValues.ResolvePatchedObject)))
															.WithTypeArgumentList(
																SyntaxFactory.TypeArgumentList(
																	SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
																		SyntaxFactory.IdentifierName(fullClassName + AssemblyChangesLoader.ClassnamePatchedPostfix))))))
												.WithArgumentList(
													SyntaxFactory.ArgumentList(
														SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
															SyntaxFactory.Argument(
																SyntaxFactory.ThisExpression())))),
											SyntaxFactory.IdentifierName(fieldName))
										.WithTriviaFrom(node),
									$"{nameof(NewFieldsRewriter)}:{nameof(VisitIdentifierName)}"
								);
							}
						}
					}
					else
					{
						LoggerScoped.LogWarning($"Unable to find type: {fullClassName}");
					}
				}
			}

			return base.VisitIdentifierName(node);
		}

		public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
		{
			var fieldName = node.Declaration.Variables.First().Identifier.ToString();
			var fullClassName = RoslynUtils.GetMemberFQDNWithoutMemberName(node);

			if (_typeToNewFieldDeclarations.TryGetValue(fullClassName, out var newFields))
			{
				if (newFields.Contains(fieldName))
				{
					var existingLeading = node.GetLeadingTrivia();
					var existingTrailing = node.GetTrailingTrivia();

					return AddRewriteCommentIfNeeded(
						node
							.WithLeadingTrivia(existingLeading.Add(SyntaxFactory.Comment("/* ")))
							.WithTrailingTrivia(existingTrailing.Insert(0, SyntaxFactory.Comment(" */ //Auto-excluded to prevent exceptions - see docs"))),
						$"{nameof(NewFieldsRewriter)}:{nameof(VisitFieldDeclaration)}"
					);
				}
			}

			return base.VisitFieldDeclaration(node);
		}
	}
}