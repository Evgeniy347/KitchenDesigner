namespace KitchenDesigner.Core
{
    public static class EdgeBandingSpecItems
    {
        public const string Name = "Кромка";

        public static float LengthMeters(int sideLengthMM) => sideLengthMM * 0.001f;

        public static SpecItem For(string thicknessLabel, int sideLengthMM, string material) =>
            new SpecItem(SpecSections.Furniture, $"{Name} {thicknessLabel} мм", material,
                SpecUnit.LinearMeters, LengthMeters(sideLengthMM));
    }
}
