using UnityEngine;

namespace KitchenDesigner.Core
{
    public class PanelElement : KitchenElement
    {

        public override string DisplayTypeName => "ДВП/ХДФ";

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.AlignsCutout;
        public const int DEFAULT_GAP_MM = 1;

        public override bool SupportsGaps => true;

        public void SetUniformGap(int gapMM)
        {
            int g = Mathf.Max(0, gapMM);
            foreach (var side in GapSides.All) Data.SetGap(side, g);
            ApplyDimensions();
        }
    }
}
