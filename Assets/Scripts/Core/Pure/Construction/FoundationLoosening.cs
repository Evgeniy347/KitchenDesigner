namespace KitchenDesigner.Core.Construction
{
    public static class FoundationLoosening
    {
        public const string Source =
            "ЕНиР, сборник Е2 «Земляные работы», приложение (показатели разрыхления грунтов Kp) "
            + "— конкретные числа не сверены исполнителем, см. NormativeUnverified";

        public const float SandKp = 1.10f;
        public const float SandyLoamKp = 1.14f;
        public const float LoamKp = 1.20f;
        public const float ClayKp = 1.28f;
        public const float PeatKp = 1.30f;
        public const float UnknownKp = 1.30f;

        public static bool TryCoefficient(SoilKind soil, out float kp)
        {
            switch (soil)
            {
                case SoilKind.Sand:
                    kp = SandKp;
                    return true;
                case SoilKind.SandyLoam:
                    kp = SandyLoamKp;
                    return true;
                case SoilKind.Loam:
                    kp = LoamKp;
                    return true;
                case SoilKind.Clay:
                    kp = ClayKp;
                    return true;
                case SoilKind.Peat:
                    kp = PeatKp;
                    return true;
                case SoilKind.Unknown:
                    kp = UnknownKp;
                    return true;
                default:
                    kp = 1f;
                    return false;
            }
        }
    }
}
