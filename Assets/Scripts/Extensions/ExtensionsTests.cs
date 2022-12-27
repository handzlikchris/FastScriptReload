using UnityEngine;

namespace Extensions
{
    public static class ExtensionsTests
    {
        public static void PrintTest(this ExtensionMethodTest t)
        {
            Debug.Log("ExtensionTest 3: " + t.TestVal);
        }
    }

    public static class TestExtensionObjExtensions
    {
        public static void Test(this TestExtensionObj t, string test)
        {
            Debug.Log("ExtensionTest:Test 12" + t + test);
            t.TestCall();
        }
    }
}