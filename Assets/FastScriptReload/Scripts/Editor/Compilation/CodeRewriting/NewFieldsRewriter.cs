using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FastScriptReload.Runtime;
using FastScriptReload.Scripts.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
	class NewFieldsRewriter : CSharpSyntaxRewriter
	{
		private readonly Dictionary<string, List<string>> _typeToNewFieldDeclarations;

		public NewFieldsRewriter(Dictionary<string, List<string>> typeToNewFieldDeclarations) {
			_typeToNewFieldDeclarations = typeToNewFieldDeclarations;
		}
		
		public static List<MemberInfo> GetReplaceableMembers(Type type) { //TODO: later other might need to be included? props?
			return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Cast<MemberInfo>().ToList();
		}

		public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
	    {
	        var classNode = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
			if(classNode != null) {
				var fullClassName = RoslynUtils.GetMemberFQDN(classNode, classNode.Identifier.ToString());
	            if(!string.IsNullOrEmpty(fullClassName)) {
	                var fieldName = node.Identifier.ToString();
	                var allNewFieldNamesForClass = _typeToNewFieldDeclarations[fullClassName];
	                if(allNewFieldNamesForClass.Contains(fieldName)) {
		                return 
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
								.WithTriviaFrom(node);
	                }
				}
			}

			return base.VisitIdentifierName(node);
		}

		public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
		{
			var fieldName = node.Declaration.Variables.First().Identifier.ToString();
			var classNode = node.Ancestors().OfType<ClassDeclarationSyntax>().First();
			var fullClassName = RoslynUtils.GetMemberFQDNWithoutMemberName(node);
			
			if(_typeToNewFieldDeclarations.TryGetValue(fullClassName, out var newFields)) {
				if(newFields.Contains(fieldName)) {
					var existingLeading = node.GetLeadingTrivia();			
					var existingTrailing = node.GetTrailingTrivia();
								
					return node
						.WithLeadingTrivia(existingLeading.Add(SyntaxFactory.Comment("/* ")))
						.WithTrailingTrivia(existingTrailing.Insert(0, SyntaxFactory.Comment(" */ //Auto-excluded to prevent exceptions - see docs")));
				}
			}

			return base.VisitFieldDeclaration(node);
		}
	}
}