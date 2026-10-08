namespace KitchenDesigner.Core.MCP
{
    internal static class PlanRender
    {
        public const int DefaultPx = 512;
        public const int MinPx = 256;
        public const int MaxPx = 2048;

        public static McpImageReply Render(DigestInput input, PlanView view, bool labels, int px)
        {
            var drawing = PlanComposer.Compose(input, view, labels, px);
            return new McpImageReply(PlanCaption.Of(drawing), PlanPng.Encode(PlanRaster.Render(drawing)));
        }

        public static string Svg(DigestInput input, PlanView view, bool labels, int px) =>
            PlanSvg.Write(PlanComposer.Compose(input, view, labels, px));
    }
}
