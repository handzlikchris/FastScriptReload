using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    internal static class NamespaceHelper
    {
        internal static string GetNamespaceName(TypeDeclarationSyntax typeDeclaration)
        {
            var namespaces = new List<string>();
            var currentNode = typeDeclaration.Parent;
            while (currentNode != null)
            {
                if (currentNode is NamespaceDeclarationSyntax namespaceDeclaration)
                {
                    namespaces.Add(namespaceDeclaration.Name.ToString());
                }
                currentNode = currentNode.Parent;
            }
            namespaces.Reverse();
            return string.Join(".", namespaces);
        }
    }

    internal static class SyntaxTreeExtensions
    {
        internal static SyntaxTree AddInternalsVisibleToAttribute(this SyntaxTree tree)
        {
            var root = tree.GetCompilationUnitRoot();

            var attributeArgument = SyntaxFactory.AttributeArgument(
                    SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("Assembly-CSharp"))
            );
            var attribute = SyntaxFactory.Attribute(
                    SyntaxFactory.ParseName("System.Runtime.CompilerServices.InternalsVisibleTo"),
                    SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(new[] { attributeArgument }))
            );

            var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
                    .WithTarget(SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.AssemblyKeyword)));

            var newRoot = root.AddAttributeLists(attributeList);

            return tree.WithRootAndOptions(newRoot, tree.Options);
        }
    }
}
