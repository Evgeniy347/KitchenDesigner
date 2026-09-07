namespace KitchenDesigner.Core
{
    public enum SnapPairRejection
    {
        None = 0,
        SupersededByGrooveSeat,
        OffTheMountAxis,
        NotParallel,
        CoDirectionalGrooveSeat,
        CoDirectionalMount,
        BeyondThreshold,
        NoOverlap,
        OverlapTooSmall,
        LandsInsideNeighbour,
        AlreadyInPlace,
    }
}
