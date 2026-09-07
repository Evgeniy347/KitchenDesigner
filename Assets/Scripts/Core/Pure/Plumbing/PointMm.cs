namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PointMm
    {
        public readonly float XMm;
        public readonly float YMm;
        public readonly float ZMm;

        public PointMm(float xMm, float yMm, float zMm)
        {
            XMm = xMm;
            YMm = yMm;
            ZMm = zMm;
        }

        public float DistanceMmTo(in PointMm other)
        {
            float dx = XMm - other.XMm;
            float dy = YMm - other.YMm;
            float dz = ZMm - other.ZMm;
            return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public PointMm Shifted(in PipeAxis axis, float distanceMm) =>
            new PointMm(XMm + axis.X * distanceMm, YMm + axis.Y * distanceMm,
                ZMm + axis.Z * distanceMm);
    }
}
