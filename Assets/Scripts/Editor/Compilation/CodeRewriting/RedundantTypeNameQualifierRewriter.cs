using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    //ideally we'd use Simplifier but FSR doesn't have access to semantic model on which simplifier works
    
    //Removes redundant type name qualifiers, eg: RootClass.NestedEnum.Value -> NestedEnum.Value, root class component is rewritten to have
    //__Patched_ postfix and if original remains it'll cause type mismatch
    class RedundantTypeNameQualifierRewriter : FastScriptReloadCodeRewriterBase
    {
        public RedundantTypeNameQualifierRewriter(bool writeRewriteReasonAsComment, bool visitIntoStructuredTrivia = false) 
            : base(writeRewriteReasonAsComment, visitIntoStructuredTrivia)
        {
        }

        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if(node.Parent is MemberAccessExpressionSyntax)
                return base.VisitMemberAccessExpression(node); //only target outermost member access for full rewrite
            
            var firstNode = node.DescendantNodes().OfType<IdentifierNameSyntax>().FirstOrDefault();
            if(firstNode is IdentifierNameSyntax idNode && node.DescendantNodes().Count() > 1)
            {
                //enclosing type name same as first id node
                if((idNode.Ancestors().First(n => n is TypeDeclarationSyntax) as TypeDeclarationSyntax)?.Identifier.ToString() == idNode.Identifier.ToString())
                {
                    return AddRewriteCommentIfNeeded(
                        SyntaxFactory.ParseExpression(node.ToString().Replace($"{firstNode.Identifier.ToString()}.", string.Empty)),
                        $"{nameof(RedundantTypeNameQualifierRewriter)}"
                    );
                }
            }

            
            return base.VisitMemberAccessExpression(node);
        }
    }
}