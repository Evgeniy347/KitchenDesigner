using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class EditGate
    {
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

        private static bool NearAnyOf(KitchenElement element, IReadOnlyList<KitchenElement> focus,
            float radiusUnits)
        {
            for (int i = 0; i < focus.Count; i++)
                if (ConstraintValidator.AreWithin(element, focus[i], radiusUnits)) return true;
            return false;
        }
    }
}
