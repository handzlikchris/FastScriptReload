using UnityEngine;

public class InternalInterfaceInOtherFile : MonoBehaviour, IInternalInterface
{
    public float Test123 = 5f;
    
    // static void OnScriptHotReloadNoInstance()  
    // {   
    //     var go = new GameObject("Test");
    //     go.AddComponent<InternalInterfaceInOtherFile>().Test("arg");       
    // }

    private void Update()
    {
        // Test("Update-changed1: "); 
    }

    // void OnScriptHotReload() 
    // {
    //     Debug.Log("TSTS!" + Test123);   
    // } 

    public void Test(string arg)   
    {
        Debug.Log("TEst" + arg);

        Debug.Log("Test Call back to existing:" + ExistingSingletonTest.Instance.IntValueGetter);     
    }
}