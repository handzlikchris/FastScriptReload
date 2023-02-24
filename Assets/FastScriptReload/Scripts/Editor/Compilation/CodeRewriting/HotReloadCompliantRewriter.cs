using System.Collections.Generic;
using FastScriptReload.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    class HotReloadCompliantRewriter : FastScriptReloadCodeRewriterBase
    {
        public List<string> StrippedUsingDirectives = new List<string>();
        
        public HotReloadCompliantRewriter(bool writeRewriteReasonAsComment, bool visitIntoStructuredTrivia = false) 
            : base(writeRewriteReasonAsComment, visitIntoStructuredTrivia)
        {
        }

        
        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            return AddPatchedPostfixToTopLevelDeclarations(node, node.Identifier);
            //if subclasses need to be adjusted, it's done via recursion.
            // foreach (var childNode in node.ChildNodes().OfType<ClassDeclarationSyntax>())
            // {
            //     var changed = Visit(childNode);
            //     node = node.ReplaceNode(childNode, changed);
            // }
        }

        public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
        {
            return AddPatchedPostfixToTopLevelDeclarations(node, node.Identifier);
        }

        public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            return AddPatchedPostfixToTopLevelDeclarations(node, node.Identifier);
        }

        public override SyntaxNode VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            return AddPatchedPostfixToTopLevelDeclarations(node, node.Identifier);
        }

        public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            return AddPatchedPostfixToTopLevelDeclarations(node, node.Identifier);
        }

        public override SyntaxNode VisitUsingDirective(UsingDirectiveSyntax node)
        {
            if (node.Parent is CompilationUnitSyntax)
            {
                StrippedUsingDirectives.Add(node.ToFullString());
                return null;
            }

            return base.VisitUsingDirective(node);
        }

        private SyntaxNode AddPatchedPostfixToTopLevelDeclarations(CSharpSyntaxNode node, SyntaxToken identifier)
        {
            var newIdentifier = SyntaxFactory.Identifier(identifier + AssemblyChangesLoader.ClassnamePatchedPostfix);
            newIdentifier = AddRewriteCommentIfNeeded(newIdentifier, $"{nameof(HotReloadCompliantRewriter)}:{nameof(AddPatchedPostfixToTopLevelDeclarations)}");
            node = node.ReplaceToken(identifier, newIdentifier);
            return node;
        }
    }
}