using UnityEngine;

public class InternalInterfaceInOtherFile : MonoBehaviour, IInternalInterface
{
    public float Test123 = 5f;
    
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
