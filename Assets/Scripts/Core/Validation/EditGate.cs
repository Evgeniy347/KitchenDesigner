using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;

namespace KitchenDesigner.Core
{
    public static class EditGate
    {
        public const string RefusalPrefix = "Правка отклонена: ";

        public static bool IntroducedOn(SceneViolations? before, SceneViolations? after,
            KitchenElement? focus, out ContactViolation introduced)
        {
            introduced = default;
            if (focus == null || after == null || after.IsClean) return false;

            var known = before ?? SceneViolations.Empty;
            foreach (var v in after.Found)
            {
                if (!ReferenceEquals(v.element, focus)) continue;
                if (known.Holds(v.element, v.kind)) continue;
                introduced = v;
                return true;
            }
            return false;
        }

        public static bool IntroducedNear(SceneViolations? before, SceneViolations? after,
            IReadOnlyList<KitchenElement>? focus, float radiusUnits, out ContactViolation introduced)
        {
            introduced = default;
            if (after == null || after.IsClean || focus == null || focus.Count == 0) return false;

            var known = before ?? SceneViolations.Empty;
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
            KitchenElement? focus, out string refusal)
        {
            bool refused = IntroducedOn(before, after, focus, out var introduced);
            refusal = refused ? TextOf(introduced) : string.Empty;
            return refused;
        }

        public static bool Refuses(SceneViolations? before, SceneViolations? after,
            IReadOnlyList<KitchenElement>? focus, float radiusUnits, out string refusal)
        {
            bool refused = IntroducedNear(before, after, focus, radiusUnits, out var introduced);
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
