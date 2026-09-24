using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace KitchenDesigner.Core
{
    public static class SceneScanLog
    {
        public const int MostCallersRemembered = 16;

        internal const int MostPositionsRemembered = 64;

        [ThreadStatic] private static NamedTally? _frame;

        [ThreadStatic] private static int _timesNoted;

        [ThreadStatic] private static int _sharesNoted;

        private static long _scans;

        private static long _shares;

        private static readonly string[] _recent = new string[MostPositionsRemembered];

        public static int TimesNoted => _timesNoted;

        public static int SharesNoted => _sharesNoted;

        internal static long Position => Interlocked.Read(ref _scans);

        internal static long SharesPosition => Interlocked.Read(ref _shares);

        public static void NoteShare()
        {
            _sharesNoted++;
            NoteSharePosition();
        }

        internal static void NoteSharePosition() => Interlocked.Increment(ref _shares);

        public static void NoteWhere(string where)
        {
            _timesNoted++;
            (_frame ??= new NamedTally(MostCallersRemembered)).Add(where, 0);
            NotePosition(where);
        }

        internal static void NotePosition(string where)
        {
            long taken = Interlocked.Increment(ref _scans);
            _recent[(taken - 1) % MostPositionsRemembered] = where;
        }

        public static void Note(string? member, string? file) => NoteWhere(Where(member, file));

        public static string Where(string? member, string? file) =>
            TypeNameOf(file) + "." + (string.IsNullOrEmpty(member) ? "?" : member!);

        public static string Take()
        {
            var frame = _frame;
            if ((frame == null || frame.IsEmpty) && _sharesNoted == 0)
            {
                Forget();
                return string.Empty;
            }

            var text = new StringBuilder();
            if (frame != null)
                text.Append(frame.Format((name, _, times) =>
                    times > 1 ? name + " ×" + times : name));

            if (_sharesNoted > 0)
            {
                if (text.Length > 0) text.Append("; ");
                text.Append("и ").Append(_sharesNoted)
                    .Append(" раз список отдан без копии через PartRegistry.All — имён нет");
            }

            Forget();
            return text.ToString();
        }

        public static void Forget()
        {
            _frame?.Clear();
            _timesNoted = 0;
            _sharesNoted = 0;
        }

        internal static string SincePosition(long mark)
        {
            long now = Position;
            if (now <= mark) return string.Empty;

            long from = now - mark > MostPositionsRemembered ? now - MostPositionsRemembered : mark;
            var names = new List<string>();
            var times = new List<int>();
            for (long taken = from + 1; taken <= now; taken++)
            {
                string where = _recent[(taken - 1) % MostPositionsRemembered] ?? "?";
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

        private static string TypeNameOf(string? file)
        {
            if (string.IsNullOrEmpty(file)) return "?";

            int start = 0;
            for (int i = file!.Length - 1; i >= 0; i--)
            {
                if (file[i] != '/' && file[i] != '\\') continue;
                start = i + 1;
                break;
            }

            int dot = file.IndexOf('.', start);
            int end = dot < 0 ? file.Length : dot;
            return end > start ? file.Substring(start, end - start) : "?";
        }
    }
}
