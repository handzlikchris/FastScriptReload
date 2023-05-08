using System;
using System.Collections.Generic;
using System.Linq;
using FastScriptReload.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    class AllPatchedIdentifiersRewriter : FastScriptReloadCodeRewriterBase
    {
        private readonly HashSet<string> _originalIdentifiersRenamedToContainPatchedPostfix;

        private readonly static HashSet<Type> _doNotRenameIfParentOfType = new HashSet<Type>
        {
            typeof(ExpressionSyntax)
        };

        public AllPatchedIdentifiersRewriter(bool writeRewriteReasonAsComment, List<string> originalIdentifiersRenamedToContainPatchedPostfix, bool visitIntoStructuredTrivia = false) 
            : base(writeRewriteReasonAsComment, visitIntoStructuredTrivia)
        {
            _originalIdentifiersRenamedToContainPatchedPostfix =  new HashSet<string>(originalIdentifiersRenamedToContainPatchedPostfix);
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            Type parentType;
            if (_originalIdentifiersRenamedToContainPatchedPostfix.Contains(node.Identifier.ValueText)
                && (parentType = node.Parent?.GetType()) != null 
                &&  _doNotRenameIfParentOfType.All(t => parentType.IsAssignableFrom(t)))
            {
                var newIdentifier = SyntaxFactory.Identifier(node.Identifier.ValueText + AssemblyChangesLoader.ClassnamePatchedPostfix + " ");
                AddRewriteCommentIfNeeded(newIdentifier, $"{nameof(AllPatchedIdentifiersRewriter)}");
                node = node.ReplaceToken(node.Identifier, newIdentifier);
                return node;            
            }
            
            return base.VisitIdentifierName(node);
        }
    }
}