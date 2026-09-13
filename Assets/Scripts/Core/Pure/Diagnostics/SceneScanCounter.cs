using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace KitchenDesigner.Core
{
    public static class SceneScanCounter
    {
        public const int MostScansRemembered = 64;

        private static long _scans;

        private static readonly string[] _recent = new string[MostScansRemembered];

        private static long _shares;

        public static long Scans => Interlocked.Read(ref _scans);

        public static long Shares => Interlocked.Read(ref _shares);

        public static void NoteShare() => Interlocked.Increment(ref _shares);

        public static void Note(string where)
        {
            long taken = Interlocked.Increment(ref _scans);
            _recent[(taken - 1) % MostScansRemembered] = where;
        }

        public static string Since(long mark)
        {
            long now = Scans;
            if (now <= mark) return string.Empty;

            long from = now - mark > MostScansRemembered ? now - MostScansRemembered : mark;
            var names = new List<string>();
            var times = new List<int>();
            for (long taken = from + 1; taken <= now; taken++)
            {
                string where = _recent[(taken - 1) % MostScansRemembered] ?? "?";
                int at = names.IndexOf(where);
                if (at >= 0) times[at]++;
                else { names.Add(where); times.Add(1); }
            }

            var text = new StringBuilder();
            if (from > mark)
                text.Append("старше предела: ").Append(from - mark).Append(", ");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(names[i]);
                if (times[i] > 1) text.Append('×').Append(times[i]);
            }
            return text.ToString();
        }
    }
}
