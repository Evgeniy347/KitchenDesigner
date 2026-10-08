using System;
using System.Collections.Generic;
using System.Text;

namespace KitchenDesigner.Core.MCP
{
    public static class SceneDigestText
    {
        public const int DefaultMaxChars = 1500;

        public const int MinMaxChars = 200;

        public const int MaxMaxChars = 20000;

        public const int MaxGroupsPerLine = 6;

        public const string ScopeHint = "use scope";

        private const string SizeSeparator = "×";

        private const string Ellipsis = "…";

        public static string Build(DigestInput input, int maxChars)
        {
            var head = new List<string> { Summary(input), Legend(input) };
            var lines = new List<string>();
            if (input.Scope == null)
            {
                AddGroupLine(lines, "levels", input.Levels, input.Entries, entry => entry.Placement.level);
                AddGroupLine(lines, "rooms", input.Rooms, input.Entries, entry => entry.Placement.room);
            }
            foreach (var entry in Ordered(input.Entries, module: true)) lines.Add(EntryLine(entry));
            foreach (var entry in Ordered(input.Entries, module: false)) lines.Add(EntryLine(entry));
            return Pack(head, lines, maxChars);
        }

        public static string MoreFooter(int hidden) => "+" + hidden + " more, " + ScopeHint;

        private static string Pack(List<string> head, List<string> lines, int maxChars)
        {
            var text = new StringBuilder(string.Join("\n", head));
            int shown = 0;
            while (shown < lines.Count && FitsWithFooter(text.Length, lines, shown, maxChars))
            {
                text.Append('\n').Append(lines[shown]);
                shown++;
            }
            if (shown < lines.Count) AppendFooter(text, lines.Count - shown, maxChars);
            return HardCut(text.ToString(), maxChars);
        }

        private static bool FitsWithFooter(int used, List<string> lines, int next, int maxChars)
        {
            int after = used + 1 + lines[next].Length;
            int hiddenAfter = lines.Count - next - 1;
            int footer = hiddenAfter > 0 ? 1 + MoreFooter(hiddenAfter).Length : 0;
            return after + footer <= maxChars;
        }

        private static void AppendFooter(StringBuilder text, int hidden, int maxChars)
        {
            var footer = MoreFooter(hidden);
            if (text.Length + 1 + footer.Length <= maxChars) text.Append('\n').Append(footer);
        }

        private static string HardCut(string text, int maxChars)
        {
            if (maxChars <= 0) return string.Empty;
            if (text.Length <= maxChars) return text;
            return maxChars == 1 ? Ellipsis : text.Substring(0, maxChars - 1) + Ellipsis;
        }

        private static string Summary(DigestInput input)
        {
            int modules = 0, loose = 0, parts = 0, withIssues = 0;
            foreach (var entry in input.Entries)
            {
                if (entry.IsModule) { modules++; parts += entry.PartsCount; }
                else { loose++; parts++; }
                if (entry.HasIssues) withIssues++;
            }
            var counts = modules > 0 ? $"{parts} parts, {modules} modules, {loose} loose" : $"{parts} parts";
            var title = input.Scope == null ? "scene" : "scope " + input.Scope;
            var issues = withIssues > 0 ? withIssues + " with issues" : "no issues";
            return $"{title}: {counts}; {issues}";
        }

        private static string Legend(DigestInput input) =>
            $"mm; size x{SizeSeparator}y{SizeSeparator}z; @(x,y,z) = {input.Ref} point; ! = issue";

        private static void AddGroupLine(List<string> lines, string title, List<DigestGroup> groups,
            List<DigestEntry> entries, Func<DigestEntry, string?> key)
        {
            if (groups.Count == 0) return;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var id = key(entry);
                if (id == null) continue;
                counts.TryGetValue(id, out int have);
                counts[id] = have + Math.Max(entry.PartsCount, 1);
            }
            var words = new List<string>();
            foreach (var group in groups)
            {
                if (words.Count == MaxGroupsPerLine) break;
                counts.TryGetValue(group.Id, out int parts);
                words.Add(group.Id + (group.Detail != null ? " " + group.Detail : string.Empty) + " (" + parts + ")");
            }
            if (groups.Count > words.Count) words.Add("+" + (groups.Count - words.Count) + " more");
            lines.Add(title + ": " + string.Join(", ", words));
        }

        private static List<DigestEntry> Ordered(List<DigestEntry> entries, bool module)
        {
            var picked = entries.FindAll(entry => entry.IsModule == module);
            picked.Sort(ByIssuesThenName);
            return picked;
        }

        private static int ByIssuesThenName(DigestEntry a, DigestEntry b)
        {
            if (a.HasIssues != b.HasIssues) return a.HasIssues ? -1 : 1;
            return McpNaturalName.Compare(a.Name, b.Name);
        }

        public static string EntryLine(DigestEntry entry)
        {
            var placement = entry.Placement;
            var line = new StringBuilder(entry.Name).Append(' ')
                .Append(entry.IsModule ? "module(" + entry.PartsCount + ")" : entry.Kind).Append(' ')
                .Append(Size(placement.footprintMm)).Append(" @(").Append(Join(",", placement.posMm)).Append(')');
            if (placement.on != null) line.Append(' ').Append(PlacementWording.On(placement.on));
            var relations = PlacementWording.Relations(placement);
            if (relations.Count > 0) line.Append("; ").Append(string.Join(", ", relations));
            return line.Append("; ").Append(Status(placement)).ToString();
        }

        private static string Status(PlacementInfo placement) =>
            placement.issues.Count == 0 ? "ok" : "!" + string.Join(" | ", placement.issues);

        private static string Size(float[] footprintMm) => Join(SizeSeparator, footprintMm);

        private static string Join(string separator, float[] mm)
        {
            if (mm.Length == 0) return "?";
            var words = new string[mm.Length];
            for (int i = 0; i < mm.Length; i++) words[i] = PlacementWording.Mm(mm[i]);
            return string.Join(separator, words);
        }
    }
}
