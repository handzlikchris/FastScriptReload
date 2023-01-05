using UnityEngine;

public class VirtualOverrideTest : BaseClassForVirtualCall
{
    void Update()
    {
        OverridableVirtual("-tst3");
    }
    
    protected override void OverridableVirtual(string s)
    {
        Debug.Log("Overriden Class-12" + s);
    }
}

public class BaseClassForVirtualCall : MonoBehaviour
{
    protected virtual void OverridableVirtual(string s)
    {
        Debug.Log("Base Class-" + s);
    }
}
