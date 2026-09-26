namespace KitchenDesigner.Core.Ventilation
{
    public enum DuctVelocityTier
    {
        Main,
        Branch,
        NearGrille,
    }

    public static class DuctVelocity
    {
        public const float MainMaxMs = 8f; // СП 60.13330.2020
        public const float BranchMaxMs = 6f; // СП 60.13330.2020
        public const float NearGrilleMaxMs = 2.5f; // СП 60.13330.2020

        public static float MaxRecommendedMs(DuctVelocityTier tier) => tier switch
        {
            DuctVelocityTier.Main => MainMaxMs,
            DuctVelocityTier.NearGrille => NearGrilleMaxMs,
            _ => BranchMaxMs,
        };

        public static float CrossSectionAreaM2(in DuctProfile profile)
        {
            if (profile.Kind == DuctProfileKind.Round)
            {
                float radiusM = profile.DiameterMm * 0.001f * 0.5f;
                return (float)System.Math.PI * radiusM * radiusM;
            }

            return (profile.WidthMm * 0.001f) * (profile.HeightMm * 0.001f);
        }

        public static float MetresPerSecond(float airflowM3PerHour, float crossSectionAreaM2)
        {
            if (crossSectionAreaM2 <= 0f) return 0f;
            return (airflowM3PerHour / 3600f) / crossSectionAreaM2;
        }
    }
}
