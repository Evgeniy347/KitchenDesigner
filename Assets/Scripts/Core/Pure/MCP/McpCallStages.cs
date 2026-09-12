using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace KitchenDesigner.Core.MCP
{
    public static class McpCallStages
    {
        public const int MostStagesRemembered = 16;

        private readonly struct Stage
        {
            public readonly string Name;
            public readonly long Ticks;
            public readonly int Times;

            public Stage(string name, long ticks, int times)
            {
                Name = name;
                Ticks = ticks;
                Times = times;
            }

            public Stage Plus(long ticks) => new Stage(Name, Ticks + ticks, Times + 1);
        }

        [ThreadStatic] private static List<Stage>? _stages;

        [ThreadStatic] private static int _stagesBeyondTheLimit;

        public static long Begin() => Stopwatch.GetTimestamp();

        public static void End(string name, long beganAt) =>
            Add(name, Stopwatch.GetTimestamp() - beganAt);

        public static void Add(string name, long ticks)
        {
            if (string.IsNullOrEmpty(name)) return;
            var stages = _stages ??= new List<Stage>(MostStagesRemembered);

            for (int i = 0; i < stages.Count; i++)
            {
                if (!string.Equals(stages[i].Name, name, StringComparison.Ordinal)) continue;
                stages[i] = stages[i].Plus(ticks);
                return;
            }

            if (stages.Count >= MostStagesRemembered)
            {
                _stagesBeyondTheLimit++;
                return;
            }
            stages.Add(new Stage(name, ticks, 1));
        }

        public static bool TryGet(string name, out double ms, out int times)
        {
            ms = 0;
            times = 0;
            var stages = _stages;
            if (stages == null) return false;

            for (int i = 0; i < stages.Count; i++)
            {
                if (!string.Equals(stages[i].Name, name, StringComparison.Ordinal)) continue;
                ms = MsOf(stages[i].Ticks);
                times = stages[i].Times;
                return true;
            }
            return false;
        }

        public static string Take()
        {
            var stages = _stages;
            if ((stages == null || stages.Count == 0) && _stagesBeyondTheLimit == 0)
                return string.Empty;

            var text = new StringBuilder();
            if (stages != null)
            {
                for (int i = 0; i < stages.Count; i++)
                {
                    if (i > 0) text.Append(", ");
                    text.Append(stages[i].Name).Append(' ')
                        .Append(MsOf(stages[i].Ticks).ToString("F2")).Append("ms");
                    if (stages[i].Times > 1) text.Append('×').Append(stages[i].Times);
                }
            }
            if (_stagesBeyondTheLimit > 0)
                text.Append(", и ещё ").Append(_stagesBeyondTheLimit).Append(" сверх предела");

            Forget();
            return text.ToString();
        }

        public static void Forget()
        {
            _stages?.Clear();
            _stagesBeyondTheLimit = 0;
        }

        private static double MsOf(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }
}
