namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipeSize
    {
        public readonly string Id;
        public readonly string Designation;
        public readonly int NominalBoreMm;
        public readonly float OuterDiameterMm;
        public readonly float WallThicknessMm;

        public PipeSize(string id, string designation, int nominalBoreMm,
            float outerDiameterMm, float wallThicknessMm)
        {
            Id = id;
            Designation = designation;
            NominalBoreMm = nominalBoreMm;
            OuterDiameterMm = outerDiameterMm;
            WallThicknessMm = wallThicknessMm;
        }

        public float InnerDiameterMm => OuterDiameterMm - 2f * WallThicknessMm;
    }
}
