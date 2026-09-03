using UnityEngine;

namespace KitchenDesigner.Core
{
    public partial class KitchenSettings
    {
        public const int PHOTO_RENDER_SCALE_DEFAULT_PCT = 150;
        public const int PHOTO_RENDER_SCALE_MIN_PCT = 100;
        public const int PHOTO_RENDER_SCALE_MAX_PCT = 300;
        public const int PHOTO_SHADOWMAP_DEFAULT_PX = 4096;
        public const int PHOTO_SHADOWMAP_MIN_PX = 512;
        public const int PHOTO_SHADOWMAP_MAX_PX = 8192;
        public const int PHOTO_LIGHTS_PER_OBJECT_DEFAULT = 8;
        public const int PHOTO_LIGHTS_PER_OBJECT_MAX = 8;

        public const int PHOTO_AMBIENT_SKY_DEFAULT_PCT = 200;
        public const int PHOTO_AMBIENT_EQUATOR_DEFAULT_PCT = 280;
        public const int PHOTO_BOUNCE_MAX_DEFAULT_PCT = 400;
        public const int PHOTO_AMBIENT_PART_MAX_PCT = 400;

        public const int PHOTO_TONEMAP_NONE = 0;
        public const int PHOTO_TONEMAP_NEUTRAL = 1;
        public const int PHOTO_TONEMAP_ACES = 2;
        public const int PHOTO_TONEMAP_DEFAULT = PHOTO_TONEMAP_NEUTRAL;

        public const int PHOTO_BLOOM_CLAMP_DEFAULT_PCT = 400;
        public const int PHOTO_BLOOM_CLAMP_MIN_PCT = 100;
        public const int PHOTO_BLOOM_CLAMP_MAX_PCT = 20000;

        public const int PHOTO_AO_INTENSITY_DEFAULT_PCT = 250;
        public const int PHOTO_AO_INTENSITY_MAX_PCT = 500;
        public const int PHOTO_AO_RADIUS_DEFAULT_MM = 50;
        public const int PHOTO_AO_RADIUS_MIN_MM = 10;
        public const int PHOTO_AO_RADIUS_MAX_MM = 1000;
        public const int PHOTO_AO_DIRECT_DEFAULT_PCT = 10;
        public const int PHOTO_AO_FALLOFF_DEFAULT_M = 4;
        public const int PHOTO_AO_FALLOFF_MIN_M = 1;
        public const int PHOTO_AO_FALLOFF_MAX_M = 100;

        public const int PHOTO_SSGI_STRENGTH_DEFAULT_PCT = 70;
        public const int PHOTO_SSGI_STRENGTH_MAX_PCT = 300;
        public const int PHOTO_SSGI_RADIUS_DEFAULT_MM = 800;
        public const int PHOTO_SSGI_RADIUS_MIN_MM = 50;
        public const int PHOTO_SSGI_RADIUS_MAX_MM = 3000;
        public const int PHOTO_SSGI_SAMPLES_DEFAULT = 32;
        public const int PHOTO_SSGI_SAMPLES_MIN = 4;
        public const int PHOTO_SSGI_SAMPLES_MAX = 64;
        public const int PHOTO_SSGI_RESOLUTION_DEFAULT_PCT = 50;
        public const int PHOTO_SSGI_RESOLUTION_MIN_PCT = 25;
        public const int PHOTO_SSGI_RESOLUTION_MAX_PCT = 100;
        public const int PHOTO_SSGI_BLUR_DEFAULT_PX = 3;
        public const int PHOTO_SSGI_BLUR_MAX_PX = 6;

        [SerializeField] private bool _photoHdr = true;
        [SerializeField] private int _photoRenderScalePct = PHOTO_RENDER_SCALE_DEFAULT_PCT;
        [SerializeField] private int _photoShadowMapPx = PHOTO_SHADOWMAP_DEFAULT_PX;
        [SerializeField] private int _photoLightsPerObject = PHOTO_LIGHTS_PER_OBJECT_DEFAULT;

        [SerializeField] private int _photoAmbientSkyPct = PHOTO_AMBIENT_SKY_DEFAULT_PCT;
        [SerializeField] private int _photoAmbientEquatorPct = PHOTO_AMBIENT_EQUATOR_DEFAULT_PCT;
        [SerializeField] private int _photoBounceMaxPct = PHOTO_BOUNCE_MAX_DEFAULT_PCT;

        [SerializeField] private int _photoBloomClampPct = PHOTO_BLOOM_CLAMP_DEFAULT_PCT;
        [SerializeField] private int _photoTonemap = PHOTO_TONEMAP_DEFAULT;
        [SerializeField] private bool _photoAoFullRes = true;

