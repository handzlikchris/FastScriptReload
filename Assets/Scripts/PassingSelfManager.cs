using System;
using SomeNamespace;
using UnityEngine;

public class PassingSelfManager
{
    public static void Pass(PassingSelfTest t, string other)
    {
        Debug.Log($"Passed: {t.Value} + other: {other}"); 
    }

    public static void Pass(Action<PassingSelfTest> tFn, PassingSelfTest t, string other)
    {
        tFn(t);
    }

    public static void Pass(CompilationTestClass t)
    {
        Debug.Log($"Passed CompilationTestClass: {t.name}");
    }
    
    public static void PassInterface(ICompilationTestClass t) 
    {
        Debug.Log($"Passed CompilationTestClass via interface: {t}"); 
    }
}