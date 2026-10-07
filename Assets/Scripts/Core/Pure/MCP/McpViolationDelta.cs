using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    public static class McpViolationDelta
    {
        public const int MaxNamedPerSide = 12;

        public static ViolationDeltaInfo Between(IEnumerable<string> before, IEnumerable<string> after)
        {
            var was = new HashSet<string>(before, StringComparer.Ordinal);
            var now = new HashSet<string>(after, StringComparer.Ordinal);
            var delta = new ViolationDeltaInfo();
            Fill(delta.added, now, was);
            Fill(delta.removed, was, now);
            return delta;
        }

        private static void Fill(List<string> into, HashSet<string> from, HashSet<string> notIn)
        {
            var names = new List<string>();
            foreach (var name in from)
                if (!notIn.Contains(name)) names.Add(name);
            names.Sort(StringComparer.Ordinal);

            if (names.Count <= MaxNamedPerSide)
            {
                into.AddRange(names);
                return;
            }
            into.AddRange(names.GetRange(0, MaxNamedPerSide));
            into.Add("+" + (names.Count - MaxNamedPerSide) + " more");
        }
    }
}
