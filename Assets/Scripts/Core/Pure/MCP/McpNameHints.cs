using System;
using System.Collections.Generic;
using System.Linq;

namespace KitchenDesigner.Core.MCP
{
    public static class McpNameHints
    {
        public const string ResendAll =
            "Nothing was applied: after fixing, resend the WHOLE batch, not only the failed items.";

        public static IReadOnlyList<string> Closest(string wanted, IEnumerable<string> names, int max = 3)
        {
            var target = (wanted ?? string.Empty).Trim().ToLowerInvariant();
            if (target.Length == 0) return Array.Empty<string>();
            int tolerance = Math.Max(2, target.Length / 2);
            return names
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => (name: n, distance: Distance(target, n.ToLowerInvariant())))
                .Where(x => x.distance <= tolerance)
                .OrderBy(x => x.distance).ThenBy(x => x.name, StringComparer.Ordinal)
                .Take(max)
                .Select(x => x.name)
                .ToList();
        }

        public static string NotFound(string wanted, IEnumerable<string> names)
        {
            var close = Closest(wanted, names);
            return close.Count > 0
                ? $"Element not found: {wanted} (closest names: {string.Join(", ", close)}; all names: get_scene_tree)"
                : $"Element not found: {wanted} (names are exact and case-sensitive; get_scene_tree lists them all)";
        }

        public static string UniqueName(string name, IEnumerable<string> taken)
        {
            var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
            for (int i = 2; i < 10000; i++)
            {
                var candidate = name + "_" + i;
                if (!used.Contains(candidate)) return candidate;
            }
            return name + "_new";
        }

        public static string AlreadyExists(string name, IEnumerable<string> taken) =>
            $"Element '{name}' already exists. To change it use edit_elements, to move it use place, "
            + $"or create a different name (free: '{UniqueName(name, taken)}')";

        public static string Locked(string name) =>
            $"Element '{name}' is LOCKED. Unlock it with edit_elements {{ops:[{{name:'{name}', locked:false}}]}} "
            + "- but ONLY if the user explicitly allowed editing this element";

        public static string UnknownFace(string face) =>
            $"Unknown face '{face}'. Valid faces: {McpFace.Words}";

        public static string FaceAxisMismatch(string face, string targetFace)
        {
            if (!McpFace.TryParse(face, out int axis, out bool maxSide))
                return UnknownFace(face);
            string facing = McpFace.NameOf(axis, !maxSide);
            return $"face '{face}' and target_face '{targetFace}' lie on different axes. For '{face}' use "
                + $"target_face '{facing}' (flush against it) or '{face}' (same side aligned)";
        }

        public const int MaxListedScopeNames = 8;

        public static string UnknownScope(string scope, IReadOnlyList<string> modules, IReadOnlyList<string> rooms)
        {
            var close = Closest(scope, modules.Concat(rooms));
            var hint = close.Count > 0 ? $" (closest: {string.Join(", ", close)})" : string.Empty;
            return $"Scope '{scope}' matches no module, room or part{hint}. Modules: {Listed(modules)}. Rooms: {Listed(rooms)}. "
                + "A scope is a module name, room:ID or a selector (name mask, type:board, module:B4*; see guide topic bulk)";
        }

        private static string Listed(IReadOnlyList<string> names)
        {
            if (names.Count == 0) return "none";
            var shown = string.Join(", ", names.Take(MaxListedScopeNames));
            return names.Count > MaxListedScopeNames ? shown + " (+" + (names.Count - MaxListedScopeNames) + " more)" : shown;
        }

        private static int Distance(string a, string b)
        {
            if (b.Contains(a) || a.Contains(b)) return Math.Abs(a.Length - b.Length) / 2;
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }
                (previous, current) = (current, previous);
            }
            return previous[b.Length];
        }
    }
}
