using AsmB;
using UnityEngine;

namespace AsmA
{
    public class TestParent : TestChild
    {
        [ContextMenu("Test")]
        void Test()
        {
            // Debug.Log(child._protectedField);  
        }
    }
}

