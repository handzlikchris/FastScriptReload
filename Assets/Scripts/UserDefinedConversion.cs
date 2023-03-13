using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UserDefinedConversion: MonoBehaviour
{
    public string Value = "Test 123";

    public string TestPropertyShorthand { get; set; }
    
    public string TestPropertyNormalGetOnly
    {
        get
        {
            return "Test";
        }
    }

    private string _testPropertyGetAndSet;
    public string TestPropertyGetAndSet 
    {
        get
        {
            return "Test";
        }
        set
        {  
            _testPropertyGetAndSet = value; 
        }
    }

    public UserDefinedConversion(string arg)  
    { 
        Debug.Log("ctor" + arg);      
    }

    public UserDefinedConversion() 
    { 
        Debug.Log("ctor");    
    }

    ~UserDefinedConversion()
    {
        Debug.Log("destructor");    
    }
    
    

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