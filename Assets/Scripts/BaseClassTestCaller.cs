using UnityEngine;

public class BaseClassTestCaller : MonoBehaviour
{
    [Tooltip("The projectile prefab")] public BaseClassHang BaseClassHangPrefab;


    [ContextMenu(nameof(Test))]
    void Test()
    {
        var o = Instantiate(BaseClassHangPrefab);
        o.Call();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
