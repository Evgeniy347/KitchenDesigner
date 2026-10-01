namespace KitchenDesigner.Core
{
    public enum GrooveKind
    {
        Through = 0,
        Blind = 1,
    }

    public enum GrooveSide
    {
        Top = 0,
        Bottom = 1,
        Left = 2,
        Right = 3,
    }

    [System.Serializable]
    public struct GrooveSpec : System.IEquatable<GrooveSpec>
    {
        public GrooveKind kind;
        public GrooveSide side;

        public GrooveSpec(GrooveKind kind, GrooveSide side)
        {
            this.kind = kind;
            this.side = side;
        }

        public override string ToString() => kind + ":" + side;

        public bool Equals(GrooveSpec other) => kind == other.kind && side == other.side;

        public override bool Equals(object? obj) => obj is GrooveSpec other && Equals(other);

        public override int GetHashCode() => ((int)kind * 4) + (int)side;
    }
}
