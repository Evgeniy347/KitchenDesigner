using System;

namespace KitchenDesigner.Core.Ports
{
    public static class PortJoint
    {
        public const float JoinToleranceMm = Tolerance.ContactMm;

        public static bool Connects(in Port a, in Port b)
        {
            if (string.Equals(a.ElementId, b.ElementId, StringComparison.Ordinal)) return false;
            if (a.PositionMm.DistanceMmTo(b.PositionMm) > JoinToleranceMm) return false;
            return Plumbing.PipeAxis.AreOpposite(a.OutwardAxis, b.OutwardAxis);
        }
    }
}
