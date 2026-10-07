using System.Collections.Generic;
using System.Globalization;

namespace KitchenDesigner.Core.MCP
{
    internal static class McpPlacementIssues
    {
        public const int MaxIssues = 6;

        public static List<string> Of(KitchenElement el, List<KitchenElement> all,
            ValidationResult? validation, PlacementRelationSet relations)
        {
            var issues = new List<string>();
            foreach (var overlap in relations.Overlaps)
                issues.Add(McpAabb.ClassifyOverlapMm(overlap.depthMm) + ": " + overlap.n + " "
                    + overlap.depthMm.ToString("0.#", CultureInfo.InvariantCulture) + "mm");

            if (validation != null && validation.violations.Contains(el) && relations.Overlaps.Count == 0)
                issues.Add("disconnected");

            if (el is FacadeElement facade) AddFacadeIssues(issues, McpFacadeIssues.Of(facade, all));
            if (el is DrawerElement drawer) AddDrawerIssues(issues, drawer, all);
            return Capped(issues);
        }

        private static void AddFacadeIssues(List<string> issues, McpFacadeIssues facade)
        {
            if (facade.faceInward) issues.Add("facade_facing_inward");
            foreach (var obstruction in facade.obstructions)
                issues.Add("face_obstruction " + obstruction.neighbor);
            foreach (var violation in facade.openingViolations)
                issues.Add("opening_collision " + violation.neighbor);
        }

        private static void AddDrawerIssues(List<string> issues, DrawerElement drawer, List<KitchenElement> all)
        {
            var validation = DrawerValidator.ValidateAll(drawer, all);
            if (validation.IsValid) return;
            foreach (var error in validation.Errors) issues.Add("drawer_invalid " + error);
        }

        private static List<string> Capped(List<string> issues)
        {
            if (issues.Count <= MaxIssues) return issues;
            var kept = issues.GetRange(0, MaxIssues - 1);
            kept.Add("+" + (issues.Count - kept.Count) + " more");
            return kept;
        }
    }
}
