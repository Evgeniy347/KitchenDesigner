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
                Loc.F("issue.vnt01.message", elementId, Round1(velocityMs), Round1(maxMs), TierName(tier)));

        public static ConstructionFinding ProfileMismatch(string elementId, string otherElementId,
            string profileA, string profileB) =>
            new ConstructionFinding(ConstructionFindingLevel.Error, CodeProfileMismatch, elementId,
                Loc.F("issue.vnt02.message", elementId, otherElementId, profileA, profileB), otherElementId);

        public static ConstructionFinding AirExchangeBelowNorm(string roomElementId,
            double suppliedM3PerHour, double requiredM3PerHour) =>
            new ConstructionFinding(ConstructionFindingLevel.Error, CodeAirExchange, roomElementId,
                Loc.F("issue.vnt03.message", roomElementId, Round1(suppliedM3PerHour), Round1(requiredM3PerHour), RoomAirExchange.MinAirChangesPerHour));

        private static string TierName(DuctVelocityTier tier) => tier switch
        {
            DuctVelocityTier.Main => Loc.T("vent.tier.main"),
            DuctVelocityTier.NearGrille => Loc.T("vent.tier.nearGrille"),
            _ => Loc.T("vent.tier.branch"),
        };

        private static string Round1(float value) =>
            value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        private static string Round1(double value) =>
            value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}
