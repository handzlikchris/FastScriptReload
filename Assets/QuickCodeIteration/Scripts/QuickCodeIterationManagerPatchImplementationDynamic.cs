using UnityEngine;
using HarmonyLib; 

class QuickCodeIterationManagerPatchImplementationDynamic
{
    [HarmonyPrefix]
    public static void Prefix(QuickCodeIterationManager __instance)
    {
        Debug.Log($"Start Prefix - dynamic {0} {nameof(QuickCodeIterationManagerPatchImplementationDynamic)}"); 
    }
}