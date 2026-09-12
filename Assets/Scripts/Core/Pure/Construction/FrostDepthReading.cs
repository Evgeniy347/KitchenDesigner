namespace KitchenDesigner.Core.Construction
{
    public readonly struct FrostDepthReading
    {
        public readonly float DepthMm;
        public readonly string Value;
        public readonly string Reason;
        public readonly string ReferenceStation;

        public FrostDepthReading(float depthMm, string value, string reason, string referenceStation)
        {
            DepthMm = depthMm;
            Value = value;
            Reason = reason;
            ReferenceStation = referenceStation;
        }

        public bool HasNumber => DepthMm > 0f;
    }
}
