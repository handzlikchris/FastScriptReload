using UnityEngine;

public delegate bool CustomRootDelegate(string test);

public class ClassDoDynamicallyUpdate: MonoBehaviour
{
    public delegate bool CustomNestedDelegate(string test); 
    
    private CustomRootDelegate _customRootDelegate = CustomRootDelegateImpl;
    private CustomNestedDelegate _customNestedDelegate = CustomNestedDelegateImpl;

    private static bool CustomNestedDelegateImpl(string test)
    {
        Debug.Log($"Delegate nested: {test}");
        return true;
    }

    private static bool CustomRootDelegateImpl(string test)
    {
        Debug.Log($"Delegate root : {test}"); 
        return true;
    }

    void Update()
    {
        Debug.Log("Testing - 2");
        var result = _customRootDelegate("test param");
        _customNestedDelegate($"test 1, {result}");
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawCube(new Vector3(0, 6, 0), Vector3.one);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawCube(new Vector3(0, 2, 0), Vector3.one);
    }
}