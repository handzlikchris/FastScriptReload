using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RecompileTest : MonoBehaviour
{
    [MenuItem("DEBUG/TestRecompile #F1")]
    static void TestRecompile_Remove()  //TODO: remove 
    {
        var result = DynamicAssemblyCompiler.Compile(new List<string>()
        {
            @"E:\_src-unity\QuickCodeIteration\Assets\Scripts\CompilationTestClass.cs",
            @"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Examples\Scripts\FunctionLibrary.cs"
        });
        
        Debug.Log(result.SourceCodeCombined);
    }
}
