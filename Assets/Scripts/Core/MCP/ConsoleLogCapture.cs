using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    public static class ConsoleLogCapture
    {
        private static readonly List<ConsoleLogEntry> _logs = new List<ConsoleLogEntry>();
        private const int MaxLogs = 1000;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Application.logMessageReceived += OnLogMessage;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            lock (_logs)
            {
                _logs.Add(new ConsoleLogEntry
                {
                    type = type.ToString(),
                    message = message,
                    stackTrace = stackTrace
                });
                while (_logs.Count > MaxLogs)
                    _logs.RemoveAt(0);
            }
        }

        public static List<ConsoleLogEntry> GetRecent(int count)
        {
            Initialize();
            lock (_logs)
            {
                if (_logs.Count <= count)
                    return new List<ConsoleLogEntry>(_logs);

                return _logs.GetRange(_logs.Count - count, count);
            }
        }

        public static void Clear()
        {
            lock (_logs) { _logs.Clear(); }
        }
    }
}
