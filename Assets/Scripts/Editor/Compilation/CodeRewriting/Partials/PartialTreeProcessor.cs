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

            // root key is namespace, inner key is type
            var combinedTypes = new Dictionary<string, Dictionary<string, List<TypeDeclarationSyntax>>>();
            (HashSet<string>, HashSet<UsingDirectiveSyntax> syntaxes) combinedUsings =
                (new HashSet<string>(), new HashSet<UsingDirectiveSyntax>());

            ProcessTree(tree, combinedTypes, combinedUsings);

            const int fileSearchMaxDepth = 5;
            var otherPartialFiles = PartialClassFinder.FindPartialClassFilesInDirectory(tree.FilePath, fileSearchMaxDepth)
                    .Where(file => file != tree.FilePath);

            foreach (var file in otherPartialFiles)
            {
                var partialTree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
                ProcessTree(partialTree, combinedTypes, combinedUsings);
            }

            var combinedTypeDeclarations = new List<MemberDeclarationSyntax>();
            foreach (var (namespaceName, types) in combinedTypes)
            {
                var typesInNamespace = types
                    .Select(type => PartialTypeCombiner.CombinePartialType(type.Value))
                    .ToList<MemberDeclarationSyntax>();

                if (string.IsNullOrEmpty(namespaceName))
                {
                    combinedTypeDeclarations.AddRange(typesInNamespace);
                }
                else
                {
                    var namespaceDeclaration = SyntaxFactory
                        .NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
                        .WithMembers(SyntaxFactory.List(typesInNamespace));

                    combinedTypeDeclarations.Add(namespaceDeclaration);
                }
            }

            var newRoot = SyntaxFactory.CompilationUnit()
                    .WithUsings(SyntaxFactory.List(combinedUsings.syntaxes))
                    .WithMembers(SyntaxFactory.List(combinedTypeDeclarations))
                    .WithAdditionalAnnotations(new SyntaxAnnotation("PreprocessorSymbols", string.Join(",", definedPreprocessorSymbols)))
                    .NormalizeWhitespace();

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
                Dictionary<string, Dictionary<string, List<TypeDeclarationSyntax>>> combinedTypes,
                (HashSet<string> strings, HashSet<UsingDirectiveSyntax> syntaxes) combinedUsings)
        {
            var root = tree.GetCompilationUnitRoot();

            foreach (var usingDirective in root.Usings)
            {
                if (combinedUsings.strings.Add(usingDirective.Name!.ToString()))
                {
                    combinedUsings.syntaxes.Add(usingDirective);
                }
            }

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (!typeDecl.Modifiers.Any(SyntaxKind.PublicKeyword))
                {
                    continue;
                }

                var namespaceName = NamespaceHelper.GetNamespaceName(typeDecl);
                var typeName = typeDecl.Identifier.Text;

                if (!combinedTypes.TryGetValue(namespaceName, out var typesInNamespace))
                {
                    typesInNamespace = new Dictionary<string, List<TypeDeclarationSyntax>>();
                    combinedTypes[namespaceName] = typesInNamespace;
                }

                if (!typesInNamespace.TryGetValue(typeName, out var typeList))
                {
                    typeList = new List<TypeDeclarationSyntax>();
                    typesInNamespace[typeName] = typeList;
                }
                typeList.Add(typeDecl);
            }
        }
    }
}
