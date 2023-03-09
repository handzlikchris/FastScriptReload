using System.Collections;
using System.Collections.Generic;
using UnityEngine;

interface IInternalInterface //no access modifier default to internal, not visible outside of asm
{
    void Test(string arg);
}
