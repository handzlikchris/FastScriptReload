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
            if(!ManualOverridesFolder.Exists)
                ManualOverridesFolder.Create();

            var overridenFile = new FileInfo(Path.Combine(ManualOverridesFolder.FullName, script.name + ".cs"));
            if (!overridenFile.Exists)
                overridenFile.Create();
            
            //TODO: write a template in on how to change
            InternalEditorUtility.OpenFileAtLineExternal(overridenFile.FullName, 0);
        }
    }
}