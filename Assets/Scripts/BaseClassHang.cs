using UnityEngine;

public abstract class BaseClassHang : MonoBehaviour
{
    public void Call()
    {
        Debug.Log("Test Call Base Class 123");
        TestCallImpl();
    }

    protected abstract void TestCallImpl();
}