using FastScriptReload.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
	class RecordeRewriter : FastScriptReloadCodeRewriterBase
    {
        public RecordeRewriter(bool writeRewriteReasonAsComment)
	        : base(writeRewriteReasonAsComment)
        {
	        
        }
        
        public override SyntaxNode VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
	        return AdjustRecordName(node, node.Identifier);
        }

        private SyntaxNode AdjustRecordName(RecordDeclarationSyntax node, SyntaxToken nodeIdentifier)
        {
	        var typeName = nodeIdentifier.ToString(); //Not ToFullString() as it may include spaces and break.
	        
	        if (!typeName.EndsWith(AssemblyChangesLoader.ClassnamePatchedPostfix))
	        {
		        typeName += AssemblyChangesLoader.ClassnamePatchedPostfix;
	        }

	        return AddRewriteCommentIfNeeded(
		        node.ReplaceToken(nodeIdentifier, SyntaxFactory.Identifier(typeName)), 
		        $"{nameof(RecordeRewriter)}:{nameof(AdjustRecordName)}"
		    );
        }
    }
}