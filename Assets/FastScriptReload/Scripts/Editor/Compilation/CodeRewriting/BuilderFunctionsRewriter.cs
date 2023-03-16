using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    class BuilderFunctionsRewriter : FastScriptReloadCodeRewriterBase
    {
        public BuilderFunctionsRewriter(bool writeRewriteReasonAsComment, bool visitIntoStructuredTrivia = false) 
            : base(writeRewriteReasonAsComment, visitIntoStructuredTrivia)
        {
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var ancestorName = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
            var ancestorNameWithoutPatchedPostfix = ancestorName.Replace(FastScriptReload.Runtime.AssemblyChangesLoader.ClassnamePatchedPostfix, "");

            if (node.ReturnType is IdentifierNameSyntax name && name.Identifier.ValueText == ancestorNameWithoutPatchedPostfix)
            {
                return node.WithReturnType(SyntaxFactory.IdentifierName(ancestorName + " "));
            }
            
            return base.VisitMethodDeclaration(node);
        }
    }
}