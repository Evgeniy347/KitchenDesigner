namespace KitchenDesigner.Core.Ventilation
{
    public static class GrilleDefaults
    {
        public static readonly (int WidthMm, int HeightMm)[] Sizes =
        {
            (150, 150),
            (150, 200),
            (180, 250),
            (200, 200),
            (200, 300),
            (250, 250),
            (340, 340),
            (440, 440),
        };

        public const int DefaultWidthMm = 150;
        public const int DefaultHeightMm = 150;
        public const int MinSizeMm = 100;
        public const int MaxSizeMm = 500;
        public const int DepthMm = 20;

        public const int DefaultAirflowM3PerHour = 60;
        public const int MinAirflowM3PerHour = 10;
        public const int MaxAirflowM3PerHour = 2000;
    }
}
