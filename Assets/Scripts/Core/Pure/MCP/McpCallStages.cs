using System;
using System.Diagnostics;

namespace KitchenDesigner.Core.MCP
{
    public static class McpCallStages
    {
        public const int MostStagesRemembered = 16;

        [ThreadStatic] private static NamedTally? _stages;

        public static long Begin() => Stopwatch.GetTimestamp();

        public static void End(string name, long beganAt) =>
            Add(name, Stopwatch.GetTimestamp() - beganAt);

        public static void Add(string name, long ticks)
        {
            if (string.IsNullOrEmpty(name)) return;
            (_stages ??= new NamedTally(MostStagesRemembered)).Add(name, ticks);
        }

        public static bool TryGet(string name, out double ms, out int times)
        {
            ms = 0;
            times = 0;
            var stages = _stages;
            if (stages == null || !stages.TryGet(name, out long ticks, out times)) return false;
            ms = MsOf(ticks);
            return true;
        }

        public static string Take()
        {
            var stages = _stages;
            if (stages == null || stages.IsEmpty) return string.Empty;

            string text = stages.Format((name, ticks, times) =>
                times > 1
                    ? name + " " + MsOf(ticks).ToString("F2") + "ms×" + times
                    : name + " " + MsOf(ticks).ToString("F2") + "ms");

            Forget();
            return text;
        }

        public static void Forget() => _stages?.Clear();

        private static double MsOf(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }
}
