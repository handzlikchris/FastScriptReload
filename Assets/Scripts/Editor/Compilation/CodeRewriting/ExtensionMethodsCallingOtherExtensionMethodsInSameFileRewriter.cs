using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    // When calling other extension methods from same file compilation would fail with 'The call is ambiguous between the following methods or properties' 
    class ExtensionMethodsCallingOtherExtensionMethodsInSameFileRewriter : FastScriptReloadCodeRewriterBase
    {
        private Dictionary<string, Dictionary<string, string>>  _typeNameToDeclaredMethodNameToThisArgName = new Dictionary<string, Dictionary<string, string>>();
        
        public ExtensionMethodsCallingOtherExtensionMethodsInSameFileRewriter(bool writeRewriteReasonAsComment)
            : base(writeRewriteReasonAsComment)
        {
	        
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var classDeclaredMethodNameToThisArgName = new Dictionary<string, string>();
            _typeNameToDeclaredMethodNameToThisArgName[node.Identifier.Text] = classDeclaredMethodNameToThisArgName;
            
            foreach (var methodDeclaration in node.Members
                         .Where(n => n.Kind() == SyntaxKind.MethodDeclaration)
                         .Select(n => (MethodDeclarationSyntax)n))
            {
                var parameters = methodDeclaration.ParameterList.Parameters;
                if (parameters.Count > 0 && parameters[0].Modifiers.Any(SyntaxKind.ThisKeyword))
                {
                    var thisArgumentName = parameters[0].Identifier.Text;
                    classDeclaredMethodNameToThisArgName.TryAdd(methodDeclaration.Identifier.Text, thisArgumentName);
                }
            }
            
            return base.VisitClassDeclaration(node);
        }
        
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Check if the invocation is an instance method with a member access (e.g., selfName.CallingOtherTest())
            if (node.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var methodName = memberAccess.Name.Identifier.Text;
                var instanceArgName = memberAccess.Expression;
                var className = node.Ancestors().OfType<ClassDeclarationSyntax>().First().Identifier.Text;
                if (_typeNameToDeclaredMethodNameToThisArgName.TryGetValue(className, out var declaredMethodNameToThisArgName)
                    && declaredMethodNameToThisArgName.TryGetValue(methodName, out var definedInstanceArgName))
                {
                    if (instanceArgName.ToString() == definedInstanceArgName)
                    {
                        // Rewrite the invocation to a static method call
                        var newInvocation = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(methodName))
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SeparatedList(
                                        node.ArgumentList.Arguments.Insert(0, SyntaxFactory.Argument(instanceArgName)) // Add instance as first argument
                                    )
                                )
                            ).NormalizeWhitespace();
                        
                        return AddRewriteCommentIfNeeded(
                            newInvocation, 
                            $"{nameof(ExtensionMethodsCallingOtherExtensionMethodsInSameFileRewriter)}:Replaced extension method call with static call"
                        );
                    }
                }
            }

            return base.VisitInvocationExpression(node);
        }
    }
}