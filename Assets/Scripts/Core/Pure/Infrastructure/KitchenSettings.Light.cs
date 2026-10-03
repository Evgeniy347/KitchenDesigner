namespace KitchenDesigner.Core
{
    public partial class KitchenSettings
    {
        public PhotoLightLook CaptureLightLook() => new PhotoLightLook(
            PhotoAmbientPct, PhotoFloorBouncePct, PhotoAmbientSkyPct, PhotoAmbientEquatorPct,
            PhotoBounceMaxPct, PhotoTonemap, PhotoExposurePct, PhotoContrastPct, PhotoSaturationPct,
            PhotoBloomPct, PhotoBloomThresholdPct, PhotoBloomClampPct, PhotoVignettePct,
            PhotoSunShadowStrengthPct, PhotoShadowDistanceM, PhotoLampShadows);

        public void ApplyLightLook(PhotoLightLook look)
        {
            PhotoAmbientPct = look.AmbientPct;
            PhotoFloorBouncePct = look.FloorBouncePct;
            PhotoAmbientSkyPct = look.AmbientSkyPct;
            PhotoAmbientEquatorPct = look.AmbientEquatorPct;
            PhotoBounceMaxPct = look.BounceMaxPct;
            PhotoTonemap = look.Tonemap;
            PhotoExposurePct = look.ExposurePct;
            PhotoContrastPct = look.ContrastPct;
            PhotoSaturationPct = look.SaturationPct;
            PhotoBloomPct = look.BloomPct;
            PhotoBloomThresholdPct = look.BloomThresholdPct;
            PhotoBloomClampPct = look.BloomClampPct;
            PhotoVignettePct = look.VignettePct;
            PhotoSunShadowStrengthPct = look.SunShadowStrengthPct;
            PhotoShadowDistanceM = look.ShadowDistanceM;
            PhotoLampShadows = look.LampShadows;
        }
    }
}
