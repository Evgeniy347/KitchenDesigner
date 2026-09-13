using System;
using System.Text;

namespace KitchenDesigner.Core
{
    public static class SelectionWorkLog
    {
        private static readonly string[] Captions =
        {
            "красок",
            "рендереров",
            "проверок сцены",
            "оповещений",
        };

        public static int KindsCounted => Captions.Length;

        [ThreadStatic] private static int[]? _counts;

        private static int[] Counts() => _counts ??= new int[Captions.Length];

        public static string CaptionOf(SelectionWork what) => Captions[(int)what];

        public static void Note(SelectionWork what) => Counts()[(int)what]++;

        public static int Count(SelectionWork what) => Counts()[(int)what];

        public static void Forget() => Array.Clear(Counts(), 0, Captions.Length);

        public static string Take()
        {
            var counts = Counts();
            var text = new StringBuilder();
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                if (text.Length > 0) text.Append(", ");
                text.Append(Captions[i]).Append(' ').Append(counts[i]);
            }
            Forget();
            return text.ToString();
        }
    }
}
