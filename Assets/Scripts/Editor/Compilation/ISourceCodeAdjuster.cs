using System.Collections.Generic;

namespace FastScriptReload.Editor.Compilation
{
    public interface ISourceCodeAdjuster
    {
        CreateSourceCodeCombinedContentsResult CreateSourceCodeCombinedContents(List<string> sourceCodeFiles,
            List<string> definedPreprocessorSymbols);
    }
}