        [SerializeField] private int _photoAoIntensityPct = PHOTO_AO_INTENSITY_DEFAULT_PCT;
        [SerializeField] private int _photoAoRadiusMM = PHOTO_AO_RADIUS_DEFAULT_MM;
        [SerializeField] private int _photoAoDirectPct = PHOTO_AO_DIRECT_DEFAULT_PCT;
        [SerializeField] private int _photoAoFalloffM = PHOTO_AO_FALLOFF_DEFAULT_M;

        [SerializeField] private int _photoSsgiStrengthPct = PHOTO_SSGI_STRENGTH_DEFAULT_PCT;
        [SerializeField] private int _photoSsgiRadiusMM = PHOTO_SSGI_RADIUS_DEFAULT_MM;
        [SerializeField] private int _photoSsgiSamples = PHOTO_SSGI_SAMPLES_DEFAULT;
        [SerializeField] private int _photoSsgiResolutionPct = PHOTO_SSGI_RESOLUTION_DEFAULT_PCT;
        [SerializeField] private int _photoSsgiBlurPx = PHOTO_SSGI_BLUR_DEFAULT_PX;

        public bool PhotoHdr
        {
            get => _photoHdr;
            set => _photoHdr = value;
        }

        public int PhotoRenderScalePct
        {
            get => _photoRenderScalePct;
            set => _photoRenderScalePct = Mathf.Clamp(value, PHOTO_RENDER_SCALE_MIN_PCT, PHOTO_RENDER_SCALE_MAX_PCT);
        }

        public int PhotoShadowMapPx
        {
            get => _photoShadowMapPx;
            set => _photoShadowMapPx = Mathf.Clamp(value, PHOTO_SHADOWMAP_MIN_PX, PHOTO_SHADOWMAP_MAX_PX);
        }

        public int PhotoLightsPerObject
        {
            get => _photoLightsPerObject;
            set => _photoLightsPerObject = Mathf.Clamp(value, 1, PHOTO_LIGHTS_PER_OBJECT_MAX);
        }

        public int PhotoAmbientSkyPct
        {
            get => _photoAmbientSkyPct;
            set => _photoAmbientSkyPct = Mathf.Clamp(value, 0, PHOTO_AMBIENT_PART_MAX_PCT);
        }

        public int PhotoAmbientEquatorPct
        {
            get => _photoAmbientEquatorPct;
            set => _photoAmbientEquatorPct = Mathf.Clamp(value, 0, PHOTO_AMBIENT_PART_MAX_PCT);
        }

        public int PhotoBounceMaxPct
        {
            get => _photoBounceMaxPct;
            set => _photoBounceMaxPct = Mathf.Clamp(value, 0, PHOTO_AMBIENT_PART_MAX_PCT);
        }

        public int PhotoBloomClampPct
        {
            get => _photoBloomClampPct;
            set => _photoBloomClampPct = Mathf.Clamp(value, PHOTO_BLOOM_CLAMP_MIN_PCT, PHOTO_BLOOM_CLAMP_MAX_PCT);
        }

        public int PhotoTonemap
        {
            get => _photoTonemap;
            set => _photoTonemap = Mathf.Clamp(value, PHOTO_TONEMAP_NONE, PHOTO_TONEMAP_ACES);
        }

        public bool PhotoAoFullRes
        {
            get => _photoAoFullRes;
            set => _photoAoFullRes = value;
        }

        public int PhotoAoIntensityPct
        {
            get => _photoAoIntensityPct;
            set => _photoAoIntensityPct = Mathf.Clamp(value, 0, PHOTO_AO_INTENSITY_MAX_PCT);
        }

        public int PhotoAoRadiusMM
        {
            get => _photoAoRadiusMM;
            set => _photoAoRadiusMM = Mathf.Clamp(value, PHOTO_AO_RADIUS_MIN_MM, PHOTO_AO_RADIUS_MAX_MM);
        }

        public int PhotoAoDirectPct
        {
            get => _photoAoDirectPct;
            set => _photoAoDirectPct = Mathf.Clamp(value, 0, 100);
        }

        public int PhotoAoFalloffM
        {
            get => _photoAoFalloffM;
            set => _photoAoFalloffM = Mathf.Clamp(value, PHOTO_AO_FALLOFF_MIN_M, PHOTO_AO_FALLOFF_MAX_M);
        }

