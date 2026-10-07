namespace KitchenDesigner.Core.MCP
{
    internal sealed class PlaceAgainstSpec
    {
        public readonly string Target;
        public readonly string Face;
        public readonly float GapMm;

        public PlaceAgainstSpec(string target, string face, float gapMm)
        {
            Target = target;
            Face = face;
            GapMm = gapMm;
        }
    }
}
