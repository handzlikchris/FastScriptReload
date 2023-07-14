using System;
using System.Collections.Generic;
using FastScriptReload.Editor.Compilation.CodeRewriting;
using FastScriptReload.Runtime;
using FastScriptReload.Scripts.Runtime;
using HarmonyLib;
using ImmersiveVRTools.Editor.Common.Utilities;
using UnityEditor;
using UnityEngine;

namespace FastScriptReload.Editor.NewFields
{
    [InitializeOnLoad]
    public class NewFieldsRendererDefaultEditorPatch
    {
        private static List<string> _cachedKeys = new List<string>();
        
        static NewFieldsRendererDefaultEditorPatch()
        {
            if ((bool)FastScriptReloadPreference.EnableExperimentalAddedFieldsSupport.GetEditorPersistedValueOrDefault())
            {
                var harmony = new Harmony(nameof(NewFieldsRendererDefaultEditorPatch));
            
                var renderAdditionalFieldsOnOptimizedGuiPostfix = AccessTools.Method(typeof(NewFieldsRendererDefaultEditorPatch), nameof(OnOptimizedInspectorGUI));
                var noCustomEditorOriginalRenderingMethdod =  AccessTools.Method("UnityEditor.GenericInspector:OnOptimizedInspectorGUI");
                harmony.Patch(noCustomEditorOriginalRenderingMethdod, postfix: new HarmonyMethod(renderAdditionalFieldsOnOptimizedGuiPostfix));
            
                var renderAdditionalFieldsDrawDefaultInspectorPostfix = AccessTools.Method(typeof(NewFieldsRendererDefaultEditorPatch), nameof(DrawDefaultInspector));
                var customEditorRenderingMethod = AccessTools.Method("UnityEditor.Editor:DrawDefaultInspector");
                harmony.Patch(customEditorRenderingMethod, postfix: new HarmonyMethod(renderAdditionalFieldsDrawDefaultInspectorPostfix)); 

#if ODIN_INSPECTOR
                // Odin Inspector support
                var renderAdditionalFieldsDrawOdinInspectorPostfix = AccessTools.Method(typeof(NewFieldsRendererDefaultEditorPatch), nameof(DrawOdinInspector));
                var customOdinEditorRenderingMethod = AccessTools.Method("Sirenix.OdinInspector.Editor.OdinEditor:DrawOdinInspector");
                harmony.Patch(customOdinEditorRenderingMethod, postfix: new HarmonyMethod(renderAdditionalFieldsDrawOdinInspectorPostfix));
#endif
            }
        }

        private static void OnOptimizedInspectorGUI(Rect contentRect, UnityEditor.Editor __instance)
        {
            RenderNewlyAddedFields(__instance);
        }
        
        private static void DrawDefaultInspector(UnityEditor.Editor __instance)
        {
            RenderNewlyAddedFields(__instance);
        }

        private static void RenderNewlyAddedFields(UnityEditor.Editor __instance)
        {
            //TODO: perf optimize, this will be used for many types, perhaps keep which types changed and just pass type?
            if (__instance.target)
            {
                if (TemporaryNewFieldValues.TryGetDynamicallyAddedFieldValues(__instance.target, out var addedFieldValues))
                {
                    EditorGUILayout.Space(10);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("[FSR] Dynamically Added Fields:");
                    GuiTooltipHelper.AddHelperTooltip("Fields were dynamically added for hot-reload, you can adjust their values and on full reload they'll disappear from this section and move back to main one.");
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(5);

                    try
                    {
                        _cachedKeys.AddRange(addedFieldValues.Keys); //otherwise collection changed exception can happen

                        var newFieldNameToGetTypeFn = CreateNewFieldInitMethodRewriter.ResolveNewFieldsToTypeFn(
                            AssemblyChangesLoader.Instance.GetRedirectedType(__instance.target.GetType())
                        );
                        
                        if(newFieldNameToGetTypeFn.Count == 0)
                            return;
                        
                        foreach (var addedFieldValueKey in _cachedKeys)
                        {
                            var newFieldType = (Type)newFieldNameToGetTypeFn[addedFieldValueKey]();

                            //rendering types come from UnityEditor.EditorGUI.DefaultPropertyField - that should handle all cases that editor can render
                            if (newFieldType == typeof(int)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.IntField(addedFieldValueKey, (int)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(bool)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.Toggle(addedFieldValueKey, (bool)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(float)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.FloatField(addedFieldValueKey, (float)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(string)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.TextField(addedFieldValueKey, (string)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(Color)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.ColorField(addedFieldValueKey, (Color)addedFieldValues[addedFieldValueKey]);
                            //TODO: SerializedPropertyType.LayerMask
                            else if (newFieldType == typeof(Enum)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.EnumPopup(addedFieldValueKey, (Enum)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(Vector2)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.Vector2Field(addedFieldValueKey, (Vector2)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(Vector3)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.Vector3Field(addedFieldValueKey, (Vector3)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(Vector4)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.Vector4Field(addedFieldValueKey, (Vector4)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(Rect)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.RectField(addedFieldValueKey, (Rect)addedFieldValues[addedFieldValueKey]);
                            //TODO: SerializedPropertyType.ArraySize
                            //TODO: SerializedPropertyType.Character
                            // else if (existingValueType == typeof(char)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.TextField(addedFieldValueKey, (char)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(AnimationCurve)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.CurveField(addedFieldValueKey, (AnimationCurve)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(Bounds)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.BoundsField(addedFieldValueKey, (Bounds)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(Gradient)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.GradientField(addedFieldValueKey, (Gradient)addedFieldValues[addedFieldValueKey]);
                            //TODO: SerializedPropertyType.FixedBufferSize
                            else if (newFieldType == typeof(Vector2Int)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.Vector2IntField(addedFieldValueKey, (Vector2Int)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(Vector3Int)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.Vector3IntField(addedFieldValueKey, (Vector3Int)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(RectInt)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.RectIntField(addedFieldValueKey, (RectInt)addedFieldValues[addedFieldValueKey]);
                            else if (newFieldType == typeof(BoundsInt)) addedFieldValues[addedFieldValueKey] = EditorGUILayout.BoundsIntField(addedFieldValueKey, (BoundsInt)addedFieldValues[addedFieldValueKey]);
                            //TODO: SerializedPropertyType.Hash128
                            else if (newFieldType == typeof(Quaternion))
                            {
                                //Quaternions are rendered as euler angles in editor
                                addedFieldValues[addedFieldValueKey] = Quaternion.Euler(EditorGUILayout.Vector3Field(addedFieldValueKey, ((Quaternion)addedFieldValues[addedFieldValueKey]).eulerAngles));
                            }
                            else if (typeof(UnityEngine.Object).IsAssignableFrom(newFieldType))
                            {
                                addedFieldValues[addedFieldValueKey] = EditorGUILayout.ObjectField(new GUIContent(addedFieldValueKey), (UnityEngine.Object)addedFieldValues[addedFieldValueKey], newFieldType, __instance.target);
                            }

                            else
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField($"{newFieldType.Name} - Unable to render");
                                GuiTooltipHelper.AddHelperTooltip(
                                    $"Unable to handle added-field rendering for type: {newFieldType.Name}, it won't be rendered. Best workaround is to not add this type dynamically in current version.");
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                    }
                    finally
                    {
                        _cachedKeys.Clear();
                    }
                }
            }
        }
    }
}