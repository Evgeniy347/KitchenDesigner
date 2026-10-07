namespace KitchenDesigner.Core.MCP
{
    internal sealed class PlaceAlignSpec
    {
        public readonly string Target;
        public readonly string Axis;
        public readonly string At;
        public readonly float OffsetMm;

        public PlaceAlignSpec(string target, string axis, string at, float offsetMm)
        {
            Target = target;
            Axis = axis;
            At = at;
            OffsetMm = offsetMm;
        }
    }
}
