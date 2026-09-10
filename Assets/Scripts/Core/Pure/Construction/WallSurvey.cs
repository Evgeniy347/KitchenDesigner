namespace KitchenDesigner.Core.Construction
{
    public readonly struct WallSurvey
    {
        public readonly string ElementId;
        public readonly MasonryTechnology Technology;
        public readonly float ThicknessMm;
        public readonly float JointMm;

        public WallSurvey(string elementId, MasonryTechnology technology,
            float thicknessMm, float jointMm)
        {
            ElementId = elementId;
            Technology = technology;
            ThicknessMm = thicknessMm;
            JointMm = jointMm;
        }
    }
}
