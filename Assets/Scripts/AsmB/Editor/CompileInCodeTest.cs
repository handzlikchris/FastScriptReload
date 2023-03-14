using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using UnityEditor;
using UnityEngine;

public class CompileInCodeTest
{
//     [MenuItem("DEBUG/CompileTest")]
//     public static void Compile()
//     {
//         var code = $@"
// public class TestClass {{
//     public void Test() {{
//         UnityEngine.Debug.Log(""test"");
//     }}
// }}
// ";
//         
//         var metadataReferences = new[]
//         {
//             // typeof(object).GetTypeInfo().Assembly, // System.Private.CoreLib.dll
//             // typeof(Enumerable).GetTypeInfo().Assembly, // System.Linq.dll
//             // typeof(Console).GetTypeInfo().Assembly, // System.Console.dll
//             // Assembly.Load(new AssemblyName("System.Runtime")), // System.Runtime.dll
//             // Assembly.Load(new AssemblyName("Calculator")) // Calculator.dll
//             typeof(UnityEngine.Debug).GetTypeInfo().Assembly
//         }.Select(x => MetadataReference.CreateFromFile(x.Location)).ToList();
//
//         var compilationOptions = new CSharpCompilationOptions(OutputKind.ConsoleApplication).
//             WithMetadataImportOptions(MetadataImportOptions.All);
//         
//         var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
//         topLevelBinderFlagsProperty.SetValue(compilationOptions, (uint)1 << 22);
//         
//         var compilation = CSharpCompilation.Create("Test", new [] { CSharpSyntaxTree.ParseText(code) }, metadataReferences, compilationOptions);
//         
//         using (var ms = new MemoryStream())
//         {
//             var cr = compilation.Emit(ms);
//             ms.Seek(0, SeekOrigin.Begin);
//             var assembly = Assembly.Load(ms.ToArray());
//             assembly.EntryPoint.Invoke(null, new object[] { new string[0] });
//         }
    // }
}
