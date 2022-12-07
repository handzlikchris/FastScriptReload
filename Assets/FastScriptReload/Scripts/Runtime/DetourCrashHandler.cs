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
        //TODO: add device support / android crashes / how to report issues back?
        public static string LastDetourFilePath;
    
        static DetourCrashHandler()
        {
#if UNITY_EDITOR
            Init();
#else
            Debug.Log($"{nameof(DetourCrashHandler)}: currently only supported in Editor");
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Init()
        {
#if UNITY_EDITOR
            LastDetourFilePath = Path.GetTempPath() + Application.productName + "-last-detour.txt";
#else
            Debug.Log($"{nameof(DetourCrashHandler)}: currently only supported in Editor");
#endif
        }

        public static void LogDetour(string fullName)
        {
#if UNITY_EDITOR
            File.AppendAllText(LastDetourFilePath, fullName + Environment.NewLine);
#else
            Debug.Log($"{nameof(DetourCrashHandler)}: currently only supported in Editor");
#endif
        }

        public static string RetrieveLastDetour()
        {
#if UNITY_EDITOR
            if (File.Exists(LastDetourFilePath))
            {
                var lines = File.ReadAllLines(LastDetourFilePath);
                return lines.Length > 0 ? lines.Last() : string.Empty;
            }

            return string.Empty;
#else
            Debug.Log($"{nameof(DetourCrashHandler)}: currently only supported in Editor");
            return string.Empty;
#endif
        }

        public static void ClearDetourLog()
        {
#if UNITY_EDITOR
            File.Delete(LastDetourFilePath);
#else
            Debug.Log($"{nameof(DetourCrashHandler)}: currently only supported in Editor");
#endif
        }
    }
}
#endif