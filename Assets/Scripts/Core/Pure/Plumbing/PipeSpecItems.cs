namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeSpecItems
    {
        public static float LengthMeters(int lengthMM) => lengthMM * 0.001f;

        public static string PipeLineName(PipeSize size) => $"Труба ДН{size.NominalBoreMm}";

        public static SpecItem PipeLine(PipeSize size, int lengthMM) =>
            new SpecItem(SpecSections.Plumbing, PipeLineName(size), "",
                SpecUnit.LinearMeters, LengthMeters(lengthMM));

        public static string FittingLineName(string title, PipeSize bore) => $"{title} ДН{bore.NominalBoreMm}";

        public static SpecItem FittingLine(string title, PipeSize bore) =>
            new SpecItem(SpecSections.Plumbing, FittingLineName(title, bore), "",
                SpecUnit.Pieces, 1f);
    }
}
