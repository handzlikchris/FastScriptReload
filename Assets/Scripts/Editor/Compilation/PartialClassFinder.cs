using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using UnityEngine;

public static class PartialClassFinder
{
    public static HashSet<string> FindOtherPartialClassFiles(string className, string namespaceName, string sourceFilePath)
    {
        var directory = Path.GetDirectoryName(sourceFilePath);
        if (string.IsNullOrEmpty(directory))
        {
            Debug.LogError($"Invalid source file path: {sourceFilePath}");
            return new HashSet<string>();
        }

        return FindPartialClassFilesInDirectory(directory, className, namespaceName);
    }

    private static HashSet<string> FindPartialClassFilesInDirectory(string directory, string className, string namespaceName)
    {
        var partialClassFiles = new HashSet<string>();

        try
        {
            var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (FileContainsPartialClass(file, className, namespaceName))
                {
                    partialClassFiles.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error accessing directory {directory}: {ex.Message}");
        }

        return partialClassFiles;
    }

    private static bool FileContainsPartialClass(string filePath, string className, string namespaceName)
    {
        try
        {
            using var streamReader = new StreamReader(filePath);
            var sourceText = SourceText.From(streamReader.BaseStream);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, new CSharpParseOptions(kind: SourceCodeKind.Regular, documentationMode: DocumentationMode.None));

            var root = syntaxTree.GetCompilationUnitRoot();
            return ContainsMatchingPartialClass(root, className, namespaceName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error processing file {filePath}: {ex.Message}");
            return false;
        }
    }

    private static bool ContainsMatchingPartialClass(CompilationUnitSyntax root, string className, string namespaceName)
    {
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        return classDeclarations.Any(classDeclaration =>
            IsMatchingPartialClass(classDeclaration, className, namespaceName, root));
    }

    private static bool IsMatchingPartialClass(ClassDeclarationSyntax classDeclaration, string className, string namespaceName, CompilationUnitSyntax root)
    {
        return classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword) &&
               classDeclaration.Identifier.Text == className &&
               GetNamespace(classDeclaration, root) == namespaceName;
    }

    public static string GetNamespace(ClassDeclarationSyntax classDeclaration, CompilationUnitSyntax root)
    {
        var fileScopedNamespace = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScopedNamespace != null)
        {
            return fileScopedNamespace.Name.ToString();
        }

        var namespaceDeclaration = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        return namespaceDeclaration?.Name.ToString() ?? string.Empty;
    }
}
