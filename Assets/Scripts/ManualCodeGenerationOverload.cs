using UnityEngine;

namespace  Root
{

public class ManualCodeGenerationOverload : MonoBehaviour
{
    void Update()
    {
        // MethodToChange1();
    }

    void MethodToLeaveUntouched()
    {
        Debug.Log("MethodToLeaveUntouched");
    }
    
    void MethodToChange1()
    {
        Debug.Log("MethodToChange1 - 1");  
    }
    
    void MethodToChange1(string arg1)
    {
        Debug.Log("MethodToChange1 with arg1");
    }
    
    void MethodToChange1<T>(string arg1, int sad)
    {
        void InnerFunction()
        {
            Debug.Log("Inner");

        }
        Debug.Log("MethodToChange1 with arg1");
    }
    
    void MethodToChange2(string arg1)
    {
        Debug.Log("MethodToChange2 with arg1");
    }

    void OnScriptHotReload()
    {
        Debug.Log("Hot reloaded");
        MethodToChange1(); 
        MethodToChange1<int>("test", 2); 
    }
}

}