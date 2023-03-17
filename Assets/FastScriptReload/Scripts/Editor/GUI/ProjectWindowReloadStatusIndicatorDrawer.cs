using UnityEditor;
using UnityEngine;

namespace FastScriptReload.Editor.GUI
{
    [InitializeOnLoad]
    public class ProjectWindowReloadStatusIndicatorDrawer
    {
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
            
            if (FastScriptReloadManager.Instance.LastProcessedDynamicFileHotReloadStates.TryGetValue(guid, out var fileHotReloadState))
            {
                var sideLineRect = new Rect(1, rect.y, 2, rect.height);
                if (fileHotReloadState.IsFailed)
                {
                    EditorGUI.DrawRect(sideLineRect, Color.red);
                }    
                else if (fileHotReloadState.IsChangeHotSwapped)
                {
                    EditorGUI.DrawRect(sideLineRect, Color.green);
                }
            }
        }
    }
}