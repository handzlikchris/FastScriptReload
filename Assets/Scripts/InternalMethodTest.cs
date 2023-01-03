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
        //test
        Debug.Log("TestInternal test - changed?");
    }

    internal class InternalClass
    {
        public void Test()
        {
            Debug.Log("InternalClassTestInternal 2");
        }
    }
}
