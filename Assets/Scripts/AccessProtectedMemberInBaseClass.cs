using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AccessProtectedMemberInBaseClass: AccessProtectedMemberInBaseClass_BaseClass
{
    void Test()
    {
        var t2 = TestInt + 10;    
    }
}