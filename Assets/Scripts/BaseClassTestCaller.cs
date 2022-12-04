using UnityEngine;

public class BaseClassTestCaller : MonoBehaviour
{
    public BaseClassHang BaseClassHang;


    [ContextMenu(nameof(Test))]
    void Test()
    {
        // var o = Instantiate(BaseClassHangPrefab);
        BaseClassHang.Call();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
