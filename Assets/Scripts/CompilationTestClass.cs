using SomeNamespace;
using UnityEngine;

namespace SomeNamespace
{
    public delegate CompilationTestClass RootDelegate(int test, CompilationTestClass test1);
    
    public class CompilationTestClass : MonoBehaviour
    {
        public class NestedClass
        {
            public string Test { get; set; }
        }
        
        public delegate CompilationTestClass NestedDelegate(int test, CompilationTestClass test1);
        
        private CompilationTestClass _selfReference;
        public RootDelegate _rootDelegate;
        public NestedDelegate _nestedDelegate;
        public NestedClass _NestedClass;
        
        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
        
        public enum NestedEnum
        {
            Value1,
            Value2
        }
    }

    public class AnotherCompilationTestClass : MonoBehaviour
    {
         
    }
    
    public enum OuterEnum
    {
        Value11,
        Value21
    }

    public struct StructInNamespace
    {
        public string Test { get; set; }
        public CompilationTestClass CompilationTestClass { get; set; }
    }
}

public struct StructNoNamespace
{
    public string Test { get; set; }
    public RootDelegate _rootDelegate;
    public StructInNamespace StructInNamespace { get; set; }
}
