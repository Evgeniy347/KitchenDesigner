using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KitchenDesigner.Core
{
    internal readonly struct AmbientGradient
    {
        public readonly Color Sky;
        public readonly Color Equator;
        public readonly Color Ground;

        public AmbientGradient(Color sky, Color equator, Color ground)
        {
            Sky = sky;
            Equator = equator;
            Ground = ground;
        }
    }

    public static class PhotoQualityController
    {
        private const string VolumeName = "PhotoModeVolume";
        private const string SsaoFeatureTypeName = "ScreenSpaceAmbientOcclusion";
        private static readonly string SsgiFeatureTypeName = nameof(ScreenSpaceGIFeature);
        private const string MainLightShadowmapResolutionField = "m_MainLightShadowmapResolution";
        private const string RendererDataListField = "m_RendererDataList";

        private const int HighDensityShadowMapResolution = 4096;
        private const float ShadowBiasForDenseMap = 0.05f;
        private const float ShadowNormalBiasForDenseMap = 0.35f;
        private const int AntiAliasedMsaaSamples = 4;
        private const int PlainMsaaSamples = 1;
        private const float SupersampledRenderScale = 1.5f;
        private const float PlainRenderScale = 1f;
        private const float PercentToUnit = 0.01f;
        private const float BloomScatter = 0.6f;
        private const float VignetteSmoothness = 0.4f;
        private const float VolumePriorityAboveSceneVolumes = 100f;

        private const float BounceAlbedoFraction = 0.5f;
        internal static readonly Color MaxBounce = new Color(0.35f, 0.32f, 0.30f);
        internal static readonly Color NeutralWarmBounce = new Color(0.28f, 0.23f, 0.17f);
        internal static readonly Color AmbientSkyBase = new Color(0.20f, 0.21f, 0.24f);
        internal static readonly Color AmbientEquatorBase = new Color(0.17f, 0.16f, 0.15f);

        private static bool _applied;
        private static GameObject? _volumeGo;

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
            ApplyAmbient(s);
            ApplyPostProcessing(s);
            LightSourceElement.RefreshAll();
            SetRendererFeatureActive(SsaoFeatureTypeName, s.PhotoAmbientOcclusion);
            SetRendererFeatureActive(SsgiFeatureTypeName, s.PhotoSSGI);

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
                if (_prevShadowRes > 0)
                    SetPrivateIntField(asset, MainLightShadowmapResolutionField, _prevShadowRes);
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

            SetRendererFeatureActive(SsaoFeatureTypeName, false);
            SetRendererFeatureActive(SsgiFeatureTypeName, false);

            if (_volumeGo != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_volumeGo);
                else UnityEngine.Object.DestroyImmediate(_volumeGo);
                _volumeGo = null;
            }

            _applied = false;
        }

        private static void ApplyPipeline(KitchenSettings s)
        {
            var asset = GetUrpAsset();
            if (asset == null) return;

            _prevMsaa = asset.msaaSampleCount;
            _prevRenderScale = asset.renderScale;
            _prevShadowDistance = asset.shadowDistance;

            asset.msaaSampleCount = s.PhotoAntiAliasing ? AntiAliasedMsaaSamples : PlainMsaaSamples;
            asset.renderScale = s.PhotoSupersampling ? SupersampledRenderScale : PlainRenderScale;
            asset.shadowDistance = s.PhotoShadowDistanceM;

            _prevShadowRes = GetPrivateIntField(asset, MainLightShadowmapResolutionField);
            if (s.PhotoShadows)
                SetPrivateIntField(asset, MainLightShadowmapResolutionField, HighDensityShadowMapResolution);
        }

        internal static LightShadows SunShadowsFor(KitchenSettings s) =>
            !s.PhotoShadows ? LightShadows.None
            : s.PhotoSoftShadows ? LightShadows.Soft
            : LightShadows.Hard;

        private static void ApplyShadows(KitchenSettings s)
        {
            var sun = SunController.Sun;
            if (sun == null) return;
            _prevSunShadows = sun.shadows;
            _prevSunShadowStrength = sun.shadowStrength;
            _prevSunShadowBias = sun.shadowBias;
            _prevSunNormalBias = sun.shadowNormalBias;

            sun.shadows = SunShadowsFor(s);
            sun.shadowStrength = s.PhotoSunShadowStrengthPct * PercentToUnit;
            sun.shadowBias = ShadowBiasForDenseMap;
            sun.shadowNormalBias = ShadowNormalBiasForDenseMap;
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

        internal static AmbientGradient AmbientFor(KitchenSettings s)
        {
            float level = s.PhotoAmbientPct * PercentToUnit;
            Color bounce = SampleFloorBounce() * (s.PhotoFloorBouncePct * PercentToUnit);
            return new AmbientGradient(
                AmbientSkyBase * level,
                AmbientEquatorBase * level,
                bounce * level);
        }

        private static void ApplyAmbient(KitchenSettings s)
        {
            _prevAmbientMode = RenderSettings.ambientMode;
            _prevAmbientLight = RenderSettings.ambientLight;
            _prevAmbientSky = RenderSettings.ambientSkyColor;
            _prevAmbientEquator = RenderSettings.ambientEquatorColor;
            _prevAmbientGround = RenderSettings.ambientGroundColor;
            _prevAmbientIntensity = RenderSettings.ambientIntensity;
            _ambientSnapped = true;

            var gradient = AmbientFor(s);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = gradient.Sky;
            RenderSettings.ambientEquatorColor = gradient.Equator;
            RenderSettings.ambientGroundColor = gradient.Ground;
        }

        internal static Color DimmedBounceOf(Color floorAlbedo) => new Color(
            Mathf.Clamp(floorAlbedo.r * BounceAlbedoFraction, 0f, MaxBounce.r),
            Mathf.Clamp(floorAlbedo.g * BounceAlbedoFraction, 0f, MaxBounce.g),
            Mathf.Clamp(floorAlbedo.b * BounceAlbedoFraction, 0f, MaxBounce.b));

        internal static Color SampleFloorBounce()
        {
            var floor = GameObject.FindWithTag("Floor");
            var mr = floor != null ? floor.GetComponent<MeshRenderer>() : null;
            var mat = mr != null ? mr.sharedMaterial : null;
            if (mat == null) return NeutralWarmBounce;
            try
            {
                if (mat.HasProperty("_BaseColor")) return DimmedBounceOf(mat.GetColor("_BaseColor"));
                if (mat.HasProperty("_Color")) return DimmedBounceOf(mat.GetColor("_Color"));
                return NeutralWarmBounce;
            }
            catch (Exception)
            {
                return NeutralWarmBounce;
            }
        }

        private static void ApplyPostProcessing(KitchenSettings s)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var tonemap = profile.Add<Tonemapping>();
            tonemap.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(s.PhotoExposurePct * PercentToUnit);
            color.contrast.Override(s.PhotoContrastPct);
            color.saturation.Override(s.PhotoSaturationPct);

            if (s.PhotoBloom)
            {
                var bloom = profile.Add<Bloom>();
                bloom.intensity.Override(s.PhotoBloomPct * PercentToUnit);
                bloom.threshold.Override(s.PhotoBloomThresholdPct * PercentToUnit);
                bloom.scatter.Override(BloomScatter);
            }

            if (s.PhotoVignette)
            {
                var vignette = profile.Add<Vignette>();
                vignette.intensity.Override(s.PhotoVignettePct * PercentToUnit);
                vignette.smoothness.Override(VignetteSmoothness);
            }

            _volumeGo = new GameObject(VolumeName);
            var volume = _volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = VolumePriorityAboveSceneVolumes;
            volume.profile = profile;
        }

        internal static void SetRendererFeatureActive(string typeNameContains, bool enabled)
        {
            try
            {
                var asset = GetUrpAsset();
                if (asset == null) return;

                var listField = typeof(UniversalRenderPipelineAsset)
                    .GetField(RendererDataListField, BindingFlags.Instance | BindingFlags.NonPublic);
                if (listField?.GetValue(asset) is not ScriptableRendererData[] datas) return;

                foreach (var data in datas)
                {
                    if (data == null) continue;
                    foreach (var feature in data.rendererFeatures)
                    {
                        if (feature == null) continue;
                        if (feature.GetType().Name.Contains(typeNameContains))
                            feature.SetActive(enabled);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static int GetPrivateIntField(object obj, string field)
        {
            try
            {
                var f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null && f.GetValue(obj) is int v) return v;
            }
            catch (Exception) { }
            return -1;
        }

        private static void SetPrivateIntField(object obj, string field, int value)
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
