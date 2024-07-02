using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    public static class PartialSyntaxTreeExtensions
    {
        public static IEnumerable<SyntaxTree> CombinePartials(
                this IEnumerable<SyntaxTree> trees,
                IEnumerable<string> definedPreprocessorSymbols,
                out HashSet<string> typesDefined)
        {
            var processedTrees = trees
                    .Select(tree => ProcessPartialTree(tree, definedPreprocessorSymbols))
                    .ToList();

            typesDefined = ExtractTypesDefined(processedTrees);

            return processedTrees.Select(info => info);
        }


        private static SyntaxTree ProcessPartialTree(SyntaxTree tree, IEnumerable<string> definedPreprocessorSymbols)
        {
            if (!HasPartialTypes(tree))
            {
                return tree;
            }

            //Note: we are looking for partials only in the same directory and max depth of 5
            const int fileSearchMaxDepth = 5;
            return PartialClassFinder.FindPartialClassFilesInDirectory(tree.FilePath, fileSearchMaxDepth)
                    .Select(File.ReadAllText)
                    .SourceToSyntaxTree(tree.FilePath)
                    .Aggregate((left, right) => CombineSyntaxTreePartials(left, right, definedPreprocessorSymbols));
        }

        private static HashSet<string> ExtractTypesDefined(IEnumerable<SyntaxTree> processedTrees)
        {
            return processedTrees
                    .SelectMany(tree => tree.GetCompilationUnitRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
                    .Select(GetFullyQualifiedName)
                    .ToHashSet();
        }

        private static bool HasPartialTypes(SyntaxTree tree)
        {
            var root = tree.GetCompilationUnitRoot();
            return root.DescendantNodes().OfType<TypeDeclarationSyntax>().Any(type => type.Modifiers.Any(SyntaxKind.PartialKeyword));
        }

        private static SyntaxTree CombineSyntaxTreePartials(SyntaxTree left, SyntaxTree right, IEnumerable<string> definedPreprocessorSymbols)
        {
            var combinedTypes = new Dictionary<string, List<TypeDeclarationSyntax>>();
            var combinedUsingDirectives = new HashSet<UsingDirectiveSyntax>();
            var combinedTypesDefined = new HashSet<string>();

            ProcessTree(left, combinedTypes, combinedUsingDirectives, combinedTypesDefined);
            ProcessTree(right, combinedTypes, combinedUsingDirectives, combinedTypesDefined);

            var combinedTypeDeclarations = combinedTypes
                    .Select(kvp => CombinePartialType(kvp.Value))
                    .ToList<MemberDeclarationSyntax>();

            var newRoot = SyntaxFactory.CompilationUnit()
                    .WithUsings(SyntaxFactory.List(combinedUsingDirectives))
                    .WithMembers(SyntaxFactory.List(combinedTypeDeclarations))
                    .WithAdditionalAnnotations(new SyntaxAnnotation("PreprocessorSymbols", string.Join(",", definedPreprocessorSymbols)));

            var filePath = string.IsNullOrEmpty(left.FilePath) ? right.FilePath : left.FilePath;
            return CSharpSyntaxTree.Create(newRoot, null, filePath);
        }

        private static void ProcessTree(
                SyntaxTree tree,
                Dictionary<string, List<TypeDeclarationSyntax>> combinedTypes,
                HashSet<UsingDirectiveSyntax> combinedUsingDirectives,
                HashSet<string> combinedTypesDefined)
        {
            var root = tree.GetCompilationUnitRoot();

            // Process using directives
            combinedUsingDirectives.UnionWith(root.Usings);

            // Process type declarations
            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var fullName = GetFullyQualifiedName(typeDecl);
                combinedTypesDefined.Add(fullName);

                if (!combinedTypes.TryGetValue(fullName, out var declarations))
                {
                    declarations = new List<TypeDeclarationSyntax>();
                    combinedTypes[fullName] = declarations;
                }
                declarations.Add(typeDecl);
            }
        }

        private static TypeDeclarationSyntax CombinePartialType(List<TypeDeclarationSyntax> declarations)
        {
            if (declarations.Count == 1)
                return declarations[0];

            var firstDeclaration = declarations[0];
            var combinedMembers = declarations.SelectMany(d => d.Members).ToList();

            // Correctly handle modifiers with proper spacing
            var combinedModifiers = declarations
                    .SelectMany(d => d.Modifiers)
                    .Where(m => !m.IsKind(SyntaxKind.PartialKeyword))
                    .GroupBy(m => m.ValueText)
                    .Select(g => g.First().WithTrailingTrivia(SyntaxFactory.Space))
                    .ToList();

            // Ensure 'partial' keyword is present
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


        private static string GetFullyQualifiedName(TypeDeclarationSyntax typeDeclaration)
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
            var namespaceName = string.Join(".", namespaces);
            return string.IsNullOrEmpty(namespaceName)
                    ? typeDeclaration.Identifier.Text
                    : $"{namespaceName}.{typeDeclaration.Identifier.Text}";
        }
    }
}
