namespace KitchenDesigner.Core
{
    public enum EdgeSide
    {
        L1,
        L2,
        W1,
        W2,
    }

    public enum EdgeSideState
    {
        Auto,
        Forced,
        Suppressed,
    }

    public static class EdgeManual
    {
        public static int Bit(EdgeSide side) => 1 << (int)side;

        public const int AllMask = 0b1111;

        public static bool Has(int mask, EdgeSide side) => (mask & Bit(side)) != 0;

        public static int With(int mask, EdgeSide side, bool manual) =>
            manual ? mask | Bit(side) : mask & ~Bit(side);
    }

    public static class EdgeStates
    {
        public const int Unmigrated = -1;

        public static readonly EdgeSide[] All =
        {
            EdgeSide.L1, EdgeSide.L2, EdgeSide.W1, EdgeSide.W2,
        };

        public static EdgeSideState Of(int forcedMask, int suppressedMask, EdgeSide side) =>
            EdgeManual.Has(suppressedMask, side) ? EdgeSideState.Suppressed
            : EdgeManual.Has(forcedMask, side) ? EdgeSideState.Forced
            : EdgeSideState.Auto;

        public static EdgeSideState Next(EdgeSideState state) => state switch
        {
            EdgeSideState.Forced => EdgeSideState.Auto,
            EdgeSideState.Auto => EdgeSideState.Suppressed,
            _ => EdgeSideState.Forced,
        };

        public static bool HasEdge(EdgeSideState state, bool autoHasEdge) => state switch
        {
            EdgeSideState.Forced => true,
            EdgeSideState.Suppressed => false,
            _ => autoHasEdge,
        };

        public static bool IsExplicit(EdgeSideState state) => state != EdgeSideState.Auto;

        public static int ForcedMaskWith(int forcedMask, EdgeSide side, EdgeSideState state) =>
            EdgeManual.With(forcedMask, side, state == EdgeSideState.Forced);

        public static int SuppressedMaskWith(int suppressedMask, EdgeSide side, EdgeSideState state) =>
            EdgeManual.With(suppressedMask, side, state == EdgeSideState.Suppressed);
    }
}
