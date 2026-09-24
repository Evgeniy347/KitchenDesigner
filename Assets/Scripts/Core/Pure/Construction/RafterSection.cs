namespace KitchenDesigner.Core.Construction
{
    public readonly struct RafterSection
    {
        public readonly float MaxSpanMm;
        public readonly float WidthMm;
        public readonly float HeightMm;
        public readonly string Source;

        public RafterSection(float maxSpanMm, float widthMm, float heightMm, string source)
        {
            MaxSpanMm = maxSpanMm;
            WidthMm = widthMm;
            HeightMm = heightMm;
            Source = source;
        }
    }
}
