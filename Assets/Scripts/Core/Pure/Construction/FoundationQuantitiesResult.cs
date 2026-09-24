namespace KitchenDesigner.Core.Construction
{
    public readonly struct FoundationQuantitiesResult
    {
        public readonly double ExcavationNaturalM3;
        public readonly double ExcavationLooseM3;
        public readonly double SandM3;
        public readonly double GravelM3;
        public readonly double ConcreteM3;
        public readonly double FormworkM2;
        public readonly double RebarKg;

        public FoundationQuantitiesResult(double excavationNaturalM3, double excavationLooseM3,
            double sandM3, double gravelM3, double concreteM3, double formworkM2, double rebarKg)
        {
            ExcavationNaturalM3 = excavationNaturalM3;
            ExcavationLooseM3 = excavationLooseM3;
            SandM3 = sandM3;
            GravelM3 = gravelM3;
            ConcreteM3 = concreteM3;
            FormworkM2 = formworkM2;
            RebarKg = rebarKg;
        }
    }
}
