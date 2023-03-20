using System.Collections.Generic;
using ImmersiveVRTools.Editor.Common.Utilities;
using UnityEditor;
using UnityEngine;

namespace FastScriptReload.Editor.GUI
{
    [InitializeOnLoad]
    public class ProjectWindowReloadStatusIndicatorDrawer
    {
        private static Texture HelpTexture;
        
        private static bool IsEnabled;
        
        
        static ProjectWindowReloadStatusIndicatorDrawer()
        {
            EditorApplication.projectWindowItemOnGUI += ProjectWindowItemOnGUI;
            EditorApplication.update += () =>
            {
                IsEnabled = (bool)FastScriptReloadPreference.IsVisualHotReloadIndicationShownInProjectWindow.GetEditorPersistedValueOrDefault();
            };
        }

        private static void ProjectWindowItemOnGUI(string guid, Rect rect)
        {
            if (!IsEnabled)
            {
                return;
            }
            
            if (HelpTexture == null)
            {
                HelpTexture = EditorGUIUtility.TrIconContent("_Help").image;
            }

            if (FastScriptReloadManager.Instance.LastProcessedDynamicFileHotReloadStatesInSession.TryGetValue(guid, out var fileHotReloadState))
            {
                var sideLineRect = new Rect(1, rect.y, 2, rect.height);
                if (fileHotReloadState.IsFailed)
                {
                    EditorGUI.DrawRect(sideLineRect, Color.red);
                    
                    var sideInfoRect = new Rect(1, rect.y - 3, 20, 20);
                    if (UnityEngine.GUI.Button(sideInfoRect,
                            new GUIContent(new GUIContent(HelpTexture, 
@"Fast Script Reload - Error

Last hot reload failed.

Please click for more details.")),
                            EditorStyles.linkLabel))
                    {
                        FastScriptReloadWelcomeScreen.Init().OpenInspectError(fileHotReloadState);
                    }; 
                }    
                else if (fileHotReloadState.IsChangeHotSwapped)
                {
                    EditorGUI.DrawRect(sideLineRect, Color.green);
                }
            }
        }
    }
}