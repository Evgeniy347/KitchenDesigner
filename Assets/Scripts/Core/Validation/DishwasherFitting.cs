using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public readonly struct DishwasherBackGapIssue
    {
        public readonly DishwasherElement dishwasher;
        public readonly FacadeElement facade;
        public readonly float gapMm;
        public DishwasherBackGapIssue(DishwasherElement dishwasher, FacadeElement facade, float gapMm)
        {
            this.dishwasher = dishwasher;
            this.facade = facade;
            this.gapMm = gapMm;
        }
    }

    public readonly struct DishwasherSupportIssue
    {
        public readonly DishwasherElement dishwasher;
        public readonly KitchenElement? blocker;
        public readonly float sinkMm;
        public DishwasherSupportIssue(DishwasherElement dishwasher, KitchenElement? blocker, float sinkMm)
        {
            this.dishwasher = dishwasher;
            this.blocker = blocker;
            this.sinkMm = sinkMm;
        }
    }

    public static class DishwasherFitting
    {
        public static bool IsFacadePair(KitchenElement a, KitchenElement b)
        {
            var dishwasher = ValidationSnapshot.AsDishwasher(a) ?? ValidationSnapshot.AsDishwasher(b);
            var facade = ValidationSnapshot.AsFacade(a) ?? ValidationSnapshot.AsFacade(b);
            if (dishwasher == null || facade == null) return false;
            return !string.IsNullOrEmpty(dishwasher.AttachedFacadeName)
                && dishwasher.AttachedFacadeName == facade.PartName;
        }

        public static List<DishwasherBackGapIssue> FindFacadeBackGaps(List<KitchenElement> all)
        {
            var result = new List<DishwasherBackGapIssue>();
            if (all == null) return result;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float mountMm = DishwasherElement.FACADE_MOUNT_GAP_MM;
            float maxGap = mountMm * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;

            foreach (var e in all)
            {
                var dw = ValidationSnapshot.AsDishwasher(e);
                if (dw == null) continue;
                var facade = dw.FindAttachedFacade();
                if (facade == null) continue;

                float sum = DrawerLinks.WithFacadeClosed(facade, DrawerLinks.IsFacadeDisplacedBy(dw),
                    () => FaceContacts.SumParallelGaps(e.GetFaces(), facade.GetFaces(), contactDist, maxGap));
                float sumMm = sum * toMm;
                if (sumMm <= 0f) continue;
                if (sumMm + ConstraintValidator.GapNoiseMm < mountMm)
                    result.Add(new DishwasherBackGapIssue(dw, facade, sumMm));
            }
            return result;
        }

        public static List<DishwasherSupportIssue> FindSupportIssues(List<KitchenElement> all)
        {
            var result = new List<DishwasherSupportIssue>();
            if (all == null) return result;

            float eps = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;
            float reach = DishwasherElement.FEET_ADJUST_MM * AppConstants.MM_TO_UNITS;

            foreach (var e in all)
            {
                var dw = ValidationSnapshot.AsDishwasher(e);
                if (dw == null) continue;

                float soleY = dw.SoleCenterWorld.y;
                var dwGeo = dw.ToGeometry();
                var facade = dw.FindAttachedFacade();

                KitchenElement? blocker = null;
                float deepest = 0f;
                bool supported = false;

                foreach (var other in all)
                {
                    if (other == null || ReferenceEquals(other, e)) continue;
                    if (ReferenceEquals(other, facade)) continue;
                    if (ValidationSnapshot.IsDecor(other)) continue;

                    var g = other.ToGeometry();
                    if (g.Min.x >= dwGeo.Max.x - eps || g.Max.x <= dwGeo.Min.x + eps) continue;
                    if (g.Min.z >= dwGeo.Max.z - eps || g.Max.z <= dwGeo.Min.z + eps) continue;

                    if (g.Max.y <= soleY + eps && g.Max.y >= soleY - reach - eps)
                    {
                        supported = true;
                        break;
                    }

                    if (g.Min.y < soleY - eps && g.Max.y > soleY + eps)
                    {
                        float sink = (g.Max.y - soleY) * toMm;
                        if (sink > deepest) { deepest = sink; blocker = other; }
                    }
                }

                if (supported) continue;
                result.Add(new DishwasherSupportIssue(dw, blocker, deepest));
            }
            return result;
        }
    }
}
