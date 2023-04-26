using System.Collections.Generic;

namespace FastScriptReload.Editor.Compilation
{
    public class ChatGptErrorAwareSourceCodeAdjuster : ISourceCodeAdjuster
    {
        private static string FixErrorsPromptTemplate = 
@"Unity C# code fails to compile with error, please fix the code to make it compilable.
Do not remove __Patched_ postfix from class names, instead adjust the code accordingly.
Do not include any explanation in your response, only code.

Error Message:
<errorMessage>

Code:
<code>";
        
        private readonly string _latestCompilationError;
        private readonly string _sourceCodeWithError;

        public ChatGptErrorAwareSourceCodeAdjuster(string latestCompilationError, string sourceCodeWithError)
        {
            _latestCompilationError = latestCompilationError;
            _sourceCodeWithError = sourceCodeWithError;
        }

        public CreateSourceCodeCombinedContentsResult CreateSourceCodeCombinedContents(List<string> sourceCodeFiles, List<string> definedPreprocessorSymbols)
        {
            throw new System.NotImplementedException();
        }
    }
}