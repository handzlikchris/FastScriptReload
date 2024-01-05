using System.Collections;
using System.Reflection;
using FastScriptReload.Tests.Runtime.Integration.CodePatterns;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.TestTools;

namespace FastScriptReload.Tests.Editor.Integration.CodePatterns
{
    public class MethodAccessingNestedEnumTest : CompileWithRedirectTestBase
    {
        [UnityTest]
        public IEnumerator AssignNestedEnumToField_ValueAssignedDirectly_CorrectValueOnOriginalInstance()
        {
            //TODO: nested enums were broken as code-adjustments were too wide, need to retarget just to enums
            
            var instance = new GameObject("instance").AddComponent<MethodAccessingNestedEnum>();
            var originalValue = (int)instance.GetType().GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
            
            TestCompileAndDetour(ResolveFullTestFilePath(@"Runtime\Integration\CodePatterns\MethodAccessingNestedEnum.cs"), (compilationResult) =>
            {
                Debug.Log("After");
                instance.AssignNestedEnumToField();
            });

            Assert.AreEqual(originalValue, 0);
            AssertDetourConfirmed(typeof(MethodAccessingNestedEnum), nameof(instance.AssignNestedEnumToField),
                (o) => 2 == (int)o,
                "Value should be set by detour"
            );
            
            yield return null;
        }
    }
}