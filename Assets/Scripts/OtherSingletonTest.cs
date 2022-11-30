using System.Collections;
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
