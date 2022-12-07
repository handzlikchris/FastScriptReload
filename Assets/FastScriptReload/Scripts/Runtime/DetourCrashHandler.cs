#if UNITY_EDITOR || LiveScriptReload_Enabled
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FastScriptReload.Runtime
{
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    public class DetourCrashHandler
    {
        public static string LastDetourFilePath;
    
        static DetourCrashHandler()
        {
            Init();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Init()
        {
            LastDetourFilePath = Path.GetTempPath() + Application.productName + "-last-detour.txt";
        }

        public static void LogDetour(string fullName)
        {
            //TODO: will that work on android?
            File.AppendAllText(LastDetourFilePath, fullName + Environment.NewLine);
        }

        public static string RetrieveLastDetour()
        {
            if (File.Exists(LastDetourFilePath))
            {
                var lines = File.ReadAllLines(LastDetourFilePath);
                return lines.Length > 0 ? lines.Last() : string.Empty;
            }

            return string.Empty;
        }

        public static void ClearDetourLog()
        {
            File.Delete(LastDetourFilePath);
        }
    }
}
#endif