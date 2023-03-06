using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    public abstract class FastScriptReloadCodeRewriterBase : CSharpSyntaxRewriter
    {
        protected readonly bool _writeRewriteReasonAsComment;

        protected FastScriptReloadCodeRewriterBase(bool writeRewriteReasonAsComment, bool visitIntoStructuredTrivia = false) : base(visitIntoStructuredTrivia)
        {
            _writeRewriteReasonAsComment = writeRewriteReasonAsComment;
        }
        
        protected SyntaxToken AddRewriteCommentIfNeeded(SyntaxToken syntaxToken, string commentText, bool append = false)
        {
            if (_writeRewriteReasonAsComment)
            {
                if (append)
                {
                    return syntaxToken.WithLeadingTrivia(syntaxToken.LeadingTrivia.Add(SyntaxFactory.Comment($"/*FSR:{commentText}*/")));
                }
                else
                {
                    return syntaxToken.WithTrailingTrivia(syntaxToken.TrailingTrivia.Add(SyntaxFactory.Comment($"/*FSR:{commentText}*/")));
                }
            }

            return syntaxToken;
        }

        protected T AddRewriteCommentIfNeeded<T>(T syntaxNode, string commentText, bool append = false)
            where T : SyntaxNode
        {
            if (_writeRewriteReasonAsComment)
            {
                if(append) {
                    return syntaxNode.WithLeadingTrivia(syntaxNode.GetLeadingTrivia().Add(SyntaxFactory.Comment($"/*FSR:{commentText}*/")));
                }
                else {
                    return syntaxNode.WithTrailingTrivia(syntaxNode.GetTrailingTrivia().Add(SyntaxFactory.Comment($"/*FSR:{commentText}*/")));
                }

            }

            return syntaxNode;
        }
    }
}