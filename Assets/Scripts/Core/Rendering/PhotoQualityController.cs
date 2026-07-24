using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KitchenDesigner.Core
{
    /// <summary>Применяет качество фоторежима к URP по отдельным тумблерам настроек
    /// (тени/мягкие тени, сглаживание, супер-сэмплинг, AO, bloom, vignette). Кроме
    /// вкл/выкл поднимает разрешение карты теней и правит смещения — чтобы тени были
    /// мягкими и без «лесенки». Также приглушает ambient: в фоторежиме комната должна
    /// быть тёмной, а свет идти от солнца через окна/двери и от светильников. Всё
    /// снимается при входе и откатывается при выходе; при отсутствии URP — no-op.</summary>
    public static class PhotoQualityController
    {
        private const string VolumeName = "PhotoModeVolume";

        // Плотная карта теней для чёткого мягкого края в пределах комнаты.
        private const int ShadowMapResolution = 4096;
        private const float ShadowDistanceUnits = 22f;

        private static bool _applied;
        private static GameObject? _volumeGo;

        // Снимок для восстановления.
        private static int _prevMsaa;
        private static float _prevRenderScale;
        private static float _prevShadowDistance;
        private static int _prevShadowRes = -1;
        private static AntialiasingMode _prevCamAA;
        private static AntialiasingQuality _prevCamAAQuality;
        private static bool _prevPostProcessing;
        private static LightShadows _prevSunShadows;
        private static float _prevSunShadowStrength;
        private static float _prevSunShadowBias;
        private static float _prevSunNormalBias;
        private static float _prevAmbientIntensity;
        private static AmbientMode _prevAmbientMode;
        private static Color _prevAmbientLight;
        private static Color _prevAmbientSky, _prevAmbientEquator, _prevAmbientGround;
        private static bool _ambientSnapped;

        public static bool IsApplied => _applied;

        public static void Apply()
        {
            if (_applied) Restore();

            var s = KitchenSettings.Instance;
            if (s == null) return;

            ApplyPipeline(s);
            ApplyShadows(s);
            ApplyCamera(s);
            ApplyAmbient();
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
                if (_prevShadowRes > 0) SetIntField(asset, "m_MainLightShadowmapResolution", _prevShadowRes);
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
            if (sun != null)
            {
                sun.shadows = _prevSunShadows;
                sun.shadowStrength = _prevSunShadowStrength;
                sun.shadowBias = _prevSunShadowBias;
                sun.shadowNormalBias = _prevSunNormalBias;
            }

            if (_ambientSnapped)
            {
                RenderSettings.ambientMode = _prevAmbientMode;
                RenderSettings.ambientLight = _prevAmbientLight;
                RenderSettings.ambientSkyColor = _prevAmbientSky;
                RenderSettings.ambientEquatorColor = _prevAmbientEquator;
                RenderSettings.ambientGroundColor = _prevAmbientGround;
                RenderSettings.ambientIntensity = _prevAmbientIntensity;
                _ambientSnapped = false;
            }

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

        private static void ApplyPipeline(KitchenSettings s)
        {
            var asset = GetUrpAsset();
            if (asset == null) return;

            _prevMsaa = asset.msaaSampleCount;
            _prevRenderScale = asset.renderScale;
            _prevShadowDistance = asset.shadowDistance;

            asset.msaaSampleCount = s.PhotoAntiAliasing ? 4 : 1;
            asset.renderScale = s.PhotoSupersampling ? 1.5f : 1f;
            asset.shadowDistance = ShadowDistanceUnits;

            // Разрешение карты теней — рантайм-сеттера нет, ставим полем через рефлексию.
            _prevShadowRes = GetIntField(asset, "m_MainLightShadowmapResolution");
            if (s.PhotoShadows) SetIntField(asset, "m_MainLightShadowmapResolution", ShadowMapResolution);
        }

        private static void ApplyShadows(KitchenSettings s)
        {
            var sun = SunController.Sun;
            if (sun == null) return;
            _prevSunShadows = sun.shadows;
            _prevSunShadowStrength = sun.shadowStrength;
            _prevSunShadowBias = sun.shadowBias;
            _prevSunNormalBias = sun.shadowNormalBias;

            sun.shadows = !s.PhotoShadows
                ? LightShadows.None
                : (s.PhotoSoftShadows ? LightShadows.Soft : LightShadows.Hard);
            sun.shadowStrength = 1f;
            // Малые смещения при плотной карте — убирает и «лесенку», и acne.
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.35f;
        }

        private static void ApplyCamera(KitchenSettings s)
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

        private static void ApplyAmbient()
        {
            _prevAmbientMode = RenderSettings.ambientMode;
            _prevAmbientLight = RenderSettings.ambientLight;
            _prevAmbientSky = RenderSettings.ambientSkyColor;
            _prevAmbientEquator = RenderSettings.ambientEquatorColor;
            _prevAmbientGround = RenderSettings.ambientGroundColor;
            _prevAmbientIntensity = RenderSettings.ambientIntensity;
            _ambientSnapped = true;

            // Дешёвая аппроксимация отскока света без запечки: градиентный ambient.
            // Грани, смотрящие ВНИЗ (низ полки/столешницы), берут «ground»-цвет —
            // тёплый отскок от пола/дерева, поэтому под полкой не чёрный провал.
            // Верхние грани — слабый холодный «sky». Уровень низкий: «темно значит
            // темно» сохраняется, лечится именно чёрный провал в тени.
            Color bounce = SampleFloorBounce();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.21f, 0.24f);
            RenderSettings.ambientEquatorColor = new Color(0.17f, 0.16f, 0.15f);
            RenderSettings.ambientGroundColor = bounce;
        }

        /// <summary>Цвет «отскока» снизу — тёплый оттенок пола, приглушённый.
        /// При отсутствии пола — нейтрально-тёплый дефолт.</summary>
        private static Color SampleFloorBounce()
        {
            var fallback = new Color(0.28f, 0.23f, 0.17f);
            var floor = GameObject.FindWithTag("Floor");
            var mr = floor != null ? floor.GetComponent<MeshRenderer>() : null;
            var mat = mr != null ? mr.sharedMaterial : null;
            if (mat == null) return fallback;
            try
            {
                Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                        : mat.HasProperty("_Color") ? mat.GetColor("_Color")
                        : fallback;
                // Отскок = часть альбедо пола; не ярче разумного, иначе зальёт сцену.
                return new Color(
                    Mathf.Clamp(c.r * 0.5f, 0f, 0.35f),
                    Mathf.Clamp(c.g * 0.5f, 0f, 0.32f),
                    Mathf.Clamp(c.b * 0.5f, 0f, 0.30f));
            }
            catch (Exception)
            {
                return fallback;
            }
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

        private static void ApplyAmbientOcclusion(bool enabled)
        {
            try
            {
                var asset = GetUrpAsset();
                if (asset == null) return;

                var listField = typeof(UniversalRenderPipelineAsset)
                    .GetField("m_RendererDataList",
                        BindingFlags.Instance | BindingFlags.NonPublic);
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

        // ── Reflection helpers ──────────────────────────────

        private static int GetIntField(object obj, string field)
        {
            try
            {
                var f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null && f.GetValue(obj) is int v) return v;
            }
            catch (Exception) { }
            return -1;
        }

        private static void SetIntField(object obj, string field, int value)
        {
            try
            {
                var f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                f?.SetValue(obj, value);
            }
            catch (Exception) { }
        }

        private static UniversalRenderPipelineAsset? GetUrpAsset()
        {
            return (QualitySettings.renderPipeline as UniversalRenderPipelineAsset)
                ?? (GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset);
        }
    }
}
