using System;
using System.Collections.Generic;
using System.Text;

namespace KitchenDesigner.Core
{
    public static class SceneScanLog
    {
        public const int MostCallersRemembered = 16;

        private readonly struct Scan
        {
            public readonly string Where;
            public readonly int Times;

            public Scan(string where, int times)
            {
                Where = where;
                Times = times;
            }

            public Scan OneMore() => new Scan(Where, Times + 1);
        }

        [ThreadStatic] private static List<Scan>? _scans;

        [ThreadStatic] private static int _callersBeyondTheLimit;

        public static void Note(string? member, string? file)
        {
            var scans = _scans ??= new List<Scan>(MostCallersRemembered);
            string where = Where(member, file);

            for (int i = 0; i < scans.Count; i++)
            {
                if (!string.Equals(scans[i].Where, where, System.StringComparison.Ordinal))
                    continue;
                scans[i] = scans[i].OneMore();
                return;
            }

            if (scans.Count >= MostCallersRemembered)
            {
                _callersBeyondTheLimit++;
                return;
            }
            scans.Add(new Scan(where, 1));
        }

        public static string Where(string? member, string? file) =>
            TypeNameOf(file) + "." + (string.IsNullOrEmpty(member) ? "?" : member!);

        public static string Take()
        {
            var scans = _scans;
            if ((scans == null || scans.Count == 0) && _callersBeyondTheLimit == 0)
                return string.Empty;

            var text = new StringBuilder();
            if (scans != null)
            {
                for (int i = 0; i < scans.Count; i++)
                {
                    if (i > 0) text.Append(", ");
                    text.Append(scans[i].Where);
                    if (scans[i].Times > 1) text.Append(" ×").Append(scans[i].Times);
                }
            }
            if (_callersBeyondTheLimit > 0)
                text.Append(", и ещё ").Append(_callersBeyondTheLimit).Append(" сверх предела");

            Forget();
            return text.ToString();
        }

        public static void Forget()
        {
            _scans?.Clear();
            _callersBeyondTheLimit = 0;
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
