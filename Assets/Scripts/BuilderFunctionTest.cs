using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuilderFunctionTest : MonoBehaviour
{
    public BuilderFunctionTest()
    {
        
    }
    
    public BuilderFunctionTest Build()  
    { 
        return this; 
    }

    private void Update() 
    {
        
    }

    private void Start()
    {
        Debug.Log("Start");       
    }
}
