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
        MountFaceLeftTheTarget,
        BeyondThreshold,
        NoOverlap,
        OverlapTooSmall,
        LandsInsideNeighbour,
        AlreadyInPlace,
    }
}
