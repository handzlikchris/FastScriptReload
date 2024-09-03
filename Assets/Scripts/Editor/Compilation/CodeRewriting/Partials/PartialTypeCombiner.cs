using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    internal static class PartialTypeCombiner
    {
        internal static TypeDeclarationSyntax CombinePartialType(List<TypeDeclarationSyntax> declarations)
        {
            if (declarations.Count == 1)
                return declarations[0];

            var firstDeclaration = declarations[0];
            var combinedMembers = declarations.SelectMany(d => d.Members).ToList();

            var combinedModifiers = declarations
                    .SelectMany(d => d.Modifiers)
                    .Where(m => !m.IsKind(SyntaxKind.PartialKeyword))
                    .GroupBy(m => m.ValueText)
                    .Select(g => g.First().WithTrailingTrivia(SyntaxFactory.Space))
                    .ToList();

            if (!combinedModifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                combinedModifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space));

            var combinedBaseList = declarations
                    .Where(d => d.BaseList != null)
                    .SelectMany(d => d.BaseList.Types)
                    .GroupBy(t => t.ToString())
                    .Select(g => g.First())
                    .ToList();

            return firstDeclaration
                    .WithMembers(SyntaxFactory.List(combinedMembers))
                    .WithModifiers(SyntaxFactory.TokenList(combinedModifiers))
                    .WithKeyword(firstDeclaration.Keyword.WithLeadingTrivia(SyntaxFactory.Space))
                    .WithBaseList(combinedBaseList.Any()
                            ? SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(combinedBaseList))
                            : null);
        }
    }
}
