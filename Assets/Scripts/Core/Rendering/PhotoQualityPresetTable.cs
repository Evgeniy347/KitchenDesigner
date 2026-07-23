namespace KitchenDesigner.Core
{
    /// <summary>Числовые параметры рендера для одного пресета фоторежима.
    /// Значения подобраны так, чтобы «Высокое» комфортно работало на GTX 1060.</summary>
    public readonly struct PhotoQualityParams
    {
        /// <summary>Кол-во сэмплов MSAA: 1 (выкл), 2, 4, 8.</summary>
        public readonly int MsaaSamples;
        /// <summary>Масштаб рендера (супер-/суб-сэмплинг). 1 = нативный.</summary>
        public readonly float RenderScale;
        /// <summary>Дальность теней, юниты.</summary>
        public readonly float ShadowDistance;
        /// <summary>Число каскадов теней directional-света: 1..4.</summary>
        public readonly int ShadowCascades;
        /// <summary>Мягкие тени (PCF/склон) вместо жёстких.</summary>
        public readonly bool SoftShadows;

        public PhotoQualityParams(int msaa, float renderScale, float shadowDistance,
            int shadowCascades, bool softShadows)
        {
            MsaaSamples = msaa;
            RenderScale = renderScale;
            ShadowDistance = shadowDistance;
            ShadowCascades = shadowCascades;
            SoftShadows = softShadows;
        }
    }

    /// <summary>Таблица пресетов фоторежима. Чистая функция preset → параметры,
    /// покрывается юнит-тестами.</summary>
    public static class PhotoQualityPresetTable
    {
        public static PhotoQualityParams Resolve(PhotoQualityPreset preset)
        {
            switch (preset)
            {
                case PhotoQualityPreset.Low:
                    // Слабое железо / WebGL: без супер-сэмплинга, короткие жёсткие тени.
                    return new PhotoQualityParams(
                        msaa: 2, renderScale: 1.0f, shadowDistance: 15f,
                        shadowCascades: 2, softShadows: false);
                case PhotoQualityPreset.Medium:
                    return new PhotoQualityParams(
                        msaa: 4, renderScale: 1.0f, shadowDistance: 25f,
                        shadowCascades: 3, softShadows: true);
                case PhotoQualityPreset.High:
                default:
                    // GTX 1060: 4x MSAA + 1.5x супер-сэмплинг + мягкие тени с запасом.
                    return new PhotoQualityParams(
                        msaa: 4, renderScale: 1.5f, shadowDistance: 40f,
                        shadowCascades: 4, softShadows: true);
            }
        }
    }
}
