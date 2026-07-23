using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KitchenDesigner.Core
{
    /// <summary>Применяет параметры качества фоторежима к URP: MSAA, render scale,
    /// дальность/мягкость теней, сглаживание камеры и пост-обработку (tonemapping,
    /// bloom, vignette, AO). Все изменения снимаются при входе и откатываются при
    /// выходе, чтобы рабочий режим оставался лёгким. Всё защищено от отсутствия URP
    /// (EditMode-тесты, batch) — в этом случае методы просто ничего не делают.</summary>
    public static class PhotoQualityController
    {
        private const string VolumeName = "PhotoModeVolume";

        private static bool _applied;
        private static GameObject? _volumeGo;

        // Снимок для восстановления.
        private static int _prevMsaa;
        private static float _prevRenderScale;
        private static float _prevShadowDistance;
        private static AntialiasingMode _prevCamAA;
        private static AntialiasingQuality _prevCamAAQuality;
        private static bool _prevPostProcessing;
        private static LightShadows _prevSunShadows;

        public static bool IsApplied => _applied;

        public static void Apply()
        {
            if (_applied) Restore();

            var s = KitchenSettings.Instance;
            if (s == null) return;
            var p = PhotoQualityPresetTable.Resolve(s.PhotoQuality);

            ApplyPipeline(p);
            ApplyShadows(s, p);
            ApplyCamera(s, p);
            ApplyPostProcessing(s);
            ApplyAmbientOcclusion(s.PhotoAmbientOcclusion);

            _applied = true;
        }

        public static void Restore()
        {
            if (!_applied) return;

            var asset = GetUrpAsset();
            if (asset != null)
            {
                asset.msaaSampleCount = _prevMsaa;
                asset.renderScale = _prevRenderScale;
                asset.shadowDistance = _prevShadowDistance;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.antialiasing = _prevCamAA;
                    data.antialiasingQuality = _prevCamAAQuality;
                    data.renderPostProcessing = _prevPostProcessing;
                }
            }

            var sun = SunController.Sun;
            if (sun != null) sun.shadows = _prevSunShadows;

            ApplyAmbientOcclusion(false);

            if (_volumeGo != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_volumeGo);
                else UnityEngine.Object.DestroyImmediate(_volumeGo);
                _volumeGo = null;
            }

            _applied = false;
        }

        // ── Pipeline (URP asset) ────────────────────────────

        private static void ApplyPipeline(PhotoQualityParams p)
        {
            var asset = GetUrpAsset();
            if (asset == null) return;

            _prevMsaa = asset.msaaSampleCount;
            _prevRenderScale = asset.renderScale;
            _prevShadowDistance = asset.shadowDistance;

            asset.msaaSampleCount = Mathf.Max(1, p.MsaaSamples);
            asset.renderScale = Mathf.Clamp(p.RenderScale, 0.5f, 2f);
            asset.shadowDistance = Mathf.Max(1f, p.ShadowDistance);
        }

        private static void ApplyShadows(KitchenSettings s, PhotoQualityParams p)
        {
            var sun = SunController.Sun;
            if (sun == null) return;
            _prevSunShadows = sun.shadows;
            sun.shadows = !s.PhotoShadows
                ? LightShadows.None
                : (p.SoftShadows ? LightShadows.Soft : LightShadows.Hard);
        }

        private static void ApplyCamera(KitchenSettings s, PhotoQualityParams p)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;

            _prevCamAA = data.antialiasing;
            _prevCamAAQuality = data.antialiasingQuality;
            _prevPostProcessing = data.renderPostProcessing;

            data.antialiasing = s.PhotoAntiAliasing
                ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderPostProcessing = true;
        }

        // ── Post-processing volume ──────────────────────────

        private static void ApplyPostProcessing(KitchenSettings s)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var tonemap = profile.Add<Tonemapping>();
            tonemap.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>();
            color.contrast.Override(8f);
            color.saturation.Override(6f);

            if (s.PhotoBloom)
            {
                var bloom = profile.Add<Bloom>();
                bloom.intensity.Override(0.35f);
                bloom.threshold.Override(1.1f);
                bloom.scatter.Override(0.6f);
            }

            if (s.PhotoVignette)
            {
                var vignette = profile.Add<Vignette>();
                vignette.intensity.Override(0.22f);
                vignette.smoothness.Override(0.4f);
            }

            _volumeGo = new GameObject(VolumeName);
            var volume = _volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.profile = profile;
        }

        // ── SSAO (renderer feature, best-effort) ────────────
        // SSAO — это ScriptableRendererFeature на рендерере. Публичного рантайм-API
        // для его переключения нет, поэтому включаем/выключаем существующую фичу
        // через рефлексию. Если она не добавлена в рендерер — тихий no-op.

        private static void ApplyAmbientOcclusion(bool enabled)
        {
            try
            {
                var asset = GetUrpAsset();
                if (asset == null) return;

                var listField = typeof(UniversalRenderPipelineAsset)
                    .GetField("m_RendererDataList",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
                if (listField?.GetValue(asset) is not ScriptableRendererData[] datas) return;

                foreach (var data in datas)
                {
                    if (data == null) continue;
                    foreach (var feature in data.rendererFeatures)
                    {
                        if (feature == null) continue;
                        if (feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
                            feature.SetActive(enabled);
                    }
                }
            }
            catch (Exception)
            {
                // Рендерер без SSAO — переключение неприменимо, это не ошибка.
            }
        }

        private static UniversalRenderPipelineAsset? GetUrpAsset()
        {
            return (QualitySettings.renderPipeline as UniversalRenderPipelineAsset)
                ?? (GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset);
        }
    }
}
