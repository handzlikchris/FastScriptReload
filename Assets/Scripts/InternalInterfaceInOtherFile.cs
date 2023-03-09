using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InternalInterfaceInOtherFile : MonoBehaviour, IInternalInterface
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update() 
    { 
        // Test("tst");
    }

    static void OnScriptHotReloadNoInstance()  
    {  
        var go = new GameObject("Test");
        go.AddComponent<InternalInterfaceInOtherFile>().Test("arg");  
    } 
 
    public void Test(string arg) 
    {
        Debug.Log("TEst" + arg);
        throw new System.NotImplementedException();
    }
}
