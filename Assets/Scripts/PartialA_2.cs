using UnityEngine;

public partial class PartialA
{
    public void Test()
    {
        Debug.Log($"PartialA: Test1: {VariableInOtherPartialFile}");
    }
}