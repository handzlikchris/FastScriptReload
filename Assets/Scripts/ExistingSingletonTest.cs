using System.Collections;
using System.Collections.Generic;
using SomeNamespace;
using UnityEngine;

// namespace TestNew {

public class ExistingSingletonTest : MonoBehaviour
{
    [SerializeField] public int _intValue = 1;

    public int IntValueGetter => _intValue;

    public int IntValueGetterAdjusted 
    {
        get
        {
            return _intValue + 15;
        }
    }
    
    public static ExistingSingletonTest Instance;
    
    void Start()
    {
        Instance = this;
    }

    public int GetValuePlus1()
    {
       return _intValue + 1;
    }

    public void AdjustIntValue(int val)
    {
        _intValue = _intValue + val;
    }

    [ContextMenu(nameof(PrintIntValue))]
    void PrintIntValue()
    {
        Debug.Log("Changed");
        Debug.Log($"{_intValue}"); 
    }

    [ContextMenu(nameof(PrintOtherSingletonValue))]
    void PrintOtherSingletonValue()
    {
        Debug.Log($"PrintOtherSingletonValue1: {OtherSingletonTest.Instance._stringValue}"); 
    }
}
// }