namespace KitchenDesigner.Core.Ventilation
{
    public static class RoomAirExchange
    {
        public const double MinAirChangesPerHour = 1d; // СП 60.13330.2020

        public static double RequiredM3PerHour(double roomVolumeM3) =>
            roomVolumeM3 * MinAirChangesPerHour;

        public static bool IsBelowNorm(double suppliedM3PerHour, double roomVolumeM3) =>
            suppliedM3PerHour < RequiredM3PerHour(roomVolumeM3);
    }
}
