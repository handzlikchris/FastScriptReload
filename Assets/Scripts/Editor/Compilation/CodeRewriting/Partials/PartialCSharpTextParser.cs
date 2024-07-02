using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using UnityEngine;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    public static class PartialCSharpTextParser
    {
        public static IEnumerable<SyntaxTree> SourceToSyntaxTree(this IEnumerable<string> sourceTexts, string filePath)
        {
            var parseOptions = new CSharpParseOptions(preprocessorSymbols: DynamicCompilationBase.ActiveScriptCompilationDefines);

            return sourceTexts
                    .Select(text => ParseSingleText(filePath, text, parseOptions))
                    .Where(result => result.IsSuccess)
                    .Select(result => result.SyntaxTree);
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

                return ParseResult.Success(syntaxTree);
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
            public SyntaxTree SyntaxTree { get; }

            private ParseResult(bool isSuccess, SyntaxTree partialTreeInfo = null)
            {
                IsSuccess = isSuccess;
                SyntaxTree = partialTreeInfo;
            }

            public static ParseResult Success(SyntaxTree partialTreeInfo) => new ParseResult(true, partialTreeInfo);
            public static ParseResult Failure() => new ParseResult(false);
        }
    }
}
