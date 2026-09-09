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
                case SpecUnit.Pieces: return "шт";
                case SpecUnit.LinearMeters: return "м";
                case SpecUnit.AreaM2: return "м²";
                case SpecUnit.VolumeM3: return "м³";
                case SpecUnit.Kilograms: return "кг";
                default: return "?";
            }
        }
    }
}
