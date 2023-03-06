using System.Linq;
using ImmersiveVrToolsCommon.Runtime.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    abstract class ThisRewriterBase : FastScriptReloadCodeRewriterBase
    {
        protected ThisRewriterBase(bool writeRewriteReasonAsComment, bool visitIntoStructuredTrivia = false) 
            : base(writeRewriteReasonAsComment, visitIntoStructuredTrivia)
        {
        }
        
        protected SyntaxNode CreateCastedThisExpression(ThisExpressionSyntax node)
        {
            var ancestors = node.Ancestors().Where(n => n is TypeDeclarationSyntax).Cast<TypeDeclarationSyntax>().ToList();
            if (ancestors.Count() > 1)
            {
                LoggerScoped.LogWarning($"ThisRewriter: for class: '{ancestors.First().Identifier}' - 'this' call/assignment in nested class / struct. Dynamic cast will be used but this could cause issues in some cases:" +
                                 $"\r\n\r\n1) - If called method has multiple overrides, using dynamic will cause compiler issue as it'll no longer be able to pick correct one" +
                                 $"\r\n\r\n If you see any issues with that message, please look at 'Limitation' section in documentation as this outlines how to deal with it.");

                //TODO: casting to dynamic seems to be best option (and one that doesn't fail for nested classes), what's the performance overhead?
                return SyntaxFactory.CastExpression(
                    SyntaxFactory.ParseTypeName("dynamic"),
                    node
                );
            }
            
            var firstAncestor = ancestors.FirstOrDefault();
            if (firstAncestor == null)
            {
                LoggerScoped.LogWarning($"Unable to find first ancestor for node: {node.ToFullString()}, this rewrite will not be applied");
                return node;
            }

            var methodInType = firstAncestor.Identifier.ToString();
            var resultNode = SyntaxFactory.CastExpression(
                SyntaxFactory.ParseTypeName(methodInType),
                SyntaxFactory.CastExpression(
                    SyntaxFactory.ParseTypeName(typeof(object).FullName),
                    node
                )
            );

            return AddRewriteCommentIfNeeded(resultNode, $"{nameof(ThisRewriterBase)}:{nameof(CreateCastedThisExpression)}");
        }
    }
}