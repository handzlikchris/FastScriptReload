using System.Linq;
using FastScriptReload.Scripts.Runtime;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace FastScriptReload.Editor.NewFields
{
    [InitializeOnLoad]
    public class NewFieldsRendererDefaultEditorPatch
    {
        static NewFieldsRendererDefaultEditorPatch()
        {
            var harmony = new Harmony(nameof(NewFieldsRendererDefaultEditorPatch));

            // var original =  AccessTools.Method("UnityEditor.GameObjectInspector:DrawInspector");
            // var prefix = AccessTools.Method(typeof(NewFieldsRendererDefaultEditorPatch), nameof(DrawDefaultInspectorPrefix));
            //
            // harmony.Patch(original, prefix: new HarmonyMethod(prefix));
            
            var original =  AccessTools.Method("UnityEditor.GenericInspector:OnOptimizedInspectorGUI");
            var prefix = AccessTools.Method(typeof(NewFieldsRendererDefaultEditorPatch), nameof(OnOptimizedInspectorGUI));
            
            harmony.Patch(original, postfix: new HarmonyMethod(prefix));
        }

        private static void OnOptimizedInspectorGUI(Rect contentRect, UnityEditor.Editor __instance)
        {
            //TODO: perf optimize, this will be used for many types, perhaps keep which types changed and just pass type?
            if (__instance.target)
            {
                // EditorGUILayout.Space(10);
                // EditorGUILayout.LabelField("FSR: Dynamically Added Fields:");
                // EditorGUILayout.TextField("Test", "123");
                
                if (TemporaryNewFieldValues.TryGetDynamicallyAddedFieldValues(__instance.target, out var addedFieldValues))
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("FSR: Dynamically Added Fields:");
                    
                    foreach (var addedFieldValueKv in addedFieldValues
                                 // .Where(kv => kv.Key == "newString")
                             )
                    {
                        // EditorGUILayout.TextField("Test", "123");
                        addedFieldValues[addedFieldValueKv.Key] = EditorGUILayout.TextField(addedFieldValueKv.Key, addedFieldValueKv.Value.ToString());
                    }
                }
            }
        }
    }
}