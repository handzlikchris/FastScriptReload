using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace FastScriptReload.Editor.Compilation.ScriptGenerationOverrides
{
    [InitializeOnLoad]
    public static class ScriptGenerationOverridesManager
    {
        public static DirectoryInfo ManualOverridesFolder { get; }

        static ScriptGenerationOverridesManager()
        {
            //TODO: allow to customize later from code, eg for user that'd like to include in source control
            ManualOverridesFolder = new DirectoryInfo(Application.persistentDataPath + @"FastScriptReload\ScriptOverrides");
        }

        public static void AddScriptOverride(MonoScript script)
        {
            EnsureOverrideFolderExists();

            var overridenFile = new FileInfo(Path.Combine(ManualOverridesFolder.FullName, script.name + ".cs"));
            if (!overridenFile.Exists)
                overridenFile.Create();
            
            //TODO: write a template in on how to change
            InternalEditorUtility.OpenFileAtLineExternal(overridenFile.FullName, 0);
        }
        
        public static bool TryRemoveScriptOverride(MonoScript script)
        {
            EnsureOverrideFolderExists();

            var overridenFile = new FileInfo(Path.Combine(ManualOverridesFolder.FullName, script.name + ".cs"));
            if (overridenFile.Exists)
            {
                try
                {
                    overridenFile.Delete();
                }
                catch (Exception e)
                {
                    Debug.Log($"Unable to remove: '{overridenFile.Name}' - make sure it's not locked / open in editor");
                }

                return true;
            }

            return false;
        }

        public static bool TryGetScriptOverride(FileInfo changedFile, out FileInfo overridesFile)
        {
            //TODO: PERF: could cache?
            overridesFile = new FileInfo(Path.Combine(ManualOverridesFolder.FullName, changedFile.Name));

            return overridesFile.Exists;
        }
        
        private static void EnsureOverrideFolderExists()
        {
            if (!ManualOverridesFolder.Exists)
                ManualOverridesFolder.Create();
        }
    }
}