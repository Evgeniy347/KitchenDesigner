namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipeAxis
    {
        public static readonly PipeAxis Right = new PipeAxis(1f, 0f, 0f);
        public static readonly PipeAxis Left = new PipeAxis(-1f, 0f, 0f);
        public static readonly PipeAxis Up = new PipeAxis(0f, 1f, 0f);
        public static readonly PipeAxis Down = new PipeAxis(0f, -1f, 0f);
        public static readonly PipeAxis Forward = new PipeAxis(0f, 0f, 1f);
        public static readonly PipeAxis Back = new PipeAxis(0f, 0f, -1f);

        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public PipeAxis(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float LengthSquared => X * X + Y * Y + Z * Z;

        public bool IsValid => LengthSquared > Tolerance.EpsilonSqr;

        public PipeAxis Opposite => new PipeAxis(-X, -Y, -Z);

        public PipeAxis Normalized
        {
            get
            {
                float length = (float)System.Math.Sqrt(LengthSquared);
                return length > 0f ? new PipeAxis(X / length, Y / length, Z / length) : this;
            }
        }

        public static float Dot(in PipeAxis a, in PipeAxis b) =>
            a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static bool AreOpposite(in PipeAxis a, in PipeAxis b) =>
            a.IsValid && b.IsValid && Dot(a.Normalized, b.Normalized) <= -Tolerance.ParallelDot;
    }
}
