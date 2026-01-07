using System;
using UnityEngine;
using Object = System.Object;

namespace TH.Utils
{
    public static class Logg
    {
        [Flags]
        public enum LoggingMode
        {
            None       = 0,
            Default    = 1 << 0,
            Completed  = 1 << 1,
            InProgress = 1 << 2,
            Focussed   = 1 << 3,
            All        = Default | Completed | InProgress | Focussed
        }

        enum LogLevel
        {
            None,
            OnlyFocussing,
            OnlyInProgress,
            All,
        }
        
        private static readonly LoggingMode _enabled = CurrLogLevel switch
        {
            LogLevel.None           => LoggingMode.None,
            LogLevel.OnlyInProgress => LoggingMode.InProgress | LoggingMode.Focussed,
            LogLevel.OnlyFocussing  => LoggingMode.Focussed,
            LogLevel.All            => LoggingMode.All,
            _                       => LoggingMode.None
        };

        private const LogLevel CurrLogLevel = LogLevel.OnlyInProgress;

        private static bool ShouldLog(LoggingMode mode)
        {
            return (_enabled & mode) != 0; // Default도 동일 규칙: _enabled에 Default가 포함돼야 출력
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Log(object msg, LoggingMode mode = LoggingMode.Default, UnityEngine.Object context = null)
        {
            if (ShouldLog(mode)) Debug.Log(msg, context);
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogWarning(object msg, UnityEngine.Object context = null)
        {
            if (CurrLogLevel != LogLevel.None) Debug.LogWarning(msg, context);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogError(object msg, UnityEngine.Object context = null) => Debug.LogError(msg, context);
        
        // Extension Version
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Log(this object sender, object msg, LoggingMode mode = LoggingMode.Default)
        {
            if (msg is string stringMsg)
                msg = $"[{sender.GetType().Name}] {stringMsg}";
            Log(msg, mode);
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogWarning(this object sender, object msg, UnityEngine.Object context = null)
        {
            if (msg is string stringMsg)
                msg = $"[{sender.GetType().Name}] {stringMsg}";
            LogWarning(msg, context);
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogError(this object sender, object msg, UnityEngine.Object context = null)
        {
            if (msg is string stringMsg)
                msg = $"[{sender.GetType().Name}] {stringMsg}";
            LogError(msg, context);
        }
    }

}
