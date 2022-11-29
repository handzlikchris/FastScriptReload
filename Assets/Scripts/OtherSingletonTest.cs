using System.Collections;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;

namespace SomeNamespace
{
    public class OtherSingletonTest : MonoBehaviour
    {
        public enum NestedSingleton
        {
            First,
            Second
        }

        private class NestedClass
        {
            public static string Test = "123";
        }
    
        [SerializeField] public string _stringValue = "test 1 other singleton";

        private static OtherSingletonTest _instance;
        public static OtherSingletonTest Instance => _instance ?? (_instance = new OtherSingletonTest());

        [ContextMenu(nameof(PrintExistingSingletonValue))]
        void PrintExistingSingletonValue()
        {
            Debug.Log($"PrintExistingSingletonValue-c: {ExistingSingletonTest.Instance._intValue}"); 
        }

        [ContextMenu(nameof(Test))] 
        void Test()
        {
            var content = File.ReadAllText(@"E:\_src-unity\QuickCodeIteration\Assets\QuickCodeIteration\Examples\Scripts\FunctionLibrary.cs");   

            var tree = CSharpSyntaxTree.ParseText(content);
	
// tree.DumpSyntaxTree();
            var root = tree.GetRoot();
            var klass = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            var existingIdentifier = klass.ChildTokens().Where(k => k.RawKind == (int)SyntaxKind.IdentifierToken).First();

            var newIdentifier = SyntaxFactory.Identifier(existingIdentifier.Value + "__Patched_");
            var updateClass = klass.ReplaceToken(existingIdentifier, newIdentifier);
	
            var updatedRoot = root.ReplaceNode(klass, updateClass);
	
            var result = updatedRoot.ToFullString();
            Debug.Log(result);
        }

        private void Start()
        {
            StartCoroutine(TestCoroutine());
        }

        private void Update()
        {
            Debug.Log($"Test Nested Singleton: {NestedSingleton.First}"); 
            Debug.Log(NestedClass.Test);
            var t = new NestedClass();
        }

        private IEnumerator TestCoroutine()
        {
            while (true)
            {
                Debug.Log("Test coroutine 1");
                yield return null;
            }

        }
    }

}
