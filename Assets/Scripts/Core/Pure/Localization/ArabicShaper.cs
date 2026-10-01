using System.Collections.Generic;
using System.Text;

namespace KitchenDesigner.Core
{
    public static class ArabicShaper
    {
        public const char Lam = 'ل';
        public const char Tatweel = 'ـ';
        public const char ZeroWidthJoiner = '‍';

        private enum Joining : byte { None, Right, Dual }

        private readonly struct Forms
        {
            public readonly char Isolated;
            public readonly char Final;
            public readonly char Initial;
            public readonly char Medial;

            public Forms(char isolated, char final, char initial, char medial)
            {
                Isolated = isolated;
                Final = final;
                Initial = initial;
                Medial = medial;
            }

            public Joining Joining => Initial != '\0' ? Joining.Dual : Final != '\0' ? Joining.Right : Joining.None;
        }

        private static readonly IReadOnlyDictionary<char, Forms> Letters = BuildLetters();

        private static readonly IReadOnlyDictionary<char, (char Isolated, char Final)> LamAlef = new Dictionary<char, (char, char)>
        {
            ['آ'] = ('ﻵ', 'ﻶ'),
            ['أ'] = ('ﻷ', 'ﻸ'),
            ['إ'] = ('ﻹ', 'ﻺ'),
            ['ا'] = ('ﻻ', 'ﻼ'),
        };

        public static bool HasArabic(string text)
        {
            foreach (char c in text)
                if (IsArabicScript(c)) return true;
            return false;
        }

        public static bool IsArabicScript(char c) =>
            (c >= '؀' && c <= 'ۿ') || (c >= 'ݐ' && c <= 'ݿ')
            || (c >= 'ﭐ' && c <= '﷿') || (c >= 'ﹰ' && c <= '﻿');

        public static string Shape(string text)
        {
            if (!HasArabic(text)) return text;
            var shaped = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!Letters.TryGetValue(c, out var forms))
                {
                    shaped.Append(c);
                    continue;
                }

                bool joinsPrevious = forms.Joining != Joining.None && JoinsToFollowing(PreviousBase(text, i));
                int next = NextBase(text, i);

                if (c == Lam && next >= 0 && LamAlef.TryGetValue(text[next], out var ligature))
                {
                    shaped.Append(joinsPrevious ? ligature.Final : ligature.Isolated);
                    for (int mark = i + 1; mark < next; mark++) shaped.Append(text[mark]);
                    i = next;
                    continue;
                }

                bool joinsNext = forms.Joining == Joining.Dual && next >= 0 && JoinsToPreceding(text[next]);
                shaped.Append(Pick(forms, joinsPrevious, joinsNext));
            }
            return shaped.ToString();
        }

        private static char Pick(Forms forms, bool joinsPrevious, bool joinsNext)
        {
            if (joinsPrevious && joinsNext) return forms.Medial;
            if (joinsPrevious) return forms.Final;
            if (joinsNext) return forms.Initial;
            return forms.Isolated;
        }

        private static bool IsTransparent(char c) =>
            (c >= 'ؐ' && c <= 'ؚ') || (c >= 'ً' && c <= 'ٟ') || c == 'ٰ'
            || (c >= 'ۖ' && c <= 'ۜ') || (c >= '۟' && c <= 'ۤ')
            || c == 'ۧ' || c == 'ۨ' || (c >= '۪' && c <= 'ۭ');

        private static bool IsJoinCausing(char c) => c == Tatweel || c == ZeroWidthJoiner;

        private static bool JoinsToFollowing(int c) =>
            c >= 0 && (IsJoinCausing((char)c) || (Letters.TryGetValue((char)c, out var f) && f.Joining == Joining.Dual));

        private static bool JoinsToPreceding(char c) =>
            IsJoinCausing(c) || (Letters.TryGetValue(c, out var f) && f.Joining != Joining.None);

        private static int PreviousBase(string text, int index)
        {
            for (int i = index - 1; i >= 0; i--)
                if (!IsTransparent(text[i])) return text[i];
            return -1;
        }

        private static int NextBase(string text, int index)
        {
            for (int i = index + 1; i < text.Length; i++)
                if (!IsTransparent(text[i])) return i;
            return -1;
        }

        private static Dictionary<char, Forms> BuildLetters()
        {
            var letters = new Dictionary<char, Forms>();
            void Right(char letter, char isolated) =>
                letters[letter] = new Forms(isolated, (char)(isolated + 1), '\0', '\0');
            void Dual(char letter, char isolated) =>
                letters[letter] = new Forms(isolated, (char)(isolated + 1), (char)(isolated + 2), (char)(isolated + 3));

            letters['ء'] = new Forms('ﺀ', '\0', '\0', '\0');
            Right('آ', 'ﺁ');
            Right('أ', 'ﺃ');
            Right('ؤ', 'ﺅ');
            Right('إ', 'ﺇ');
            Dual('ئ', 'ﺉ');
            Right('ا', 'ﺍ');
            Dual('ب', 'ﺏ');
            Right('ة', 'ﺓ');
            Dual('ت', 'ﺕ');
            Dual('ث', 'ﺙ');
            Dual('ج', 'ﺝ');
            Dual('ح', 'ﺡ');
            Dual('خ', 'ﺥ');
            Right('د', 'ﺩ');
            Right('ذ', 'ﺫ');
            Right('ر', 'ﺭ');
            Right('ز', 'ﺯ');
            Dual('س', 'ﺱ');
            Dual('ش', 'ﺵ');
            Dual('ص', 'ﺹ');
            Dual('ض', 'ﺽ');
            Dual('ط', 'ﻁ');
            Dual('ظ', 'ﻅ');
            Dual('ع', 'ﻉ');
            Dual('غ', 'ﻍ');
            Dual('ف', 'ﻑ');
            Dual('ق', 'ﻕ');
            Dual('ك', 'ﻙ');
            Dual('ل', 'ﻝ');
            Dual('م', 'ﻡ');
            Dual('ن', 'ﻥ');
            Dual('ه', 'ﻩ');
            Right('و', 'ﻭ');
            Right('ى', 'ﻯ');
            Dual('ي', 'ﻱ');
            Dual('پ', 'ﭖ');
            Dual('چ', 'ﭺ');
            Right('ژ', 'ﮊ');
            Dual('ڤ', 'ﭪ');
            Dual('ک', 'ﮎ');
            Dual('گ', 'ﮒ');
            Dual('ی', 'ﯼ');
            return letters;
        }
    }
}
