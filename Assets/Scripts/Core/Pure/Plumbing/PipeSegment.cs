namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipeSegment
    {
        public readonly string ElementId;
        public readonly PointMm AMm;
        public readonly PointMm BMm;
        public readonly float OuterDiameterMm;

        public PipeSegment(string elementId, in PointMm aMm, in PointMm bMm, float outerDiameterMm)
        {
            ElementId = elementId;
            AMm = aMm;
            BMm = bMm;
            OuterDiameterMm = outerDiameterMm;
        }

        public float LengthMm => AMm.DistanceMmTo(BMm);

        public BoxMm BoundsMm => BoxMm.Around(AMm, BMm, OuterDiameterMm * 0.5f);
    }
}
