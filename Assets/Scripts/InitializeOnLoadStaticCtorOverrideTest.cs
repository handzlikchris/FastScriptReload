using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class InitializeOnLoadStaticCtorOverrideTest
{
    static InitializeOnLoadStaticCtorOverrideTest()
    {
        Debug.Log("Test Initialize on load");      
    }
}
