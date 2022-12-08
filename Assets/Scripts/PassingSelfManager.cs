using System;
using SomeNamespace;
using Test;
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

    public static void PassStruct(NestedStructTest.TestNestedStruct s)
    {
        Debug.Log($"Passed {s}"); 
    }
    
    public static void Pass(RootStruct s)
    {
        Debug.Log($"Passed {s}"); 
    }
    
    public static void Pass(NestedStructTest.NestedClass s)
    {
        Debug.Log($"Passed {s}"); 
    }
}