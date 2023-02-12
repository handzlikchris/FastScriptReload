using UnityEngine;

public class InheritanceTestParent : InheritanceTestChild
{
    [ContextMenu("Test")]
    void Test()
    {
        Debug.Log(_protectedField); 
        Debug.Log("test 123");
    }
}
