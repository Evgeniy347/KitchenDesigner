namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct BoxMm
    {
        public readonly PointMm MinMm;
        public readonly PointMm MaxMm;

        public BoxMm(in PointMm minMm, in PointMm maxMm)
        {
            MinMm = minMm;
            MaxMm = maxMm;
        }

        public static BoxMm Around(in PointMm a, in PointMm b, float marginMm) =>
            new BoxMm(
                new PointMm(Lower(a.XMm, b.XMm) - marginMm, Lower(a.YMm, b.YMm) - marginMm,
                    Lower(a.ZMm, b.ZMm) - marginMm),
                new PointMm(Upper(a.XMm, b.XMm) + marginMm, Upper(a.YMm, b.YMm) + marginMm,
                    Upper(a.ZMm, b.ZMm) + marginMm));

        public bool Overlaps(in BoxMm other) =>
            AxisOverlaps(MinMm.XMm, MaxMm.XMm, other.MinMm.XMm, other.MaxMm.XMm)
            && AxisOverlaps(MinMm.YMm, MaxMm.YMm, other.MinMm.YMm, other.MaxMm.YMm)
            && AxisOverlaps(MinMm.ZMm, MaxMm.ZMm, other.MinMm.ZMm, other.MaxMm.ZMm);

        private static bool AxisOverlaps(float min1, float max1, float min2, float max2) =>
            Tolerance.IntervalsOverlap(min1, max1, min2, max2, Tolerance.ContactMm);

        private static float Lower(float a, float b) => a < b ? a : b;

        private static float Upper(float a, float b) => a > b ? a : b;
    }
}
