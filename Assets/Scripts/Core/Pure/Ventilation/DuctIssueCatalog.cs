using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core.Ventilation
{
    public static class DuctIssueCatalog
    {
        public const string CodeVelocity = "VNT-01";
        public const string CodeProfileMismatch = "VNT-02";
        public const string CodeAirExchange = "VNT-03";

        public static ConstructionFinding VelocityExceeded(string elementId, float velocityMs,
            float maxMs, DuctVelocityTier tier) =>
            new ConstructionFinding(ConstructionFindingLevel.Warning, CodeVelocity, elementId,
                $"Воздуховод «{elementId}»: скорость воздуха {Round1(velocityMs)} м/с превышает "
                + $"рекомендуемую {Round1(maxMs)} м/с ({TierName(tier)})");

        public static ConstructionFinding ProfileMismatch(string elementId, string otherElementId,
            string profileA, string profileB) =>
            new ConstructionFinding(ConstructionFindingLevel.Error, CodeProfileMismatch, elementId,
                $"Стык воздуховодов «{elementId}» ↔ «{otherElementId}»: сечения не совпадают "
                + $"({profileA} и {profileB}) — нужен переход", otherElementId);

        public static ConstructionFinding AirExchangeBelowNorm(string roomElementId,
            double suppliedM3PerHour, double requiredM3PerHour) =>
            new ConstructionFinding(ConstructionFindingLevel.Error, CodeAirExchange, roomElementId,
                $"Воздухообмен помещения «{roomElementId}»: решётки дают "
                + $"{Round1(suppliedM3PerHour)} м³/ч, требуется не менее "
                + $"{Round1(requiredM3PerHour)} м³/ч (кратность {RoomAirExchange.MinAirChangesPerHour} "
                + "об/ч)");

        private static string TierName(DuctVelocityTier tier) => tier switch
        {
            DuctVelocityTier.Main => "магистраль",
            DuctVelocityTier.NearGrille => "перед решёткой",
            _ => "ответвление",
        };

        private static string Round1(float value) =>
            value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        private static string Round1(double value) =>
            value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}
