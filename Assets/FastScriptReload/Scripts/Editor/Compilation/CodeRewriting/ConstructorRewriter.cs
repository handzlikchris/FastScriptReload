using System.Linq;
using FastScriptReload.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
        class ConstructorRewriter : FastScriptReloadCodeRewriterBase
        {
	        private readonly bool _adjustCtorOnlyForNonNestedTypes;
	        
	        public ConstructorRewriter(bool adjustCtorOnlyForNonNestedTypes, bool writeRewriteReasonAsComment)
				: base(writeRewriteReasonAsComment)
	        {
		        _adjustCtorOnlyForNonNestedTypes = adjustCtorOnlyForNonNestedTypes;
	        }
	        
	        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
	        {
		        if (_adjustCtorOnlyForNonNestedTypes)
		        {
			        var typeNestedLevel = node.Ancestors().Count(a => a is TypeDeclarationSyntax);
			        if (typeNestedLevel == 1)
			        {
				        return AdjustCtorOrDestructorNameForTypeAdjustment(node, node.Identifier);
			        }
		        }
		        else
		        {
			        return AdjustCtorOrDestructorNameForTypeAdjustment(node, node.Identifier);
		        }

		        return base.VisitConstructorDeclaration(node);
	        }

	        public override SyntaxNode VisitDestructorDeclaration(DestructorDeclarationSyntax node)
	        {
		        if (_adjustCtorOnlyForNonNestedTypes)
		        {
			        var typeNestedLevel = node.Ancestors().Count(a => a is TypeDeclarationSyntax);
			        if (typeNestedLevel == 1)
			        {
				        return AdjustCtorOrDestructorNameForTypeAdjustment(node, node.Identifier);
			        }
		        }
		        else
		        {
			        return AdjustCtorOrDestructorNameForTypeAdjustment(node, node.Identifier);
		        }
		        
		        return base.VisitDestructorDeclaration(node);
	        }

	        private SyntaxNode AdjustCtorOrDestructorNameForTypeAdjustment(BaseMethodDeclarationSyntax node, SyntaxToken nodeIdentifier)
	        {
		        var typeName = (node.Ancestors().First(n => n is TypeDeclarationSyntax) as TypeDeclarationSyntax).Identifier.ToString();
		        if (!nodeIdentifier.ToFullString().Contains(typeName))
		        {
			        //Used Roslyn version bug, some static methods are also interpreted as ctors, eg
			        // public static void Method()
			        // {
			        //    Bar(); //treated as Ctor declaration...
			        // }
			        //
			        // private static void Bar() 
			        // {
			        //  
			        // }
			        return node;
		        }
		        
		        if (!typeName.EndsWith(AssemblyChangesLoader.ClassnamePatchedPostfix))
		        {
			        typeName += AssemblyChangesLoader.ClassnamePatchedPostfix;
		        }

		        return AddRewriteCommentIfNeeded(
			        node.ReplaceToken(nodeIdentifier, SyntaxFactory.Identifier(typeName)), 
			        $"{nameof(ConstructorRewriter)}:{nameof(AdjustCtorOrDestructorNameForTypeAdjustment)}"
			    );
	        }
        }
}