namespace KitchenDesigner.Core
{
    public readonly struct PhotoQualityToggles
    {
        public readonly bool Shadows;
        public readonly bool SoftShadows;
        public readonly bool AntiAliasing;
        public readonly bool Supersampling;
        public readonly bool AmbientOcclusion;
        public readonly bool Bloom;
        public readonly bool Vignette;

        public PhotoQualityToggles(bool shadows, bool softShadows, bool antiAliasing,
            bool supersampling, bool ambientOcclusion, bool bloom, bool vignette)
        {
            Shadows = shadows;
            SoftShadows = softShadows;
            AntiAliasing = antiAliasing;
            Supersampling = supersampling;
            AmbientOcclusion = ambientOcclusion;
            Bloom = bloom;
            Vignette = vignette;
        }

        public int EnabledCount =>
            (Shadows ? 1 : 0) + (SoftShadows ? 1 : 0) + (AntiAliasing ? 1 : 0)
            + (Supersampling ? 1 : 0) + (AmbientOcclusion ? 1 : 0) + (Bloom ? 1 : 0)
            + (Vignette ? 1 : 0);
    }

    public static class PhotoQualityPresetTable
    {
        public const PhotoQualityPreset CustomFallsBackTo = PhotoQualityPreset.High;

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
                        supersampling: false, ambientOcclusion: false, bloom: false, vignette: false);
                case PhotoQualityPreset.Medium:
                    return new PhotoQualityToggles(
                        shadows: true, softShadows: true, antiAliasing: true,
                        supersampling: false, ambientOcclusion: true, bloom: true, vignette: true);
                case PhotoQualityPreset.High:
                case PhotoQualityPreset.Custom:
                default:
                    return new PhotoQualityToggles(
                        shadows: true, softShadows: true, antiAliasing: true,
                        supersampling: true, ambientOcclusion: true, bloom: true, vignette: true);
            }
        }

        public static void Apply(PhotoQualityPreset preset, KitchenSettings s)
        {
            if (s == null || preset == PhotoQualityPreset.Custom) return;
            var t = Resolve(preset);
            s.PhotoShadows = t.Shadows;
            s.PhotoSoftShadows = t.SoftShadows;
            s.PhotoAntiAliasing = t.AntiAliasing;
            s.PhotoSupersampling = t.Supersampling;
            s.PhotoAmbientOcclusion = t.AmbientOcclusion;
            s.PhotoBloom = t.Bloom;
            s.PhotoVignette = t.Vignette;
            s.PhotoQuality = preset;
        }

        public static PhotoQualityPreset Detect(KitchenSettings s)
        {
            if (s == null) return PhotoQualityPreset.Custom;
            var cur = new PhotoQualityToggles(
                s.PhotoShadows, s.PhotoSoftShadows, s.PhotoAntiAliasing,
                s.PhotoSupersampling, s.PhotoAmbientOcclusion, s.PhotoBloom, s.PhotoVignette);

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
            a.Bloom == b.Bloom &&
            a.Vignette == b.Vignette;

        public static PhotoQualityPreset Next(PhotoQualityPreset current) => current switch
        {
            PhotoQualityPreset.Low => PhotoQualityPreset.Medium,
            PhotoQualityPreset.Medium => PhotoQualityPreset.High,
            _ => PhotoQualityPreset.Low
        };
    }
}
