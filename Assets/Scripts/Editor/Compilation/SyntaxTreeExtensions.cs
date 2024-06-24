using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;

public static class SyntaxTreeExtensions
{
    public static IEnumerable<SyntaxTree> CombinePartials(
        this IEnumerable<SyntaxTree> trees,
        IEnumerable<string> definedPreprocessorSymbols,
        out HashSet<string> typesDefined)
    {
        var result = new List<SyntaxTree>();
        typesDefined = new HashSet<string>();

        foreach (var tree in trees)
        {
            //if not partial, return original tree
            if (!tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().Any(t => t.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))))
            {
                result.Append(tree);
                continue;
            }

            // if partial, get and combine them
            HashSet<string> partialsFiles = FindPartialClassFiles(tree.FilePath);
            IEnumerable<SyntaxTree> partialTrees = partialsFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f)));
            SyntaxTree mergedTree = CombinePartialTrees(partialTrees, definedPreprocessorSymbols, out var types);
            result.Add(mergedTree.WithFilePath(tree.FilePath));
            typesDefined.UnionWith(types);
        }

        return result;
    }

    private static HashSet<string> FindPartialClassFiles(string sourceFilePath)
    {
        var partialClassFiles = new HashSet<string> { sourceFilePath };
        var sourceCode = File.ReadAllText(sourceFilePath);
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDeclaration in classDeclarations)
        {
            if (classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                var className = classDeclaration.Identifier.Text;
                var namespaceName = PartialClassFinder.GetNamespace(classDeclaration, root.SyntaxTree.GetCompilationUnitRoot());
                var otherPartialFiles = PartialClassFinder.FindOtherPartialClassFiles(className, namespaceName, sourceFilePath);
                partialClassFiles.UnionWith(otherPartialFiles);
            }
        }

        return partialClassFiles;
    }


    private static SyntaxTree CombinePartialTrees(IEnumerable<SyntaxTree> partialTrees, IEnumerable<string> definedPreprocessorSymbols, out HashSet<string> typesDefined)
    {
        var partialTypeDefinitions = new Dictionary<string, List<TypeDeclarationSyntax>>();
        var combinedUsingDirectives = new HashSet<UsingDirectiveSyntax>();
        typesDefined = new HashSet<string>();


        foreach (var partialTree in partialTrees)
        {
            var root = partialTree.GetRoot();
            ExtractUsingDirectives(root, combinedUsingDirectives);
            ProcessPartialTypeDefinitions(root, partialTypeDefinitions, typesDefined);
        }

        var combinedTree = partialTypeDefinitions.Select(partialType => CombinePartialTypes(partialType.Value))
                .Select(combinedType => SyntaxFactory.CompilationUnit()
                        .AddMembers(combinedType)
                        .WithUsings(SyntaxFactory.List(combinedUsingDirectives)))
                .Select(combinedRoot => CSharpSyntaxTree.ParseText(combinedRoot.ToFullString(), new CSharpParseOptions(preprocessorSymbols: definedPreprocessorSymbols)))
                .First();

        return combinedTree;
    }

    private static void ExtractUsingDirectives(SyntaxNode root, HashSet<UsingDirectiveSyntax> usingDirectives)
    {
        foreach (var usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            usingDirectives.Add(usingDirective.WithoutTrivia().NormalizeWhitespace());
        }
    }

    private static void ProcessPartialTypeDefinitions(
            SyntaxNode root,
            Dictionary<string, List<TypeDeclarationSyntax>> partialTypeDefinitions,
            HashSet<string> typesDefined)
    {
        var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        foreach (var type in types)
        {
            if (type.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                var fullName = GetTypeFullName(type);
                if (!partialTypeDefinitions.ContainsKey(fullName))
                {
                    partialTypeDefinitions[fullName] = new List<TypeDeclarationSyntax>();
                }
                partialTypeDefinitions[fullName].Add(type);
                typesDefined.Add(fullName);
            }
        }
    }

    private static TypeDeclarationSyntax CombinePartialTypes(List<TypeDeclarationSyntax> partialDeclarations)
    {
        var firstDeclaration = partialDeclarations.First();
        var combinedMembers = partialDeclarations.SelectMany(d => d.Members).ToList();

        // Combine base types and constraints
        var baseTypes = partialDeclarations
                .SelectMany(d => d.BaseList?.Types ?? Enumerable.Empty<BaseTypeSyntax>())
                .Distinct()
                .ToList();

        var constraints = partialDeclarations
                .SelectMany(d => d.ConstraintClauses)
                .Distinct()
                .ToList();

        var combinedType = firstDeclaration
                .WithMembers(SyntaxFactory.List(combinedMembers))
                .WithModifiers(SyntaxFactory.TokenList(firstDeclaration.Modifiers.Where(m => !m.IsKind(SyntaxKind.PartialKeyword))))
                .WithBaseList(baseTypes.Any() ? SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(baseTypes)) : null)
                .WithConstraintClauses(SyntaxFactory.List(constraints));

        // Preserve type parameters if present
        if (firstDeclaration is TypeDeclarationSyntax typeDeclaration && typeDeclaration.TypeParameterList != null)
        {
            combinedType = combinedType.WithTypeParameterList(typeDeclaration.TypeParameterList);
        }

        return combinedType.NormalizeWhitespace();
    }

    private static string GetTypeFullName(TypeDeclarationSyntax type)
    {
        var ns = type.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        return ns != null ? $"{ns.Name}.{type.Identifier}" : type.Identifier.ToString();
    }
}
