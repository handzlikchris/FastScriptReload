using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenericsTest : MonoBehaviour
{
    public void Test<T>(T arg) where T: IGenericsTest
    {
        Debug.Log("test generics 123" + arg.GetType());  
    }

    // Update is called once per frame
    void Update() 
    { 
        Test(new GenericsTest1());       
        Test(new GenericsTest2());      
    }
}

public interface IGenericsTest
{
    
}

public class GenericsTest1 : IGenericsTest
{
    
}

public class GenericsTest2 : IGenericsTest
{
    
}