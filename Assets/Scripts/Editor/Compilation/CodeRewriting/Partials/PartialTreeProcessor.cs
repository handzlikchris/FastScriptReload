using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    internal static class PartialTreeProcessor
    {
        /// <summary>
        /// Processes a syntax tree containing partial type definitions, merging it with other partial definitions found in the same directory.
        /// </summary>
        /// <param name="tree">The original syntax tree to process.</param>
        /// <param name="definedPreprocessorSymbols">A collection of defined preprocessor symbols.</param>
        /// <returns>A new SyntaxTree with merged partial types and added InternalsVisibleTo attribute.</returns>
        /// <remarks>
        /// This method performs the following steps:
        /// 1. Checks if the tree contains partial types.
        /// 2. If it does, processes the current tree and other partial files in the same directory.
        /// 3. Combines all partial type declarations.
        /// 4. Creates a new compilation unit with merged types and using directives.
        /// 5. Adds the InternalsVisibleTo attribute to the resulting tree.
        /// </remarks>
        internal static SyntaxTree ProcessPartialTree(SyntaxTree tree, IEnumerable<string> definedPreprocessorSymbols)
        {
            if (!HasPartialTypes(tree))
            {
                return tree;
            }

            var combinedTypes = new Dictionary<string, List<TypeDeclarationSyntax>>();
            var combinedUsingDirectives = new HashSet<UsingDirectiveSyntax>();
            var combinedTypesDefined = new HashSet<string>();

            ProcessTree(tree, combinedTypes, combinedUsingDirectives, combinedTypesDefined);

            const int fileSearchMaxDepth = 5;
            var otherPartialFiles = PartialClassFinder.FindPartialClassFilesInDirectory(tree.FilePath, fileSearchMaxDepth)
                    .Where(file => file != tree.FilePath);

            foreach (var file in otherPartialFiles)
            {
                var partialTree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
                ProcessTree(partialTree, combinedTypes, combinedUsingDirectives, combinedTypesDefined);
            }

            var combinedTypeDeclarations = combinedTypes
                    .Select(kvp => PartialTypeCombiner.CombinePartialType(kvp.Value))
                    .ToList<MemberDeclarationSyntax>();

            var newRoot = SyntaxFactory.CompilationUnit()
                    .WithUsings(SyntaxFactory.List(combinedUsingDirectives))
                    .WithMembers(SyntaxFactory.List(combinedTypeDeclarations))
                    .WithAdditionalAnnotations(new SyntaxAnnotation("PreprocessorSymbols", string.Join(",", definedPreprocessorSymbols)));

            return CSharpSyntaxTree.Create(newRoot, null, tree.FilePath)
                    .AddInternalsVisibleToAttribute();
        }

        private static bool HasPartialTypes(SyntaxTree tree)
        {
            var root = tree.GetCompilationUnitRoot();
            return root.DescendantNodes().OfType<TypeDeclarationSyntax>().Any(type => type.Modifiers.Any(SyntaxKind.PartialKeyword));
        }

        private static void ProcessTree(
                SyntaxTree tree,
                Dictionary<string, List<TypeDeclarationSyntax>> combinedTypes,
                HashSet<UsingDirectiveSyntax> combinedUsingDirectives,
                HashSet<string> combinedTypesDefined)
        {
            var root = tree.GetCompilationUnitRoot();

            combinedUsingDirectives.UnionWith(root.Usings);

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var fullName = NamespaceHelper.GetFullyQualifiedName(typeDecl);
                combinedTypesDefined.Add(fullName);

                if (!combinedTypes.TryGetValue(fullName, out var declarations))
                {
                    declarations = new List<TypeDeclarationSyntax>();
                    combinedTypes[fullName] = declarations;
                }
                declarations.Add(typeDecl);
            }
        }
    }
}
