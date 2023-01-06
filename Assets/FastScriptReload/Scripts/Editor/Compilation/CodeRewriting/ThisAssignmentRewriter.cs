using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    class ThisAssignmentRewriter: ThisRewriterBase {
        public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
        {
            if (node.Parent is AssignmentExpressionSyntax) {
                return CreateCastedThisExpression(node);
            }

            return base.VisitThisExpression(node);
        }
    }
}