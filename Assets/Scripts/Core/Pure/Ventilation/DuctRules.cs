using System.Collections.Generic;
using KitchenDesigner.Core.Construction;
using KitchenDesigner.Core.Ports;

namespace KitchenDesigner.Core.Ventilation
{
    public static class DuctRules
    {
        public static IReadOnlyList<ConstructionFinding> Collect(DuctSurvey survey)
        {
            var findings = new List<ConstructionFinding>();
            CollectProfileMismatches(survey, findings);
            CollectVelocity(survey, findings);
            CollectAirExchange(survey, findings);
            return findings;
        }

        private static void CollectProfileMismatches(DuctSurvey survey,
            List<ConstructionFinding> findings)
        {
            foreach (var link in survey.Network.Links)
            {
                var a = survey.Ports[link.APortIndex];
                var b = survey.Ports[link.BPortIndex];
                if (PortJoint.ProfilesCompatible(a.ProfileId, b.ProfileId)) continue;
                findings.Add(DuctIssueCatalog.ProfileMismatch(a.ElementId, b.ElementId,
                    a.ProfileId ?? "?", b.ProfileId ?? "?"));
            }
        }

        private static void CollectVelocity(DuctSurvey survey, List<ConstructionFinding> findings)
        {
            foreach (var duct in survey.Ducts)
            {
                float area = DuctVelocity.CrossSectionAreaM2(duct.Profile);
                float velocity = DuctVelocity.MetresPerSecond(duct.AirflowM3PerHour, area);
                var tier = TierOf(survey, duct);
                float max = DuctVelocity.MaxRecommendedMs(tier);
                if (velocity <= max) continue;
                findings.Add(DuctIssueCatalog.VelocityExceeded(duct.ElementId, velocity, max, tier));
            }
        }

        private static DuctVelocityTier TierOf(DuctSurvey survey, in DuctRun duct)
        {
            bool aFree = survey.Network.IsFree(duct.PortAIndex);
            bool bFree = survey.Network.IsFree(duct.PortBIndex);

            bool nearGrille =
                (aFree && NearAnyGrille(survey, survey.Ports[duct.PortAIndex]))
                || (bFree && NearAnyGrille(survey, survey.Ports[duct.PortBIndex]));
            if (nearGrille) return DuctVelocityTier.NearGrille;

            int connectedCount = (aFree ? 0 : 1) + (bFree ? 0 : 1);
            return connectedCount >= 2 ? DuctVelocityTier.Main : DuctVelocityTier.Branch;
        }

        private static bool NearAnyGrille(DuctSurvey survey, in Port freePort)
        {
            foreach (var grille in survey.Grilles)
                if (freePort.PositionMm.DistanceMmTo(grille.PositionMm) <= PortJoint.JoinToleranceMm)
                    return true;
            return false;
        }

        private static void CollectAirExchange(DuctSurvey survey, List<ConstructionFinding> findings)
        {
            if (survey.Ducts.Count == 0 && survey.Grilles.Count == 0) return;

            foreach (var room in survey.Rooms)
            {
                double suppliedM3PerHour = 0d;
                foreach (var grille in survey.Grilles)
                    if (room.Contains(grille.PositionMm.XMm, grille.PositionMm.ZMm))
                        suppliedM3PerHour += grille.AirflowM3PerHour;

                double volumeM3 = room.VolumeM3;
                if (!RoomAirExchange.IsBelowNorm(suppliedM3PerHour, volumeM3)) continue;

                findings.Add(DuctIssueCatalog.AirExchangeBelowNorm(room.ElementId, suppliedM3PerHour,
                    RoomAirExchange.RequiredM3PerHour(volumeM3)));
            }
        }
    }
}
