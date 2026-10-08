using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    public sealed class PlanLabelNames
    {
        public const int MinShortChars = 5;
        public const string Gap = "..";
        public const int TailChars = 2;
        public const int MaxVariants = 9;

        private readonly Dictionary<string, string> _fullBy = new Dictionary<string, string>(StringComparer.Ordinal);

        public static string Cut(string name, int maxChars)
        {
            if (name.Length <= maxChars) return name;
            int head = maxChars - Gap.Length - TailChars;
            return name.Substring(0, head) + Gap + name.Substring(name.Length - TailChars);
        }

        public string? Unique(string name, int maxChars)
        {
            var cut = Cut(name, maxChars);
            if (cut == name || IsFree(cut, name)) return cut;
            for (int n = 2; n <= MaxVariants; n++)
            {
                var variant = name.Substring(0, maxChars - Gap.Length - 1) + Gap + n;
                if (IsFree(variant, name)) return variant;
            }
            return null;
        }

        public void Commit(string shortName, string fullName) => _fullBy[shortName] = fullName;

        private bool IsFree(string shortName, string fullName) =>
            !_fullBy.TryGetValue(shortName, out var owner) || owner == fullName;
    }
}
