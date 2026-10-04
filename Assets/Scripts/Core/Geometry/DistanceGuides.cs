using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class DistanceGuides
    {
        public static readonly int[] Signs = { 1, -1 };

        public static AxisCast Cast(AxisGuideIndex index, in AxisBox moving, int axis, int sign)
        {
            var origin = moving.Centre;
            moving.Spans(origin, axis, out float lo, out float hi);
            float start = sign > 0 ? hi : -lo;
            return index.Cast(origin, axis, sign, start);
        }

        public static void Collect(AxisGuideIndex index, in AxisBox moving, List<GuideLine> into)
        {
            into.Clear();
            for (int axis = 0; axis < 3; axis++)
                foreach (int sign in Signs)
                {
                    var cast = Cast(index, moving, axis, sign);
                    if (!cast.HasNear || cast.NearGap <= Tolerance.ContactUnits) continue;

                    bool equal = EqualGapSnap.GapsAreEqual(cast);
                    into.Add(new GuideLine(cast.PointAt(cast.Start), cast.PointAt(cast.Near.Enter), equal));
                    if (equal)
                        into.Add(new GuideLine(cast.PointAt(cast.Near.Exit), cast.PointAt(cast.Far.Enter), true));
                }
        }
    }
}
