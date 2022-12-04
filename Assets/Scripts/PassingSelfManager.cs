using UnityEngine;

public class PassingSelfManager
{
    public static void Pass(PassingSelfTest t)
    {
        Debug.Log($"Passed: {t.Value}"); 
    }
}