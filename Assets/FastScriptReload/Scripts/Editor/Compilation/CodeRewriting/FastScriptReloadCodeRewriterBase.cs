using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    abstract class FastScriptReloadCodeRewriterBase : CSharpSyntaxRewriter
    {
        protected readonly bool _writeRewriteReasonAsComment;

        protected FastScriptReloadCodeRewriterBase(bool writeRewriteReasonAsComment, bool visitIntoStructuredTrivia = false) : base(visitIntoStructuredTrivia)
        {
            _writeRewriteReasonAsComment = writeRewriteReasonAsComment;
        }
        
        protected SyntaxToken AddRewriteCommentIfNeeded(SyntaxToken syntaxToken, string commentText)
        {
            if (_writeRewriteReasonAsComment)
            {
                return syntaxToken.WithTrailingTrivia(syntaxToken.TrailingTrivia.Add(SyntaxFactory.Comment($"/*FSR:{commentText}*/")));
            }

            return syntaxToken;
        }
        
        protected T AddRewriteCommentIfNeeded<T>(T syntaxNode, string commentText)
            where T: SyntaxNode
        {
            if (_writeRewriteReasonAsComment)
            {
                return syntaxNode.WithTrailingTrivia(syntaxNode.GetTrailingTrivia().Add(SyntaxFactory.Comment($"/*FSR:{commentText}*/")));
            }

            return syntaxNode;
        }
    }
}