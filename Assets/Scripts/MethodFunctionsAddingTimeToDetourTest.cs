using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MethodFunctionsAddingTimeToDetourTest : MonoBehaviour
{
    private int someFieldInt = 0;
    private int SomeFieldIntClassMethod() => someFieldInt;
 
    private IEnumerator Coroutine1() { yield return null; }  
    private IEnumerator Coroutine2() { yield return null; }      
    private IEnumerator Coroutine3() { yield return null; }            
    private IEnumerator Coroutine4() { yield return null; }      
    
    // comment this whole method to reduce hot-reload time penalty
    private IEnumerator FooTestClassMethod() {
        while (true) {
            yield return null;
        }
    }
 
    private void OnScriptHotReload() {
        StopAllCoroutines();
 
        Func<int> AnonSomeFieldInt = () => someFieldInt;
 
        IEnumerator FooTest() {
            while (true) {
                yield return null;
                     SomeFieldIntClassMethod(); // <-- this doesn't add a 300ms penalty
                   AnonSomeFieldInt(); // comment to remove time penalty and the warning "this method doesn't exist in this class"  
            }
        }
    }
}
