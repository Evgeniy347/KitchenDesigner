using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;

namespace KitchenDesigner.Core
{
    public static class EditGate
    {
        public const string RefusalPrefix = "Правка отклонена: ";

        public static float RadiusUnits =>
            KitchenSettings.Instance.SnapThreshold * 2f * AppConstants.MM_TO_UNITS;

        public static bool Introduced(SceneViolations? before, SceneViolations? after,
            IReadOnlyList<KitchenElement>? focus, out ContactViolation introduced)
        {
            introduced = default;
            if (after == null || after.IsClean || focus == null || focus.Count == 0) return false;

            var known = before ?? SceneViolations.Empty;
            float radiusUnits = RadiusUnits;
            foreach (var v in after.Found)
            {
                if (v.element == null) continue;
                if (known.Holds(v.element, v.kind)) continue;
                if (!NearAnyOf(v.element, focus, radiusUnits)) continue;
                introduced = v;
                return true;
            }
            return false;
        }

        public static bool Refuses(SceneViolations? before, SceneViolations? after,
            KitchenElement? focus, out string refusal) =>
            Refuses(before, after, focus == null ? null : new[] { focus }, out refusal);

        public static bool Refuses(SceneViolations? before, SceneViolations? after,
            IReadOnlyList<KitchenElement>? focus, out string refusal)
        {
            bool refused = Introduced(before, after, focus, out var introduced);
            refusal = refused ? TextOf(introduced) : string.Empty;
            return refused;
        }

        public static string TextOf(ContactViolation introduced)
        {
            var issue = IssueCatalog.FromViolation(introduced);
            return $"{RefusalPrefix}{issue.Code} · {issue.Detail} · {issue.Message}";
        }

        private static bool NearAnyOf(KitchenElement element, IReadOnlyList<KitchenElement> focus,
            float radiusUnits)
        {
            for (int i = 0; i < focus.Count; i++)
                if (ConstraintValidator.AreWithin(element, focus[i], radiusUnits)) return true;
            return false;
        }
    }
}
