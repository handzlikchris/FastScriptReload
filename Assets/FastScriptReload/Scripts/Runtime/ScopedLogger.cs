using UnityEngine;

namespace FastScriptReload.Runtime
{
    //TODO: move to common and just set LogPrefix as needed
    public static class ScopedLogger
    {
        public static string LogPrefix = "FSR: ";

        public static void LogDebug(string message)
        {
#if FastScriptReload_DebugEnabled
            Debug.Log($"<color=#ABABAB>{LogPrefix}{message}</color>");    
#endif
        }

        public static void Log(string message) => Debug.Log(LogPrefix + message);
    }
}