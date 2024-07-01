using System.Collections.Generic;
using Microsoft.CodeAnalysis;
namespace FastScriptReload.Editor.Compilation.CodeRewriting.Partials
{
    public class PartialTreeInfo
    {
        public SyntaxTree Tree { get; }
        public IEnumerable<string> DefinedPreprocessorSymbols { get; }

        public PartialTreeInfo(SyntaxTree tree, IEnumerable<string> definedPreprocessorSymbols)
        {
            Tree = tree;
            DefinedPreprocessorSymbols = definedPreprocessorSymbols;
        }
    }
}
