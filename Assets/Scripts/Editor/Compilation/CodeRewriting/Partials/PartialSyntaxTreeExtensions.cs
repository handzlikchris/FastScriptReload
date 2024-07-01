using System.Collections.Generic;
using System.IO;
using System.Linq;
using FastScriptReload.Editor.Compilation.CodeRewriting.Partials;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class PartialSyntaxTreeExtensions
{
    public static IEnumerable<SyntaxTree> CombinePartials(
        this IEnumerable<SyntaxTree> trees,
        IEnumerable<string> definedPreprocessorSymbols,
        out HashSet<string> typesDefined)
    {
        var processedTrees = trees
            .Select(tree => new PartialTreeInfo(tree, definedPreprocessorSymbols))
            .Select(ProcessPartialTree)
            .ToList();

        typesDefined = ExtractTypesDefined(processedTrees);

        return processedTrees.Select(info => info.Tree);
    }

    private static PartialTreeInfo ProcessPartialTree(PartialTreeInfo treeInfo)
    {
        if (!HasPartialTypes(treeInfo.Tree))
        {
            return treeInfo;
        }

        return PartialClassFinder.FindPartialClassFilesInDirectory(treeInfo.Tree.FilePath)
                .Select(File.ReadAllText)
                .SourceToSyntaxTree(treeInfo.Tree.FilePath)
                .Aggregate(CombinePartials);
    }

    private static HashSet<string> ExtractTypesDefined(IEnumerable<PartialTreeInfo> processedTrees)
    {
        return new HashSet<string>(processedTrees.SelectMany(info => info.DefinedPreprocessorSymbols));
    }

    private static bool HasPartialTypes(SyntaxTree tree)
    {
        var root = tree.GetCompilationUnitRoot();
        return root.DescendantNodes().OfType<TypeDeclarationSyntax>().Any(type => type.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private static PartialTreeInfo CombinePartials(PartialTreeInfo left, PartialTreeInfo right)
    {
        var combinedTypes = new Dictionary<string, List<TypeDeclarationSyntax>>();
        var combinedUsingDirectives = new HashSet<UsingDirectiveSyntax>();
        var combinedTypesDefined = new HashSet<string>();

        ProcessTree(left.Tree, combinedTypes, combinedUsingDirectives, combinedTypesDefined);
        ProcessTree(right.Tree, combinedTypes, combinedUsingDirectives, combinedTypesDefined);

        var combinedTypeDeclarations = combinedTypes
            .Select(kvp => CombinePartialType(kvp.Value))
            .ToList<MemberDeclarationSyntax>();

        var combinedPreprocessorSymbols = left.DefinedPreprocessorSymbols
            .Union(right.DefinedPreprocessorSymbols)
            .ToList();

        var newRoot = SyntaxFactory.CompilationUnit()
                .WithUsings(SyntaxFactory.List(combinedUsingDirectives))
                .WithMembers(SyntaxFactory.List(combinedTypeDeclarations));
                //.WithAdditionalAnnotations(new SyntaxAnnotation("PreprocessorSymbols", string.Join(",", combinedPreprocessorSymbols)));

        var filePath = string.IsNullOrEmpty(left.Tree.FilePath) ? right.Tree.FilePath : left.Tree.FilePath;
        var newTree = CSharpSyntaxTree.Create(newRoot, null, filePath);
        return new PartialTreeInfo(newTree, combinedPreprocessorSymbols);
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
