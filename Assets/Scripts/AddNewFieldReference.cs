using System;
using UnityEngine;

public class AddNewFieldReference: MonoBehaviour
{
    public string TestString = "Test1";

    public void Test()
    {
        Debug.Log($"AddNewFieldReference: {TestString}");
    }
}