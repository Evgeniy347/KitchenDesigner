namespace KitchenDesigner.Core.Construction
{
    public readonly struct FoundationWallSpan
    {
        public readonly string ElementId;
        public readonly WallCentreline Centreline;
        public readonly float ThicknessMm;

        public FoundationWallSpan(string elementId, WallCentreline centreline, float thicknessMm)
        {
            ElementId = elementId;
            Centreline = centreline;
            ThicknessMm = thicknessMm;
        }
    }
}
