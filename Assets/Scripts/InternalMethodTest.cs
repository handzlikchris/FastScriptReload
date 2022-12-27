using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InternalMethodTest : MonoBehaviour
{
    void Update()
    {
        Test();
        new InternalClass().Test();
    }

    internal void Test()
    {
        Debug.Log("TestInternal 2");
    }

    internal class InternalClass
    {
        public void Test()
        {
            Debug.Log("InternalClassTestInternal 2");
        }
    }
}
