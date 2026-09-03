namespace KitchenDesigner.Core
{
    public readonly struct FloorSupport
    {
        public readonly Span X;
        public readonly Span Z;
        public readonly float TopY;

        public FloorSupport(Span x, Span z, float topY)
        {
            X = x;
            Z = z;
            TopY = topY;
        }

        public bool CarriesFootprint(Span footprintX, Span footprintZ) =>
            Tolerance.IntervalsOverlap(X.Min, X.Max, footprintX.Min, footprintX.Max)
            && Tolerance.IntervalsOverlap(Z.Min, Z.Max, footprintZ.Min, footprintZ.Max);
    }
}
