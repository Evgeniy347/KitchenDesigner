using System;

namespace KitchenDesigner.Core.MCP
{
    public static class McpNameShortening
    {
        public const int MinChars = 6;
        public const string Gap = "..";
        public const int PlainTailChars = 2;
        public const int MaxTailChars = 5;
        public const int MinHeadChars = 2;

        public static string Fit(string name, int maxChars)
        {
            if (name.Length <= maxChars) return name;
            int room = Math.Max(maxChars, MinChars);
            int tail = Math.Min(Math.Min(TailLength(name), room - Gap.Length - MinHeadChars), name.Length - 1);
            int head = room - Gap.Length - tail;
            return name.Substring(0, head) + Gap + name.Substring(name.Length - tail);
        }

        public static int TailLength(string name)
        {
            int digits = 0;
            while (digits < name.Length && char.IsDigit(name[name.Length - 1 - digits])) digits++;
            return digits == 0 ? PlainTailChars : Math.Min(digits + 1, MaxTailChars);
        }
    }
}
