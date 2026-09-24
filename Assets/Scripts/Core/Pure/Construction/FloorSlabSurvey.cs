namespace KitchenDesigner.Core.Construction
{
    public readonly struct FloorSlabSurvey
    {
        public readonly string ElementId;
        public readonly float GapToWallMm;

        public FloorSlabSurvey(string elementId, float gapToWallMm)
        {
            ElementId = elementId;
            GapToWallMm = gapToWallMm;
        }
    }
}
