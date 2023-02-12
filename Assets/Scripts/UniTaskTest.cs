using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class UniTaskTest : MonoBehaviour
{
    [ContextMenu("Test")]
    async void Test()
    {
        Debug.Log("Test called");
        await UniTask.Delay(TimeSpan.FromMilliseconds(300));
        
        Debug.Log("Test called delayed");

        var iterationCount = 0;
        while (iterationCount++ < 10)
        {
            var result = await GetTestString();
            Debug.Log(result);
            await UniTask.Delay(TimeSpan.FromMilliseconds(1000));
        }
    }

    private async UniTask<string> GetTestString()
    {
        return "Test String";
    }
}
