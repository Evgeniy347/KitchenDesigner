namespace KitchenDesigner.Core
{
    /// <summary>Набор тумблеров качества, задаваемый одним пресетом фоторежима.
    /// Пресет ничего не «прячет» — он просто выставляет эти же переключатели,
    /// которые пользователь видит и может крутить вручную.</summary>
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
    }

    /// <summary>Пресеты фоторежима как комбинации тумблеров + определение
    /// «Свои настройки». Чистые функции, покрываются юнит-тестами.</summary>
    public static class PhotoQualityPresetTable
    {
        /// <summary>Тумблеры именованного пресета. Для Custom возвращает High
        /// (как база), но применять Custom не следует — это состояние-метка.</summary>
        public static PhotoQualityToggles Resolve(PhotoQualityPreset preset)
        {
            switch (preset)
            {
                case PhotoQualityPreset.Low:
                    // Слабое железо / WebGL: жёсткие тени, без супер-сэмплинга и эффектов.
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
                    // GTX 1060: всё включено + супер-сэмплинг.
                    return new PhotoQualityToggles(
                        shadows: true, softShadows: true, antiAliasing: true,
                        supersampling: true, ambientOcclusion: true, bloom: true, vignette: true);
            }
        }

        /// <summary>Записать тумблеры пресета в настройки. Для Custom — no-op
        /// (оставляем то, что накрутил пользователь).</summary>
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

        /// <summary>Какому пресету соответствует текущая комбинация тумблеров.
        /// Если ни одному — Custom.</summary>
        public static PhotoQualityPreset Detect(KitchenSettings s)
        {
            if (s == null) return PhotoQualityPreset.Custom;
            var cur = new PhotoQualityToggles(
                s.PhotoShadows, s.PhotoSoftShadows, s.PhotoAntiAliasing,
                s.PhotoSupersampling, s.PhotoAmbientOcclusion, s.PhotoBloom, s.PhotoVignette);

            foreach (var p in new[] { PhotoQualityPreset.Low, PhotoQualityPreset.Medium, PhotoQualityPreset.High })
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

        /// <summary>Следующий именованный пресет по кругу (Low→Medium→High→Low).
        /// Из Custom переходим к Low.</summary>
        public static PhotoQualityPreset Next(PhotoQualityPreset current) => current switch
        {
            PhotoQualityPreset.Low => PhotoQualityPreset.Medium,
            PhotoQualityPreset.Medium => PhotoQualityPreset.High,
            _ => PhotoQualityPreset.Low
        };
    }
}
