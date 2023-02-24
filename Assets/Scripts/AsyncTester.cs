using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class AsyncTester : MonoBehaviour
{
    private string TestNewField1;
    private string TestProp { get; set; } 
    
    // Start is called before the first frame update
    async Task Start()
    {
        LogMessage();
        StartCoroutine(TestCor());
    }

    void Update()
    {
        // LogMessage();
    }

    async void LogMessage()
    {

        Debug.Log("Async changes - changed2"); 

        await Task.Delay(1000);
    
        Debug.Log("Async after delay 1");

        await Task.Yield();
    }

    void OnScriptHotReload()
    {
        LogMessage();
        StartCoroutine(TestCor());
    }

    IEnumerator TestCor()
    {
        Debug.Log("Coroutine - changed1");
        yield return new WaitForSeconds(1);
        Debug.Log("Coroutine after wait");
    }
}
