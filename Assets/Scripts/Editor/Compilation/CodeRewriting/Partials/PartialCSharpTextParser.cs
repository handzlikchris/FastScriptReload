using System;
using System.Collections.Generic;
using System.Linq;
using FastScriptReload.Editor.Compilation;
using FastScriptReload.Editor.Compilation.CodeRewriting.Partials;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using UnityEngine;

public static class PartialCSharpTextParser
{
    public static IEnumerable<PartialTreeInfo> SourceToSyntaxTree(this IEnumerable<string> sourceTexts, string filePath)
    {
        var parseOptions = new CSharpParseOptions(preprocessorSymbols: DynamicCompilationBase.ActiveScriptCompilationDefines);

        return sourceTexts
            .Select(text => ParseSingleText( filePath, text, parseOptions))
            .Where(result => result.IsSuccess)
            .Select(result => result.PartialTreeInfo);
    }

    private static ParseResult ParseSingleText(string filePath, string sourceText, CSharpParseOptions options)
    {
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(sourceText), options).WithFilePath(filePath);
            var errors = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            if (errors.Any())
            {
                LogErrors(errors);
                return ParseResult.Failure();
            }

            var partialTreeInfo = new PartialTreeInfo(syntaxTree, options.PreprocessorSymbolNames.ToList());
            return ParseResult.Success(partialTreeInfo);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception while parsing C# text: {ex.Message}");
            return ParseResult.Failure();
        }
    }

    private static void LogErrors(IEnumerable<Diagnostic> errors)
    {
        foreach (var error in errors)
        {
            Debug.LogError($"Parsing error: {error}");
        }
    }

    private class ParseResult
    {
        public bool IsSuccess { get; }
        public PartialTreeInfo PartialTreeInfo { get; }

        private ParseResult(bool isSuccess, PartialTreeInfo partialTreeInfo = null)
        {
            IsSuccess = isSuccess;
            PartialTreeInfo = partialTreeInfo;
        }

        public static ParseResult Success(PartialTreeInfo partialTreeInfo) => new ParseResult(true, partialTreeInfo);
        public static ParseResult Failure() => new ParseResult(false);
    }
}
