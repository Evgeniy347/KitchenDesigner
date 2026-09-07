using System;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeJoint
    {
        public const float JoinToleranceMm = Tolerance.ContactMm;

        public static bool Connects(in PipePort a, in PipePort b)
        {
            if (string.Equals(a.ElementId, b.ElementId, StringComparison.Ordinal)) return false;
            if (a.PositionMm.DistanceMmTo(b.PositionMm) > JoinToleranceMm) return false;
            return PipeAxis.AreOpposite(a.OutwardAxis, b.OutwardAxis);
        }
    }
}
