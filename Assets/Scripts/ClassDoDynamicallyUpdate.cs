using UnityEngine;

public class ClassDoDynamicallyUpdate: BaseClass
{
    void Update()
    {
        Debug.Log("Testing - 2");
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

public class BaseClass : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.DrawCube(new Vector3(0, 3, 0), Vector3.one);
    }
}