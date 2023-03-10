using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UserDefinedConversion: MonoBehaviour
{
    public string Value = "Test 123";
    
    public static implicit operator UserDefinedConversion(UserDefinedConversionOther other) { 
        return new UserDefinedConversion { Value = other.Value + "changed" }; 
    }

    void Update()
    {
        var other = new UserDefinedConversionOther { Value = "Other Test" };
        var converted = (UserDefinedConversion)other;
        Debug.Log("Converted2: " + converted.Value);
    }
}  

public class UserDefinedConversionOther  
{
    public string Value;
}