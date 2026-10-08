using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class PlanLabelNames
    {
        public const int MaxVariants = 9;

        private readonly Dictionary<string, string> _fullBy = new Dictionary<string, string>(StringComparer.Ordinal);

        public string? Unique(string name, int maxChars)
        {
            var cut = McpNameShortening.Fit(name, maxChars);
            if (cut == name || IsFree(cut, name)) return cut;
            for (int n = 2; n <= MaxVariants; n++)
            {
                var variant = name.Substring(0, maxChars - McpNameShortening.Gap.Length - 1) + McpNameShortening.Gap + n;
                if (IsFree(variant, name)) return variant;
            }
            return null;
        }

        public void Commit(string shortName, string fullName) => _fullBy[shortName] = fullName;

        private bool IsFree(string shortName, string fullName) =>
            !_fullBy.TryGetValue(shortName, out var owner) || owner == fullName;
    }
}
