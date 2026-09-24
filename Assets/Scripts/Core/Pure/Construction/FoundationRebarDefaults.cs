namespace KitchenDesigner.Core.Construction
{
    public static class FoundationRebarDefaults
    {
        public const string CoverSource =
            "СП 63.13330.2018, п. 10.3.2 — защитный слой при наличии бетонной подготовки "
            + "(пункт не подтверждён исполнителем, см. NormativeUnverified)";

        public const int DiameterMm = 12;
        public const int MinDiameterMm = 6;
        public const int MaxDiameterMm = 40;

        public const int StepMm = 300;
        public const int MinStepMm = 50;
        public const int MaxStepMm = 500;

        public const int CoverMm = 40;
        public const int MinCoverMm = 0;
        public const int MaxCoverMm = 100;
    }
}
