using UnityEngine;

namespace ExistingNamespaceTest
{
    public class SingletonAccessorTest : MonoBehaviour
    {
        public SingletonAccessorTest SelfRef; //self ref causes error when recompiled in same namespace
    
        [ContextMenu(nameof(PrintExistingSingletonValue))]
        void PrintExistingSingletonValue()
        {
            Debug.Log("t31");
        
            Debug.Log($"_intValue: {ExistingSingletonTest.Instance._intValue}");
            Debug.Log($"IntValueGetter: {ExistingSingletonTest.Instance.IntValueGetter}");
            Debug.Log($"IntValueGetterAdjusted: {ExistingSingletonTest.Instance.IntValueGetterAdjusted}");
            Debug.Log($"GetValuePlus1: {ExistingSingletonTest.Instance.GetValuePlus1()}");
        }

        [ContextMenu(nameof(Add5ToIntValueViaMethod))]
        void Add5ToIntValueViaMethod()
        {
            ExistingSingletonTest.Instance.AdjustIntValue(5);
        }
    
        [ContextMenu(nameof(Add10ToIntValueViaField))]
        void Add10ToIntValueViaField()
        {
            ExistingSingletonTest.Instance._intValue += 10;
        }

        // private void Update()
        // {
        //     Debug.Log("test4");
        // }
    }
}

