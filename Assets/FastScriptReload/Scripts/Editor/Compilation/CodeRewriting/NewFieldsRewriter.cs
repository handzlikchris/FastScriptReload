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
                                                SyntaxFactory.IdentifierName(typeof(TemporaryNewFieldValues).FullName),
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