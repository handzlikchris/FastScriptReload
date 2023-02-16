using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PartialClassSupport : MonoBehaviour
{
    [ContextMenu(nameof(Test))]
    void Test()
    {
        var partial = new PartialA();
        partial.Test();
    }
}