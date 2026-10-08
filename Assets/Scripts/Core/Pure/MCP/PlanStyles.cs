namespace KitchenDesigner.Core.MCP
{
    public static class PlanStyles
    {
        private const int OpeningOutlineWidth = 2;

        public static PlanStyle Of(DigestEntry entry, PlanLayer layer, PlanView view)
        {
            if (view == PlanView.Front)
            {
                if (layer == PlanLayer.Wall)
                    return new PlanStyle(IsEdgeOn(entry, view) ? PlanShape.NoColor : PlanPalette.WallFacing, PlanPalette.WallLine, 1);
                if (layer == PlanLayer.Door) return new PlanStyle(PlanShape.NoColor, PlanPalette.DoorLine, OpeningOutlineWidth);
                if (layer == PlanLayer.Window) return new PlanStyle(PlanShape.NoColor, PlanPalette.WindowLine, OpeningOutlineWidth);
            }
            return new PlanStyle(PlanPalette.FillOf(layer), PlanPalette.Ink, 1);
        }

        public static bool IsEdgeOn(DigestEntry entry, PlanView view)
        {
            var size = entry.Box.Size;
            return view == PlanView.Front && size.x <= size.z;
        }

        public static bool LabelsInStrip(PlanLayer layer, PlanView view) =>
            view == PlanView.Front && (layer == PlanLayer.Wall || layer == PlanLayer.Floor);
    }
}
