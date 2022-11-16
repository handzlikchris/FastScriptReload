using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestDynamicCompileChangeScale
{
    public void ChangeScale()
    {
        GameObject.Find("Graph").transform.localScale = Vector3.one * 2;
    }
}
