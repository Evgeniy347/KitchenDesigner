namespace KitchenDesigner.Core
{
    public static class EdgeBandingSpecItems
    {
        public static string Name => Loc.T("spec.item.edgeBanding");

        public static float LengthMeters(int sideLengthMM) => sideLengthMM * 0.001f;

        public static SpecItem For(string thicknessLabel, int sideLengthMM, string material) =>
            new SpecItem(SpecSections.Furniture, Loc.F("spec.item.edgeBandingSized", Name, thicknessLabel), material,
                SpecUnit.LinearMeters, LengthMeters(sideLengthMM));
    }
}
