using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FastScriptReload.Runtime.Polyfills
{
    public static class Memory
    {
        public static void DetourMethod(MethodBase original, MethodBase target)
        {
            try 
            {
                // 1. Grab the Harmony assembly using a known PUBLIC class
                Assembly harmonyAssembly = typeof(HarmonyLib.Harmony).Assembly;
                
                // 2. Find the INTERNAL PatchTools class by its string name
                Type patchToolsType = harmonyAssembly.GetType("HarmonyLib.PatchTools");
                
                if (patchToolsType != null)
                {
                    // 3. Find the INTERNAL DetourMethod
                    MethodInfo detourMethod = patchToolsType.GetMethod(
                        "DetourMethod", 
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                    );

                    if (detourMethod != null)
                    {
                        // 4. Forcefully execute it
                        detourMethod.Invoke(null, new object[] { original, target });
                    }
                    else
                    {
                        Debug.LogError("FSR Polyfill Error: DetourMethod not found inside PatchTools.");
                    }
                }
                else
                {
                    Debug.LogError("FSR Polyfill Error: HarmonyLib.PatchTools type not found.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"FSR Polyfill Error: Failed to detour method. {e.Message}");
            }
        }
    }
}
