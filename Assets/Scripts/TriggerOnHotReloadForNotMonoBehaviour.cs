using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerOnHotReloadForNotMonoBehaviour : MonoBehaviour
{
    [ContextMenu(nameof(Test))]
    void Test()
    {
        var o = new NonMonoBehaviourReload();
        o.Test();
        
        NonMonoBehaviourReload.TestStatic();
    }
}
