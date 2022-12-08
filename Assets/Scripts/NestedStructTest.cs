using System.Collections.Generic;
using SomeNamespace;
using Test;
using UnityEngine;

namespace Test
{
    public class NestedStructTest : MonoBehaviour
    {
        public class NestedClass 
        {
            public NestedClass()
            {
                PassingSelfManager.Pass(this);      
            }
        }
        
        private List<TestNestedStruct> nestedStructBuffer = new(); 
        
        public NestedStructTest()
        {
        
        }
        
        
        public NestedStructTest(string test, NestedStructTest other)
        {
        
        }
        
        [ContextMenu(nameof(Test))]
        private void Test()
        {
            new TestNestedStruct(this, "raw 1");
        }
        
        [ContextMenu(nameof(TestRootStruct))]
        private void TestRootStruct()
        {
            new RootStruct(this, "raw ");
        }
        
        [ContextMenu(nameof(TestNestedClass))] 
        private void TestNestedClass() 
        {
            new NestedClass();
        }
        
        public struct TestNestedStruct
        {
            public TestNestedStruct(NestedStructTest nestedStructTest, string test) {
                PassingSelfManager.Pass(this);   
            }
        }
    }
}

namespace SomeNamespace
{
    public struct RootStruct
    {
        public RootStruct(NestedStructTest nestedStructTest, string test) {
            PassingSelfManager.Pass(this);
        }
    }
}

