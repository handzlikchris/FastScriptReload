using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PassingSelfTest : MonoBehaviour 
{
    public string Value = "Test Value";  

    [ContextMenu(nameof(Pass))]
    void Pass()
    {
        PassingSelfManager.Pass(this);
    }
}