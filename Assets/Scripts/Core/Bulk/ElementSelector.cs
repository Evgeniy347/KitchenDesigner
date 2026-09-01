using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Bulk
{
    public static class ElementSelector
    {
        public static List<KitchenElement> Match(string? selector)
            => Match(selector, PartRegistry.GetAll());

        public static List<KitchenElement> Match(string? selector, IEnumerable<KitchenElement> pool)
        {
            var clauses = ParseClauses(selector);
            var result = new List<KitchenElement>();
            foreach (var e in pool)
            {
                if (!IsSelectableContent(e)) continue;
                bool ok = true;
                foreach (var c in clauses) { if (!c(e)) { ok = false; break; } }
                if (ok) result.Add(e);
            }
            return result;
        }

        private static bool IsSelectableContent(KitchenElement e) =>
            e != null
            && e.gameObject.activeInHierarchy
            && e.GetComponent<BasePlate>() == null;

        private static readonly string[] CmpOps = { ">=", "<=", "==", "!=", "=", ">", "<" };

        private static bool IsUnrestrictedToken(string lower) =>
            lower == "*" || lower == "all" || lower == "all_elements";

        private static List<Func<KitchenElement, bool>> ParseClauses(string? selector)
        {
            var clauses = new List<Func<KitchenElement, bool>>();
            if (string.IsNullOrWhiteSpace(selector)) return clauses;

            foreach (var raw in selector.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim();
                var lower = token.ToLowerInvariant();

                if (IsUnrestrictedToken(lower))
                    continue;
                if (lower == "all_boards") { clauses.Add(e => TypeOf(e) == "board"); continue; }
                if (lower == "all_modules") { clauses.Add(e => GroupManager.GroupOf(e) != null); continue; }

                if (TrySplit(token, "type:", out var t))
                { var tl = t.ToLowerInvariant(); clauses.Add(e => TypeOf(e) == tl); continue; }

                if (TrySplit(token, "module:", out var m) || TrySplit(token, "group:", out m))
                { var pat = m; clauses.Add(e => { var g = GroupManager.GroupOf(e); return g != null && Glob(g.name, pat); }); continue; }

                if (TrySplit(token, "name:", out var n))
                { clauses.Add(MatchesName(n)); continue; }

                if (TryParseCompare(token, out var dim, out var op, out var val))
                { clauses.Add(e => Compare(DimOf(e, dim), op, val)); continue; }

                clauses.Add(MatchesName(token));
            }
            return clauses;
        }

        private static Func<KitchenElement, bool> MatchesName(string pattern)
            => e => Glob(e.PartName, pattern);

        private static bool TrySplit(string token, string prefix, out string rest)
        {
            if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            { rest = token.Substring(prefix.Length); return true; }
            rest = "";
            return false;
        }

        private static bool TryParseCompare(string token, out string dim, out string op, out int val)
        {
            dim = ""; op = ""; val = 0;
            foreach (var candidate in CmpOps)
            {
                int idx = token.IndexOf(candidate, StringComparison.Ordinal);
                if (idx <= 0) continue;
                var left = token.Substring(0, idx).Trim().ToLowerInvariant();
                var right = token.Substring(idx + candidate.Length).Trim();
                if (left != "width" && left != "height" && left != "depth" && left != "thickness") return false;
                if (!int.TryParse(right, out val)) return false;
                dim = left; op = candidate == "=" ? "==" : candidate;
                return true;
            }
            return false;
        }

        private static int DimOf(KitchenElement e, string dim)
        {
            var d = e.DimensionsMM;
            return dim switch
            {
                "width" => d.x,
                "height" => d.y,
                "depth" => d.z,
                "thickness" => d.z,
                _ => 0,
            };
        }

        private static bool Compare(int a, string op, int b) => op switch
        {
            "==" => a == b,
            "!=" => a != b,
            ">=" => a >= b,
            "<=" => a <= b,
            ">" => a > b,
            "<" => a < b,
            _ => false,
        };

        public static string TypeOf(KitchenElement e)
        {
            if (e is WindowElement) return "window";
            if (e is DoorElement) return "door";
            if (e is DrawerElement) return "drawer";
            if (e is AssembledFacadeElement) return "assembled_facade";
            if (e is FacadeElement) return "facade";
            if (e is RadialShelfElement) return "radial_shelf";
            if (e is PanelElement) return "panel";
            if (e is RadiusTableElement) return "radius_table";
            if (e is TableElement) return "table";
            if (e is StoolElement) return "stool";
            if (e is ChairElement) return "chair";
            if (e is SofaElement) return "sofa";
            if (e is PillarElement) return "pillar";
            if (e is ScrewLegElement) return "screw_leg";
            if (e is LightSourceElement) return "light";
            if (e is SinkElement) return "sink";
            if (e is CooktopElement) return "cooktop";
            if (e is OvenElement) return "oven";
            if (e is DishwasherElement) return "dishwasher";
            if (e is FloorElement) return "floor";
            if (e.GetComponent<Wall>() != null) return "wall";
            return "board";
        }

        private static bool Glob(string value, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            value ??= "";
            if (pattern.IndexOf('*') < 0)
                return value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;

            var parts = pattern.Split('*');
            int pos = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length == 0) continue;
                int found = value.IndexOf(part, pos, StringComparison.OrdinalIgnoreCase);
                if (found < 0) return false;
                if (i == 0 && !pattern.StartsWith("*") && found != 0) return false;
                pos = found + part.Length;
            }
            if (!pattern.EndsWith("*"))
            {
                var last = parts[parts.Length - 1];
                if (last.Length > 0 && !value.EndsWith(last, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }
    }
}
