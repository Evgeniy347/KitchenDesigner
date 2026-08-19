using System;
using System.Globalization;

namespace KitchenDesigner.Core.Update
{
    /// <summary>
    /// Разбор и сравнение версий вида «0.N» / «v0.N». Версии приложения
    /// (BuildInfo.Version) и GitHub-теги релизов (v0.N) приводятся к одному виду,
    /// чтобы десктоп мог честно решить, есть ли обновление. Никакого I/O — чистые функции,
    /// целиком покрыты тестами.
    /// </summary>
    public static class VersionUtil
    {
        /// <summary>
        /// Разбирает «MAJOR.MINOR.BUILD» (ведущий «v»/«V» и обрамляющие пробелы
        /// игнорируются; недостающие части = 0; нецифровые хвосты обрезаются).
        /// Возвращает false, если в строке нет ни одной цифры (например, пустая или
        /// мусор) — вызывающий трактует это как ошибку ответа сервера.
        /// </summary>
        public static bool TryParse(string raw, out int major, out int minor, out int build)
        {
            major = minor = build = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var s = raw.Trim();
            if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s.Substring(1);

            var parts = s.Split('.');
            int[] vals = new int[3];
            bool anyDigit = false;
            for (int i = 0; i < parts.Length && i < 3; i++)
            {
                if (TryLeadingInt(parts[i], out int v))
                {
                    vals[i] = v;
                    anyDigit = true;
                }
                else if (parts[i].Length > 0)
                {
                    // Первая же нечисловая часть — не версия.
                    if (!anyDigit) return false;
                    break;
                }
            }
            if (!anyDigit) return false;

            major = vals[0]; minor = vals[1]; build = vals[2];
            return true;
        }

        /// <summary>-1 если a &lt; b, 0 если равны, 1 если a &gt; b. Нераспознанные
        /// строки считаются равными (0), чтобы не показывать ложное «есть обновление».</summary>
        public static int Compare(string a, string b)
        {
            TryParse(a, out int am, out int an, out int ab);
            TryParse(b, out int bm, out int bn, out int bb);
            if (am != bm) return am < bm ? -1 : 1;
            if (an != bn) return an < bn ? -1 : 1;
            if (ab != bb) return ab < bb ? -1 : 1;
            return 0;
        }

        private static bool TryLeadingInt(string s, out int value)
        {
            value = 0;
            int i = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            {
                value = value * 10 + (s[i] - '0');
                i++;
            }
            return i > 0;
        }
    }
}
