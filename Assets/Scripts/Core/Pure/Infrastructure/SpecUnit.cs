namespace KitchenDesigner.Core
{
    public enum SpecUnit
    {
        Pieces = 0,
        LinearMeters = 1,
        AreaM2 = 2,
        VolumeM3 = 3,
        Kilograms = 4,
    }

    public static class SpecUnitLabels
    {
        public static string Label(this SpecUnit unit)
        {
            switch (unit)
            {
                case SpecUnit.Pieces: return Loc.T("spec.unit.pieces");
                case SpecUnit.LinearMeters: return Loc.T("spec.unit.linearMeters");
                case SpecUnit.AreaM2: return Loc.T("spec.unit.squareMeters");
                case SpecUnit.VolumeM3: return Loc.T("spec.unit.cubicMeters");
                case SpecUnit.Kilograms: return Loc.T("spec.unit.kilograms");
                default: return "?";
            }
        }
    }
}
