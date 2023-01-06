using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
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
}