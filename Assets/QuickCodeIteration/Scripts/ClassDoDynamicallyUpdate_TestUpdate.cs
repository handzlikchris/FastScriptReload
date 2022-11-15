//Just for testing, when working that'll be auto created
using UnityEngine;

public class ClassDoDynamicallyUpdate_TestUpdate
{
    void Update() //bool will be auto added, this is a prefix method
    {
        Debug.Log("Update normal - patched");

        // return false;
    }
}
