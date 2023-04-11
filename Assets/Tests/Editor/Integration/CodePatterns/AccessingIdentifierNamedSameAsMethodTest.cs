using System.Collections;
using System.Linq;
using System.Reflection;
using FastScriptReload.Tests.Runtime.Integration.CodePatterns;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.TestTools;

namespace FastScriptReload.Tests.Editor.Integration.CodePatterns
{
    public class AccessingIdentifierNamedSameAsMethodTest : CompileWithRedirectTestBase
    {
        private static readonly string RelativeFilePath = @"Runtime\Integration\CodePatterns\AccessingIdentifierNamedSameAsMethod.cs";

        [UnityTest]
        public IEnumerator TestInstanceFieldTypeRewritten_StandardCompilation_TypeForInstanceVariableCorrectlyRewritten()
        {
            var instance = new GameObject("instance").AddComponent<AccessingIdentifierNamedSameAsMethod>();

            var patchedTypeName = nameof(AccessingIdentifierNamedSameAsMethod) + FastScriptReload.Runtime.AssemblyChangesLoader.ClassnamePatchedPostfix;
            TestCompileAndDetour(ResolveFullTestFilePath(RelativeFilePath), (compileResult) =>
            {
                instance.TestInstanceFieldTypeRewritten();
            });
            
            AssertDetourConfirmed(typeof(AccessingIdentifierNamedSameAsMethod), nameof(instance.TestInstanceFieldTypeRewritten), 
                (o) => o.ToString() == patchedTypeName,
                "Value should be set after compilation"
            );

            yield return null;
        }
        
        [UnityTest]
        public IEnumerator TestAccessingSameNamedVariableNotChanged_StandardCompilation_PropertyAccessInCallNotReplaced()
        {
            var instance = new GameObject("instance").AddComponent<AccessingIdentifierNamedSameAsMethod>();

            TestCompileAndDetour(ResolveFullTestFilePath(RelativeFilePath), (compileResult) =>
            {
                instance.TestAccessingSameNamedVariableNotChanged();
            });
            
            AssertDetourConfirmed(typeof(AccessingIdentifierNamedSameAsMethod), nameof(instance.TestAccessingSameNamedVariableNotChanged), 
                (r) => (bool)r == true,
                "Value should be set after compilation"
            );
            
            yield return null;
        }
    }
}