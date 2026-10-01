using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KitchenDesigner.Core
{
    public static class BidiLine
    {
        public enum Kind : byte { L, R, AL, EN, AN, ES, ET, CS, NSM, WS, ON }

        private static readonly IReadOnlyDictionary<char, char> Mirrors = new Dictionary<char, char>
        {
            ['('] = ')', [')'] = '(', ['['] = ']', [']'] = '[', ['{'] = '}', ['}'] = '{',
            ['<'] = '>', ['>'] = '<', ['«'] = '»', ['»'] = '«',
            ['‹'] = '›', ['›'] = '‹',
        };

        public static Kind Classify(char c)
        {
            if (c >= '0' && c <= '9') return Kind.EN;
            if (c >= '۰' && c <= '۹') return Kind.EN;
            if ((c >= '٠' && c <= '٩') || c == '٫' || c == '٬') return Kind.AN;
            if (c == '+' || c == '-' || c == '−') return Kind.ES;
            if (c == ',' || c == '.' || c == ':' || c == '/' || c == ' ' || c == '،') return Kind.CS;
            if (c == '#' || c == '$' || c == '%' || c == '°' || c == '±' || c == '‰'
                || c == '€' || c == '₽' || c == '£' || c == '¥') return Kind.ET;
            if (c == '‎') return Kind.L;
            if (c == '‏') return Kind.R;
            if (char.IsWhiteSpace(c)) return Kind.WS;
            var category = char.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.EnclosingMark) return Kind.NSM;
            if (c >= '֐' && c <= '׿') return Kind.R;
            if (c >= 'יִ' && c <= 'ﭏ') return Kind.R;
            if ((c >= '؀' && c <= 'ࣿ') || (c >= 'ﭐ' && c <= '﷿') || (c >= 'ﹰ' && c <= 'ﻼ'))
                return Kind.AL;
            if (char.IsLetter(c) || char.IsDigit(c) || char.IsSurrogate(c) || category == UnicodeCategory.SpacingCombiningMark)
                return Kind.L;
            return Kind.ON;
        }

        public static bool? FirstStrongIsRightToLeft(string text)
        {
            foreach (char c in text)
            {
                var kind = Classify(c);
                if (kind == Kind.L) return false;
                if (kind == Kind.R || kind == Kind.AL) return true;
            }
            return null;
        }

        public static string ForRightToLeftLayout(string line, bool rightToLeftBase)
        {
            var visual = Visual(line, rightToLeftBase);
            var units = Units(visual);
            units.Reverse();
            return string.Concat(units);
        }

        public static string Visual(string line, bool rightToLeftBase)
        {
            var units = Units(line);
            if (units.Count == 0) return line;
            var levels = ResolveLevels(units, rightToLeftBase);
            var order = new int[units.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Reorder(order, levels);

            var visual = new StringBuilder(line.Length);
            foreach (int index in order)
            {
                var unit = units[index];
                if ((levels[index] & 1) == 1 && unit.Length == 1 && Mirrors.TryGetValue(unit[0], out var mirrored))
                    visual.Append(mirrored);
                else
                    visual.Append(unit);
            }
            return visual.ToString();
        }

        private static List<string> Units(string text)
        {
            var units = new List<string>(text.Length);
            var elements = StringInfo.GetTextElementEnumerator(text);
            while (elements.MoveNext()) units.Add((string)elements.Current);
            return units;
        }

        private static int[] ResolveLevels(List<string> units, bool rightToLeftBase)
        {
            int n = units.Count;
            var kinds = new Kind[n];
            for (int i = 0; i < n; i++) kinds[i] = Classify(units[i][0]);
            var baseStrong = rightToLeftBase ? Kind.R : Kind.L;

            ResolveWeak(kinds, baseStrong);
            ResolveNeutrals(kinds, baseStrong);

            int baseLevel = rightToLeftBase ? 1 : 0;
            var levels = new int[n];
            for (int i = 0; i < n; i++) levels[i] = ImplicitLevel(kinds[i], baseLevel);
            for (int i = n - 1; i >= 0 && Classify(units[i][0]) == Kind.WS; i--) levels[i] = baseLevel;
            return levels;
        }

        private static void ResolveWeak(Kind[] kinds, Kind baseStrong)
        {
            int n = kinds.Length;
            for (int i = 0; i < n; i++)
                if (kinds[i] == Kind.NSM) kinds[i] = i == 0 ? Kind.ON : kinds[i - 1];

            var lastStrong = baseStrong;
            for (int i = 0; i < n; i++)
            {
                if (kinds[i] == Kind.L || kinds[i] == Kind.R || kinds[i] == Kind.AL) lastStrong = kinds[i];
                else if (kinds[i] == Kind.EN && lastStrong == Kind.AL) kinds[i] = Kind.AN;
            }
            for (int i = 0; i < n; i++)
                if (kinds[i] == Kind.AL) kinds[i] = Kind.R;

            for (int i = 1; i < n - 1; i++)
            {
                var before = kinds[i - 1];
                var after = kinds[i + 1];
                if (kinds[i] == Kind.ES && before == Kind.EN && after == Kind.EN) kinds[i] = Kind.EN;
                else if (kinds[i] == Kind.CS && before == after && (before == Kind.EN || before == Kind.AN)) kinds[i] = before;
            }

            for (int i = 0; i < n; i++)
            {
                if (kinds[i] != Kind.ET) continue;
                int end = i;
                while (end < n && kinds[end] == Kind.ET) end++;
                bool touchesNumber = (i > 0 && kinds[i - 1] == Kind.EN) || (end < n && kinds[end] == Kind.EN);
                if (touchesNumber)
                    for (int j = i; j < end; j++) kinds[j] = Kind.EN;
                i = end - 1;
            }

            for (int i = 0; i < n; i++)
                if (kinds[i] == Kind.ES || kinds[i] == Kind.ET || kinds[i] == Kind.CS) kinds[i] = Kind.ON;

            lastStrong = baseStrong;
            for (int i = 0; i < n; i++)
            {
                if (kinds[i] == Kind.L || kinds[i] == Kind.R) lastStrong = kinds[i];
                else if (kinds[i] == Kind.EN && lastStrong == Kind.L) kinds[i] = Kind.L;
            }
        }

        private static void ResolveNeutrals(Kind[] kinds, Kind baseStrong)
        {
            int n = kinds.Length;
            for (int i = 0; i < n; i++)
            {
                if (!IsNeutral(kinds[i])) continue;
                int end = i;
                while (end < n && IsNeutral(kinds[end])) end++;
                var before = i == 0 ? baseStrong : StrongDirection(kinds[i - 1]);
                var after = end == n ? baseStrong : StrongDirection(kinds[end]);
                var resolved = before == after ? before : baseStrong;
                for (int j = i; j < end; j++) kinds[j] = resolved;
                i = end - 1;
            }
        }

        private static bool IsNeutral(Kind kind) => kind == Kind.WS || kind == Kind.ON;

        private static Kind StrongDirection(Kind kind) => kind == Kind.L ? Kind.L : Kind.R;

        private static int ImplicitLevel(Kind kind, int baseLevel)
        {
            if (baseLevel == 0)
                return kind == Kind.R ? 1 : kind == Kind.EN || kind == Kind.AN ? 2 : 0;
            return kind == Kind.R ? 1 : 2;
        }

        private static void Reorder(int[] order, int[] levels)
        {
            int highest = 0;
            int lowestOdd = int.MaxValue;
            foreach (int level in levels)
            {
                if (level > highest) highest = level;
                if ((level & 1) == 1 && level < lowestOdd) lowestOdd = level;
            }
            if (lowestOdd == int.MaxValue) return;

            var current = (int[])levels.Clone();
            for (int level = highest; level >= lowestOdd; level--)
            {
                for (int i = 0; i < order.Length; i++)
                {
                    if (current[i] < level) continue;
                    int end = i;
                    while (end < order.Length && current[end] >= level) end++;
                    System.Array.Reverse(order, i, end - i);
                    System.Array.Reverse(current, i, end - i);
                    i = end - 1;
                }
            }
        }
    }
}
