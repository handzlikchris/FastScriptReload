using UnityEngine;

public class BaseClassCallHangTest : BaseClassHang 
{
    void OnEnable()
    {
        Debug.Log($"BaseClassCallHangTest: enabled");
    }

    protected override void TestCallImpl()
    {
        Debug.Log("Test Impl - changed");
    }
}