using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class ScrewLegHosting
    {
        public const int NoHost = -1;

        public static int HostIndex(in ElementGeometry thread,
            IReadOnlyList<ElementGeometry> candidates, float marginUnits)
        {
            if (thread.IsEmpty || candidates == null) return NoHost;

            int best = NoHost;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c.IsEmpty) continue;
                if (!FaceContacts.AABBsIntersect(thread, c, marginUnits)) continue;
                if (best == NoHost || EntersEarlier(c, candidates[best])) best = i;
            }
            return best;
        }

        private static bool EntersEarlier(in ElementGeometry candidate, in ElementGeometry best)
        {
            float delta = candidate.Min.y - best.Min.y;
            if (delta < -Tolerance.EpsilonUnits) return true;
            if (delta > Tolerance.EpsilonUnits) return false;
            return string.CompareOrdinal(candidate.Name, best.Name) < 0;
        }
    }
}
