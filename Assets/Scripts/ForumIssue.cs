using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//IWithGenMethod.cs
public interface IWithGenMethod
{
    public T Dummy<T>();
}

//WithGenTypeReturn.cs
public class WithGenTypeReturn 
{
    public List<IWithGenMethod> GetEmptyList()
    {
      // Editing here
        return new List<IWithGenMethod>(); 
    }
}