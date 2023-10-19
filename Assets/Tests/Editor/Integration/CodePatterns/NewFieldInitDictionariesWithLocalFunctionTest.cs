using System.Collections;
using FastScriptReload.Tests.Runtime.Integration.CodePatterns;
using UnityEngine.TestTools;

namespace FastScriptReload.Tests.Editor.Integration.CodePatterns
{
    public class NewFieldInitDictionariesWithLocalFunctionTest : CompileWithRedirectTestBase
    {
        private static readonly string RelativeFilePath = @$"Runtime\Integration\CodePatterns\{nameof(NewFieldInitDictionariesWithLocalFunction)}.cs";

        [UnityTest]
        public IEnumerator TestStaticFieldAccess_StandardCompilation_CorrectRewriteCreated()
        {
            TestCompileAndDetour(ResolveFullTestFilePath(RelativeFilePath));

            yield return null;
        }
    }
}