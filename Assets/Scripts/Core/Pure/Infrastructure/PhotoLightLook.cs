using System;

namespace KitchenDesigner.Core
{
    public readonly struct PhotoLightLook : IEquatable<PhotoLightLook>
    {
        public PhotoLightLook(int ambientPct, int floorBouncePct, int ambientSkyPct, int ambientEquatorPct,
            int bounceMaxPct, int tonemap, int exposurePct, int contrastPct, int saturationPct, int bloomPct,
            int bloomThresholdPct, int bloomClampPct, int vignettePct, int sunShadowStrengthPct,
            int shadowDistanceM, bool lampShadows)
        {
            AmbientPct = ambientPct;
            FloorBouncePct = floorBouncePct;
            AmbientSkyPct = ambientSkyPct;
            AmbientEquatorPct = ambientEquatorPct;
            BounceMaxPct = bounceMaxPct;
            Tonemap = tonemap;
            ExposurePct = exposurePct;
            ContrastPct = contrastPct;
            SaturationPct = saturationPct;
            BloomPct = bloomPct;
            BloomThresholdPct = bloomThresholdPct;
            BloomClampPct = bloomClampPct;
            VignettePct = vignettePct;
            SunShadowStrengthPct = sunShadowStrengthPct;
            ShadowDistanceM = shadowDistanceM;
            LampShadows = lampShadows;
        }

        public int AmbientPct { get; }
        public int FloorBouncePct { get; }
        public int AmbientSkyPct { get; }
        public int AmbientEquatorPct { get; }
        public int BounceMaxPct { get; }
        public int Tonemap { get; }
        public int ExposurePct { get; }
        public int ContrastPct { get; }
        public int SaturationPct { get; }
        public int BloomPct { get; }
        public int BloomThresholdPct { get; }
        public int BloomClampPct { get; }
        public int VignettePct { get; }
        public int SunShadowStrengthPct { get; }
        public int ShadowDistanceM { get; }
        public bool LampShadows { get; }

        public static PhotoLightLook Defaults { get; } = new PhotoLightLook(
            KitchenSettings.PHOTO_AMBIENT_DEFAULT_PCT, KitchenSettings.PHOTO_FLOOR_BOUNCE_DEFAULT_PCT,
            KitchenSettings.PHOTO_AMBIENT_SKY_DEFAULT_PCT, KitchenSettings.PHOTO_AMBIENT_EQUATOR_DEFAULT_PCT,
            KitchenSettings.PHOTO_BOUNCE_MAX_DEFAULT_PCT, KitchenSettings.PHOTO_TONEMAP_DEFAULT,
            KitchenSettings.PHOTO_EXPOSURE_DEFAULT_PCT, KitchenSettings.PHOTO_CONTRAST_DEFAULT_PCT,
            KitchenSettings.PHOTO_SATURATION_DEFAULT_PCT, KitchenSettings.PHOTO_BLOOM_DEFAULT_PCT,
            KitchenSettings.PHOTO_BLOOM_THRESHOLD_DEFAULT_PCT, KitchenSettings.PHOTO_BLOOM_CLAMP_DEFAULT_PCT,
            KitchenSettings.PHOTO_VIGNETTE_DEFAULT_PCT, KitchenSettings.PHOTO_SUN_SHADOW_DEFAULT_PCT,
            KitchenSettings.PHOTO_SHADOW_DISTANCE_DEFAULT_M, true);

        public bool IsDefault => Equals(Defaults);

        public bool Equals(PhotoLightLook other) =>
            AmbientPct == other.AmbientPct && FloorBouncePct == other.FloorBouncePct
            && AmbientSkyPct == other.AmbientSkyPct && AmbientEquatorPct == other.AmbientEquatorPct
            && BounceMaxPct == other.BounceMaxPct && Tonemap == other.Tonemap
            && ExposurePct == other.ExposurePct && ContrastPct == other.ContrastPct
            && SaturationPct == other.SaturationPct && BloomPct == other.BloomPct
            && BloomThresholdPct == other.BloomThresholdPct && BloomClampPct == other.BloomClampPct
            && VignettePct == other.VignettePct && SunShadowStrengthPct == other.SunShadowStrengthPct
            && ShadowDistanceM == other.ShadowDistanceM && LampShadows == other.LampShadows;

        public override bool Equals(object? obj) => obj is PhotoLightLook other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(AmbientPct); hash.Add(FloorBouncePct); hash.Add(AmbientSkyPct);
            hash.Add(AmbientEquatorPct); hash.Add(BounceMaxPct); hash.Add(Tonemap);
            hash.Add(ExposurePct); hash.Add(ContrastPct); hash.Add(SaturationPct); hash.Add(BloomPct);
            hash.Add(BloomThresholdPct); hash.Add(BloomClampPct); hash.Add(VignettePct);
            hash.Add(SunShadowStrengthPct); hash.Add(ShadowDistanceM); hash.Add(LampShadows);
            return hash.ToHashCode();
        }
    }
}
