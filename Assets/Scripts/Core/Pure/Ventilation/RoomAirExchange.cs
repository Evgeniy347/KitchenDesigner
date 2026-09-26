namespace KitchenDesigner.Core.Ventilation
{
    public static class RoomAirExchange
    {
        public const float MinAirChangesPerHour = 1f; // СП 60.13330.2020

        public static float RequiredM3PerHour(float roomVolumeM3) =>
            roomVolumeM3 * MinAirChangesPerHour;

        public static bool IsBelowNorm(float suppliedM3PerHour, float roomVolumeM3) =>
            suppliedM3PerHour < RequiredM3PerHour(roomVolumeM3);
    }
}
