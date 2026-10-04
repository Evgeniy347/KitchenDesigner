using System;
using System.Collections.Generic;
using System.Text;

namespace KitchenDesigner.Core
{
    public static class PerfHudLines
    {
        public const float FrameBudgetMs = 1000f / 60f;
        public const float MarkerWarnMs = FrameBudgetMs / 3f;

        public static PerfHudLineKind ForFrame(float dtMs) =>
            dtMs > FrameBudgetMs ? PerfHudLineKind.Warning : PerfHudLineKind.Normal;

        public static PerfHudLineKind ForMarker(float ms) =>
            ms >= MarkerWarnMs ? PerfHudLineKind.Warning : PerfHudLineKind.Normal;

        public static string Join(IReadOnlyList<PerfHudLine> lines)
        {
            var sb = new StringBuilder();
            foreach (var line in lines) sb.Append(line.Text).Append(Environment.NewLine);
            return sb.ToString();
        }
    }
}
