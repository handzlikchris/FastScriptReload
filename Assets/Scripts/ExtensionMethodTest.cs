using System.Collections;
using System.Collections.Generic;
using Extensions;
using UnityEngine;

public class ExtensionMethodTest : MonoBehaviour
{
    public string TestVal = "test";
    
    void Update()
    {
        var t = new TestExtensionObj();
        t.Test("123 456");
        
        // Debug.Log("ExtensionTest:inside"); 
        // this.PrintTest();
        // ExtensionsTests.PrintTest(this);
    }
}

//
// public class ExtensionMethodTest__Patched_ : MonoBehaviour
// {
//     public string TestVal = "test";
//     
//     void Update()
//     {
//         Debug.Log("ExtensionTest:inside"); 
//         // this.PrintTest();
//         // ExtensionsTests.PrintTest((dynamic)this);
//     }
// }