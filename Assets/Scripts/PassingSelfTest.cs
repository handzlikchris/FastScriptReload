using System;
using UnityEngine;

public class PassingSelfTest : MonoBehaviour 
{
    public string Value = "Test Value";  

    [ContextMenu(nameof(Pass))]
    void Pass()
    {
        
        // this.Value = Value;
        Debug.Log(this + "This test test" + Value); 
        PassingSelfManager.Pass(this, "test"); 
         
        PassingSelfManager.Pass((t) =>
        {
            t.Value = "changed in lambda";
            Debug.Log(t.Value);
            
            Debug.Log("local this.Value = " + this.Value);
        }, this, "raw string");
    }
}