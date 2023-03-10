using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    public class ManualUserDefinedScriptOverridesRewriter : FastScriptReloadCodeRewriterBase
    {
        private readonly SyntaxNode _userDefinedOverridesRoot;

        public ManualUserDefinedScriptOverridesRewriter(SyntaxNode userDefinedOverridesRoot, bool writeRewriteReasonAsComment, bool visitIntoStructuredTrivia = false)
            : base(writeRewriteReasonAsComment, visitIntoStructuredTrivia)
        {
            _userDefinedOverridesRoot = userDefinedOverridesRoot;
        }

        public override SyntaxNode VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            var methodFQDN = RoslynUtils.GetMemberFQDN(node, "operator");
            var matchingInOverride = _userDefinedOverridesRoot.DescendantNodes()
                //implicit conversion operators do not have name, just parameter list
                .OfType<BaseMethodDeclarationSyntax>()
                .FirstOrDefault(m => m.ParameterList.ToString() == node.ParameterList.ToString() //parameter lists is type / order / names, all good for targetting if there's a proper match
                                      && methodFQDN == RoslynUtils.GetMemberFQDN(m, "operator") //make sure same FQDN, even though there's no name there could be more implicit operators in file
                );

            if (matchingInOverride != null)
            {
                return AddRewriteCommentIfNeeded(matchingInOverride.WithTriviaFrom(node), $"User defined custom conversion override", true);
            }
            else {
                return base.VisitConversionOperatorDeclaration(node);
            }
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var methodName = node.Identifier.ValueText;
            var methodFQDN = RoslynUtils.GetMemberFQDN(node, node.Identifier.ToString());
            var matchingInOverride = _userDefinedOverridesRoot.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodName
                                     && m.ParameterList.Parameters.Count == node.ParameterList.Parameters.Count
                                     && m.ParameterList.ToString() == node.ParameterList.ToString() //parameter lists is type / order / names, all good for targetting if there's a proper match
                                     && m.TypeParameterList?.ToString() == node.TypeParameterList?.ToString() //typed paratemets are for generics, also check
                                     && methodFQDN == RoslynUtils.GetMemberFQDN(m, m.Identifier.ToString()) //last check for mathod FQDN (potentially slower than others)
                );

            if (matchingInOverride != null)
            {
                return AddRewriteCommentIfNeeded(matchingInOverride.WithTriviaFrom(node), $"User defined custom method override", true);
            }
            else {
                return base.VisitMethodDeclaration(node);
            }
        }
    }
}