        public int PhotoSsgiStrengthPct
        {
            get => _photoSsgiStrengthPct;
            set => _photoSsgiStrengthPct = Mathf.Clamp(value, 0, PHOTO_SSGI_STRENGTH_MAX_PCT);
        }

        public int PhotoSsgiRadiusMM
        {
            get => _photoSsgiRadiusMM;
            set => _photoSsgiRadiusMM = Mathf.Clamp(value, PHOTO_SSGI_RADIUS_MIN_MM, PHOTO_SSGI_RADIUS_MAX_MM);
        }

        public int PhotoSsgiSamples
        {
            get => _photoSsgiSamples;
            set => _photoSsgiSamples = Mathf.Clamp(value, PHOTO_SSGI_SAMPLES_MIN, PHOTO_SSGI_SAMPLES_MAX);
        }

        public int PhotoSsgiResolutionPct
        {
            get => _photoSsgiResolutionPct;
            set => _photoSsgiResolutionPct = Mathf.Clamp(value, PHOTO_SSGI_RESOLUTION_MIN_PCT, PHOTO_SSGI_RESOLUTION_MAX_PCT);
        }

        public int PhotoSsgiBlurPx
        {
            get => _photoSsgiBlurPx;
            set => _photoSsgiBlurPx = Mathf.Clamp(value, 0, PHOTO_SSGI_BLUR_MAX_PX);
        }

        internal void ResetPhotoLook()
        {
            _photoQuality = PhotoQualityPreset.High;
            _photoShadows = true;
            _photoSoftShadows = true;
            _photoAntiAliasing = true;
            _photoSupersampling = true;
            _photoAmbientOcclusion = true;
            _photoBloom = true;
            _photoVignette = true;
            _photoCeiling = true;
            _photoSSGI = false;
            _photoLampShadows = true;
            _photoAmbientPct = PHOTO_AMBIENT_DEFAULT_PCT;
            _photoFloorBouncePct = PHOTO_FLOOR_BOUNCE_DEFAULT_PCT;
            _photoExposurePct = PHOTO_EXPOSURE_DEFAULT_PCT;
            _photoContrastPct = PHOTO_CONTRAST_DEFAULT_PCT;
            _photoSaturationPct = PHOTO_SATURATION_DEFAULT_PCT;
            _photoBloomPct = PHOTO_BLOOM_DEFAULT_PCT;
            _photoBloomThresholdPct = PHOTO_BLOOM_THRESHOLD_DEFAULT_PCT;
            _photoVignettePct = PHOTO_VIGNETTE_DEFAULT_PCT;
            _photoSunShadowStrengthPct = PHOTO_SUN_SHADOW_DEFAULT_PCT;
            _photoShadowDistanceM = PHOTO_SHADOW_DISTANCE_DEFAULT_M;
            ResetPhotoTuning();
        }

        public bool PhotoLookMigrated { get; private set; }

        private void ApplyPhotoSettings(KitchenSettingsData data)
        {
            PhotoLookMigrated = data.photoSchema < KitchenSettingsData.CURRENT_PHOTO_SCHEMA;
            if (PhotoLookMigrated)
            {
                ResetPhotoLook();
                return;
            }

            _photoQuality = (PhotoQualityPreset)Mathf.Clamp(data.photoQuality, 0, 3);
            _photoShadows = data.photoShadows;
            _photoSoftShadows = data.photoSoftShadows;
            _photoAntiAliasing = data.photoAntiAliasing;
            _photoSupersampling = data.photoSupersampling;
            _photoAmbientOcclusion = data.photoAmbientOcclusion;
            _photoBloom = data.photoBloom;
            _photoVignette = data.photoVignette;
            _photoCeiling = data.photoCeiling;
            _photoSSGI = data.photoSSGI;
            _photoLampShadows = data.photoLampShadows;
            PhotoAmbientPct = data.photoAmbientPct;
            PhotoFloorBouncePct = data.photoFloorBouncePct;
            PhotoExposurePct = data.photoExposurePct;
            PhotoContrastPct = data.photoContrastPct;
            PhotoSaturationPct = data.photoSaturationPct;
            PhotoBloomPct = data.photoBloomPct;
            PhotoBloomThresholdPct = data.photoBloomThresholdPct;
            PhotoVignettePct = data.photoVignettePct;
            PhotoSunShadowStrengthPct = data.photoSunShadowStrengthPct;
            PhotoShadowDistanceM = data.photoShadowDistanceM;
            ApplyPhotoTuning(data);
        }

