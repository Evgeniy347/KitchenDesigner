namespace KitchenDesigner.Core
{
    public class PanelElement : KitchenElement
    {
        public const int DEFAULT_GAP_MM = PanelBody.DEFAULT_GAP_MM;

        public override bool IsFlatBoardElement => true;

        public override string DisplayTypeName => PanelBody.DISPLAY_TYPE_NAME;

        public override CutoutNeighbourRole CutoutRole => PanelBody.CUTOUT_ROLE;

        public override bool SupportsGaps => PanelBody.SUPPORTS_GAPS;

        public PanelBody Body =>
            new PanelBody(Data.DimensionsMM, Data.Gaps,
                ValidationPositionAt(transform.position), ValidationRotation);

        public void SetUniformGap(int gapMM)
        {
            var gaps = PanelBody.UniformGaps(gapMM);
            foreach (var side in GapSides.All) Data.SetGap(side, gaps.Of(side));
            ApplyDimensions();
        }
    }
}
