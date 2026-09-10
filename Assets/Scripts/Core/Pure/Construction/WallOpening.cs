namespace KitchenDesigner.Core.Construction
{
    public readonly struct WallOpening
    {
        public readonly string ElementId;
        public readonly float WidthMm;
        public readonly float HeightMm;

        public WallOpening(string elementId, float widthMm, float heightMm)
        {
            ElementId = elementId;
            WidthMm = widthMm;
            HeightMm = heightMm;
        }
    }
}
