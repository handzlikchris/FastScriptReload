using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewSwitchFormatTest : MonoBehaviour
{
    public NewSwitchFormatTest()
    {
        
    }

    static NewSwitchFormatTest()  
    {
        
    }
    
    public static void Step()
    {
        var foo = 0 switch { _ => 0 }; 
        Bar(); 
    }

    private static void Bar()  
    {
        var a = 5;
    }
}
