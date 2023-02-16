using UnityEngine;

public class NonMonoBehaviourReload
{
    public static void TestStatic()
    {
        Debug.Log("NonMonoBehaviourReload: TestStatic 1");
    }

    public void Test() 
    {
        Debug.Log("NonMonoBehaviourReload: Test 1  "); 
    }

    static void OnScriptHotReloadNoInstance()
    {
        Debug.Log("NonMonoBehaviourReload: Hotreloaded");
    }

    void OnScriptHotReload()
    {
        Debug.Log("Test"); 
    }
}
