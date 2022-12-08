using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class AsyncTester : MonoBehaviour
{
    // Start is called before the first frame update
    async Task Start()
    {
        await LogMessage();
    }

    async Task LogMessage()
    {
        while(true)
        {
            Debug.Log("Async changes 12311");

            await Task.Delay(1000);
        
            Debug.Log("Async after delay 12311");

            await Task.Yield();
        }

    }
}
