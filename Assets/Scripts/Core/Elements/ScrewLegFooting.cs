using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public readonly struct ScrewLegSupport
    {
        public readonly KitchenElement? Nearest;
        public readonly float GapMM;

        public ScrewLegSupport(KitchenElement? nearest, float gapMM)
        {
            Nearest = nearest;
            GapMM = gapMM;
        }
    }

    public static class ScrewLegFooting
    {
        public static bool TryFindUnsupported(ScrewLegElement leg,
            IReadOnlyList<KitchenElement> scene, out ScrewLegSupport below)
        {
            below = default;
            if (leg == null || scene == null) return false;

            float eps = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            var pad = leg.BaseBody;
            if (pad.IsEmpty) return false;
            float bottom = pad.Min.y;

            KitchenElement? nearest = null;
            float nearestTop = 0f;
            string nearestName = "";
            for (int i = 0; i < scene.Count; i++)
            {
                var other = scene[i];
                if (other == null || ReferenceEquals(other, leg)) continue;
                if (other is ScrewLegElement || ValidationSnapshot.IsDecor(other)) continue;

                var box = ElementAabb.Of(other);
                if (box.minX >= pad.Max.x - eps || box.maxX <= pad.Min.x + eps) continue;
                if (box.minZ >= pad.Max.z - eps || box.maxZ <= pad.Min.z + eps) continue;

                if (box.maxY >= bottom - eps)
                {
                    if (box.maxY <= bottom + eps) return false;
                    continue;
                }

                if (nearest != null
                    && !RisesAbove(box.maxY, nearestTop, other.PartName, nearestName))
                    continue;
                nearest = other;
                nearestTop = box.maxY;
                nearestName = other.PartName;
            }

            below = new ScrewLegSupport(nearest,
                nearest == null ? 0f : (bottom - nearestTop) / AppConstants.MM_TO_UNITS);
            return true;
        }

        private static bool RisesAbove(float top, float bestTop, string name, string bestName)
        {
            float delta = top - bestTop;
            if (delta > Tolerance.EpsilonUnits) return true;
            if (delta < -Tolerance.EpsilonUnits) return false;
            return string.CompareOrdinal(name, bestName) < 0;
        }
    }
}
