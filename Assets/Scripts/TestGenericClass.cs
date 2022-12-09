using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestGenericClass : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
         
    }

    void TestGeneric<T>()
    {
        Debug.Log("Generic 1112" + typeof(T));
    }

    // Update is called once per frame
    void Update()
    {
        var t = new TestGenericClassImpl<object>();
        t.Test((object)t);
        
        var num = new TestGenericClassImpl<int>(); 
        num.Test((int)10);

        TestGeneric<bool>(); 
        
        var i = new TestGenericBaseImpl();
        i.Test();
        i.BaseTest(12);
    }
}

public class TestGenericClassImpl<T>
{
    private T _val;
    
    public void Test(T obj) 
    {
        _val = obj;
        Debug.Log("TestGenericClassImpl: 1" + typeof(T) + obj.GetHashCode()); 
    }
}

public class TestGenericBaseImpl : TestGenericBase<int>
{
    public void Test() 
    {
        Debug.Log("TestGenericBaseImpl: 1 change2"); 
    }
}

public class TestGenericBase<T>
{
    private T _val;
    
    public void BaseTest(T obj) 
    {
        _val = obj;
        Debug.Log("TestGenericBase: 12" + typeof(T) + obj.GetHashCode()); 
    }
}