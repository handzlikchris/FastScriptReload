using HarmonyLib;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class QuickCodeIterationManager : MonoBehaviour
{
    static QuickCodeIterationManager()
    {
        var harmony = new Harmony("QuickCodeIterationManager");
        harmony.PatchAll();
    }
    
    void Start()
    {
        Debug.Log("Start normal");
    }

    [HarmonyPatch(typeof(QuickCodeIterationManager))]
    [HarmonyPatch("Start")]
    class QuickCodeIterationManagerPatchImplementation
    {
        [HarmonyPrefix]
        public static void Prefix(QuickCodeIterationManager __instance)
        {
            Debug.Log($"Start Prefix");
        }
    }
}