        private void ResetPhotoTuning()
        {
            _photoHdr = true;
            _photoRenderScalePct = PHOTO_RENDER_SCALE_DEFAULT_PCT;
            _photoShadowMapPx = PHOTO_SHADOWMAP_DEFAULT_PX;
            _photoLightsPerObject = PHOTO_LIGHTS_PER_OBJECT_DEFAULT;
            _photoAmbientSkyPct = PHOTO_AMBIENT_SKY_DEFAULT_PCT;
            _photoAmbientEquatorPct = PHOTO_AMBIENT_EQUATOR_DEFAULT_PCT;
            _photoBounceMaxPct = PHOTO_BOUNCE_MAX_DEFAULT_PCT;
            _photoBloomClampPct = PHOTO_BLOOM_CLAMP_DEFAULT_PCT;
            _photoTonemap = PHOTO_TONEMAP_DEFAULT;
            _photoAoFullRes = true;
            _photoAoIntensityPct = PHOTO_AO_INTENSITY_DEFAULT_PCT;
            _photoAoRadiusMM = PHOTO_AO_RADIUS_DEFAULT_MM;
            _photoAoDirectPct = PHOTO_AO_DIRECT_DEFAULT_PCT;
            _photoAoFalloffM = PHOTO_AO_FALLOFF_DEFAULT_M;
            _photoSsgiStrengthPct = PHOTO_SSGI_STRENGTH_DEFAULT_PCT;
            _photoSsgiRadiusMM = PHOTO_SSGI_RADIUS_DEFAULT_MM;
            _photoSsgiSamples = PHOTO_SSGI_SAMPLES_DEFAULT;
            _photoSsgiResolutionPct = PHOTO_SSGI_RESOLUTION_DEFAULT_PCT;
            _photoSsgiBlurPx = PHOTO_SSGI_BLUR_DEFAULT_PX;
        }

        private void CapturePhotoTuning(KitchenSettingsData data)
        {
            data.photoSchema = KitchenSettingsData.CURRENT_PHOTO_SCHEMA;
            data.photoHdr = _photoHdr;
            data.photoRenderScalePct = _photoRenderScalePct;
            data.photoShadowMapPx = _photoShadowMapPx;
            data.photoLightsPerObject = _photoLightsPerObject;
            data.photoAmbientSkyPct = _photoAmbientSkyPct;
            data.photoAmbientEquatorPct = _photoAmbientEquatorPct;
            data.photoBounceMaxPct = _photoBounceMaxPct;
            data.photoBloomClampPct = _photoBloomClampPct;
            data.photoTonemap = _photoTonemap;
            data.photoAoFullRes = _photoAoFullRes;
            data.photoAoIntensityPct = _photoAoIntensityPct;
            data.photoAoRadiusMM = _photoAoRadiusMM;
            data.photoAoDirectPct = _photoAoDirectPct;
            data.photoAoFalloffM = _photoAoFalloffM;
            data.photoSsgiStrengthPct = _photoSsgiStrengthPct;
            data.photoSsgiRadiusMM = _photoSsgiRadiusMM;
            data.photoSsgiSamples = _photoSsgiSamples;
            data.photoSsgiResolutionPct = _photoSsgiResolutionPct;
            data.photoSsgiBlurPx = _photoSsgiBlurPx;
        }

        private void ApplyPhotoTuning(KitchenSettingsData data)
        {
            PhotoHdr = data.photoHdr;
            PhotoRenderScalePct = data.photoRenderScalePct;
            PhotoShadowMapPx = data.photoShadowMapPx;
            PhotoLightsPerObject = data.photoLightsPerObject;
            PhotoAmbientSkyPct = data.photoAmbientSkyPct;
            PhotoAmbientEquatorPct = data.photoAmbientEquatorPct;
            PhotoBounceMaxPct = data.photoBounceMaxPct;
            PhotoBloomClampPct = data.photoBloomClampPct;
            PhotoTonemap = data.photoTonemap;
            PhotoAoFullRes = data.photoAoFullRes;
            PhotoAoIntensityPct = data.photoAoIntensityPct;
            PhotoAoRadiusMM = data.photoAoRadiusMM;
            PhotoAoDirectPct = data.photoAoDirectPct;
            PhotoAoFalloffM = data.photoAoFalloffM;
            PhotoSsgiStrengthPct = data.photoSsgiStrengthPct;
            PhotoSsgiRadiusMM = data.photoSsgiRadiusMM;
            PhotoSsgiSamples = data.photoSsgiSamples;
            PhotoSsgiResolutionPct = data.photoSsgiResolutionPct;
            PhotoSsgiBlurPx = data.photoSsgiBlurPx;
        }
    }
}
