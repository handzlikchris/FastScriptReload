using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TupleTest : MonoBehaviour
{
    public void Test()
    {
        List<Tuple<string, string>> ff = new List<Tuple<string, string>>();
        ff.Add(new Tuple<string, string>("a", "b"));
        //ff.Add(new Tuple<string, string>("c", "d"));
    }
}
