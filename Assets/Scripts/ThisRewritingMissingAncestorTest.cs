using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ThisRewritingMissingAncestorTest : MonoBehaviour
{
    private GameObject Test(bool noInstantiate = false)
    {
        GameObject o = null;
        SomeComponentToAssign c = o.GetComponent<SomeComponentToAssign>(); 
        if (c != null) { c.testInstance = this; }   

        return null;
    }
}