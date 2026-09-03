namespace KitchenDesigner.Core
{
    public readonly struct PhotoQualityToggles
    {
        public readonly bool Shadows;
        public readonly bool SoftShadows;
        public readonly bool AntiAliasing;
        public readonly bool Supersampling;
        public readonly bool AmbientOcclusion;
        public readonly bool AoFullRes;
        public readonly bool Bloom;
        public readonly bool Vignette;
        public readonly int RenderScalePct;
        public readonly int ShadowMapPx;

        public PhotoQualityToggles(bool shadows, bool softShadows, bool antiAliasing,
            bool supersampling, bool ambientOcclusion, bool aoFullRes, bool bloom, bool vignette,
            int renderScalePct, int shadowMapPx)
        {
            Shadows = shadows;
            SoftShadows = softShadows;
            AntiAliasing = antiAliasing;
            Supersampling = supersampling;
            AmbientOcclusion = ambientOcclusion;
            AoFullRes = aoFullRes;
            Bloom = bloom;
            Vignette = vignette;
            RenderScalePct = renderScalePct;
            ShadowMapPx = shadowMapPx;
        }

        public int EnabledCount =>
            (Shadows ? 1 : 0) + (SoftShadows ? 1 : 0) + (AntiAliasing ? 1 : 0)
            + (Supersampling ? 1 : 0) + (AmbientOcclusion ? 1 : 0) + (AoFullRes ? 1 : 0)
            + (Bloom ? 1 : 0) + (Vignette ? 1 : 0);
    }

    public static class PhotoQualityPresetTable
    {
        public const PhotoQualityPreset CustomFallsBackTo = PhotoQualityPreset.High;

        public const int LOW_RENDER_SCALE_PCT = 100;
        public const int MEDIUM_RENDER_SCALE_PCT = 100;
        public const int HIGH_RENDER_SCALE_PCT = 150;

        public const int LOW_SHADOWMAP_PX = 1024;
        public const int MEDIUM_SHADOWMAP_PX = 2048;
        public const int HIGH_SHADOWMAP_PX = 4096;

        public static readonly PhotoQualityPreset[] NamedPresets =
        {
            PhotoQualityPreset.Low,
            PhotoQualityPreset.Medium,
            PhotoQualityPreset.High,
        };

        public static PhotoQualityToggles Resolve(PhotoQualityPreset preset)
        {
            switch (preset)
            {
                case PhotoQualityPreset.Low:
                    return new PhotoQualityToggles(
                        shadows: true, softShadows: false, antiAliasing: true,
                        supersampling: false, ambientOcclusion: false, aoFullRes: false,
                        bloom: false, vignette: false,
                        renderScalePct: LOW_RENDER_SCALE_PCT, shadowMapPx: LOW_SHADOWMAP_PX);
                case PhotoQualityPreset.Medium:
                    return new PhotoQualityToggles(
                        shadows: true, softShadows: true, antiAliasing: true,
                        supersampling: false, ambientOcclusion: true, aoFullRes: false,
                        bloom: true, vignette: true,
                        renderScalePct: MEDIUM_RENDER_SCALE_PCT, shadowMapPx: MEDIUM_SHADOWMAP_PX);
                case PhotoQualityPreset.High:
                case PhotoQualityPreset.Custom:
                default:
                    return new PhotoQualityToggles(
                        shadows: true, softShadows: true, antiAliasing: true,
                        supersampling: true, ambientOcclusion: true, aoFullRes: true,
                        bloom: true, vignette: true,
                        renderScalePct: HIGH_RENDER_SCALE_PCT, shadowMapPx: HIGH_SHADOWMAP_PX);
            }
        }

        public static void Apply(PhotoQualityPreset preset, KitchenSettings s)
        {
            if (s == null || preset == PhotoQualityPreset.Custom) return;
            s.PhotoQuality = preset;
        }

        internal static void ApplyToggles(PhotoQualityPreset preset, KitchenSettings s)
        {
            if (s == null || preset == PhotoQualityPreset.Custom) return;
            var t = Resolve(preset);
            s.PhotoShadows = t.Shadows;
            s.PhotoSoftShadows = t.SoftShadows;
            s.PhotoAntiAliasing = t.AntiAliasing;
            s.PhotoSupersampling = t.Supersampling;
            s.PhotoAmbientOcclusion = t.AmbientOcclusion;
            s.PhotoAoFullRes = t.AoFullRes;
            s.PhotoBloom = t.Bloom;
            s.PhotoVignette = t.Vignette;
            s.PhotoRenderScalePct = t.RenderScalePct;
            s.PhotoShadowMapPx = t.ShadowMapPx;
        }

        public static PhotoQualityPreset Detect(KitchenSettings s)
        {
            if (s == null) return PhotoQualityPreset.Custom;
            var cur = new PhotoQualityToggles(
                s.PhotoShadows, s.PhotoSoftShadows, s.PhotoAntiAliasing,
                s.PhotoSupersampling, s.PhotoAmbientOcclusion, s.PhotoAoFullRes,
                s.PhotoBloom, s.PhotoVignette,
                s.PhotoRenderScalePct, s.PhotoShadowMapPx);

            foreach (var p in NamedPresets)
                if (Equal(cur, Resolve(p))) return p;
            return PhotoQualityPreset.Custom;
        }

        private static bool Equal(PhotoQualityToggles a, PhotoQualityToggles b) =>
            a.Shadows == b.Shadows &&
            a.SoftShadows == b.SoftShadows &&
            a.AntiAliasing == b.AntiAliasing &&
            a.Supersampling == b.Supersampling &&
            a.AmbientOcclusion == b.AmbientOcclusion &&
            a.AoFullRes == b.AoFullRes &&
            a.Bloom == b.Bloom &&
            a.Vignette == b.Vignette &&
            a.RenderScalePct == b.RenderScalePct &&
            a.ShadowMapPx == b.ShadowMapPx;

        public static PhotoQualityPreset Next(PhotoQualityPreset current) => current switch
        {
            PhotoQualityPreset.Low => PhotoQualityPreset.Medium,
            PhotoQualityPreset.Medium => PhotoQualityPreset.High,
            _ => PhotoQualityPreset.Low
        };
    }
}
