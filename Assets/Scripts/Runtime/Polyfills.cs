using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace FastScriptReload.Runtime.Polyfills
{
    /// <summary>
    /// Due to my limited familiarity with the Harmony library, I’m unable to deeply modify the program logic.
    /// This example serves only to give a basic demo of running Harmony on macOS Apple Silicon.
    /// Note: The Harmony 2.2 and 2.3 APIs have some differences; you may need to adjust this code for your version.
    /// </summary>
    public static class Memory
    {
        public static void DetourMethod(MethodBase original, MethodBase target)
        {
            PatchTools.DetourMethod(original, target);
        }
    }
}
