namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeJoint
    {
        public const float JoinToleranceMm = Ports.PortJoint.JoinToleranceMm;

        public static bool Connects(in PipePort a, in PipePort b) =>
            PipeConnectionRule.CanConnect(a.OwnerKind, b.OwnerKind) &&
            Ports.PortJoint.Connects(a.AsPort(), b.AsPort());
    }
}
