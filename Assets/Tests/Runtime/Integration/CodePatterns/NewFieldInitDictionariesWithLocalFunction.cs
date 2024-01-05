using UnityEngine;

namespace FastScriptReload.Tests.Runtime.Integration.CodePatterns
{
    public class NewFieldInitDictionariesWithLocalFunction : MonoBehaviour
    {
        public void CompileTest()
        {
            LocalFunction();
            
            void LocalFunction()
            {
                object a = 1;
                if (a is not int)
                {
                    return;
                }
                Debug.Log("Test");
            }
            
            //<mock-runtime-code-change>// Debug.Log("Trigger change for compilation");
        }
    }
}