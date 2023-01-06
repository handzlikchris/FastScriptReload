using System.Linq;
using FastScriptReload.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
        class ConstructorRewriter : CSharpSyntaxRewriter
        {
	        private readonly bool _adjustCtorOnlyForNonNestedTypes;
	        
	        public ConstructorRewriter(bool adjustCtorOnlyForNonNestedTypes)
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
				        return AdjustCtorNameForTypeAdjustment(node);
			        }
		        }
		        else
		        {
			        return AdjustCtorNameForTypeAdjustment(node);
		        }

		        return base.VisitConstructorDeclaration(node);
	        }

	        private static SyntaxNode AdjustCtorNameForTypeAdjustment(ConstructorDeclarationSyntax node)
	        {
		        var typeName = (node.Ancestors().First(n => n is TypeDeclarationSyntax) as TypeDeclarationSyntax).Identifier
			        .ToString();
		        if (!typeName.EndsWith(AssemblyChangesLoader.ClassnamePatchedPostfix))
		        {
			        typeName += AssemblyChangesLoader.ClassnamePatchedPostfix;
		        }

		        return node.ReplaceToken(node.Identifier, SyntaxFactory.Identifier(typeName));
	        }
        }
}