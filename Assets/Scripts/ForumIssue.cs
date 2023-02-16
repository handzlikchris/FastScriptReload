using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// SoundMgr.cs
public class SoundMgr : Test.Singleton<SoundMgr>
{
} 

// Singleton.cs
namespace Test
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T sInst;

        public static T Inst
        {
            get
            {
                if (sInst == null)
                    sInst = FindObjectOfType(typeof(T)) as T;
                return sInst; 
            }
        }
    }
}
