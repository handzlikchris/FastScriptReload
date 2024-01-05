using UnityEngine;

namespace FastScriptReload.Tests.Runtime.Integration.CodePatterns
{
    public class AccessingNewFieldsAddedAtRuntimeSameNameAsStaticClassField : MonoBehaviour 
    {
        //<mock-runtime-code-change>// public bool deltaTime = true;
        
        //when using newly added field that has same name as static class field name, it should not get rewritten
        public void CompileTest()
        {
            //<mock-runtime-code-change>// var value = Time.deltaTime; //this value should not get rewritten
            //<mock-runtime-code-change>// var value1 = deltaTime;
        }
    }
}