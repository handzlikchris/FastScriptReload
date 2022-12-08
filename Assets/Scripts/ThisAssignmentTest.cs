using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThisAssignmentTest : MonoBehaviour
{
    private ThisAssignmentTest test;
    
    [ContextMenu(nameof(Test))]
    void Test()
    {
        test = this;
        Debug.Log($"Now test - {test.GetType().Name} - {test}"); 
        
        new NestedAssignmentTest().Test();
    }

    private class NestedAssignmentTest
    {
        private NestedAssignmentTest test;
        
        [ContextMenu(nameof(Test))] 
        public void Test()
        {
            test = this; 
            Debug.Log($"Nested test - {test.GetType().Name} - {test} 25 6");     
        }
    }
}
