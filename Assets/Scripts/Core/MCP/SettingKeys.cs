using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Construction;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class SettingKey
    {
        private SettingKey(string wire, string field, Func<object> read,
            Action<bool>? writeFlag, Action<float>? writeNumber)
        {
            Wire = wire;
            Field = field;
            Read = read;
            WriteFlag = writeFlag;
            WriteNumber = writeNumber;
        }

        public string Wire { get; }

        public string Field { get; }

        public Func<object> Read { get; }

        public Action<bool>? WriteFlag { get; }

        public Action<float>? WriteNumber { get; }

        public bool IsNumber => WriteNumber != null;

        public static SettingKey Flag(string wire, string field, Func<bool> read, Action<bool> write)
            => new SettingKey(wire, field, () => read(), write, null);

        public static SettingKey Number(string wire, string field, Func<object> read, Action<float> write)
            => new SettingKey(wire, field, read, null, write);
    }

    internal static class SettingKeys
    {
        private static KitchenSettings S => KitchenSettings.Instance;

        private const int MM_PER_METRE = 1000;

        public static readonly IReadOnlyList<SettingKey> All = new[]
        {
            SettingKey.Flag("snap_enabled", "snapEnabled",
                () => S.SnapEnabled, v => S.SnapEnabled = v),
            SettingKey.Number("snap_threshold", "snapThresholdMM",
                () => S.SnapThreshold, v => S.SnapThreshold = v),
            SettingKey.Flag("grid_enabled", "gridEnabled",
                () => S.GridEnabled, v => S.GridEnabled = v),
            SettingKey.Number("grid_step", "gridStepMM",
                () => S.GridStep, v => S.GridStep = (int)v),
            SettingKey.Flag("block_on_violation", "blockOnViolation",
                () => S.BlockOnViolation, v => S.BlockOnViolation = v),
            SettingKey.Flag("auto_save", "autoSave",
                () => S.AutoSave, v => S.AutoSave = v),
            SettingKey.Number("auto_save_interval", "autoSaveIntervalSec",
                () => S.AutoSaveInterval, v => S.AutoSaveInterval = (int)v),
            SettingKey.Flag("snap_verbose_log", "snapVerboseLog",
                () => SnapSystem.VerboseLog, v => SnapSystem.VerboseLog = v),
            SettingKey.Flag("camera_pan_free", "cameraPanFree",
                () => S.CameraPanFree, v => S.CameraPanFree = v),
            SettingKey.Number("edge_partial_threshold", "edgePartialThresholdPct",
                () => S.EdgePartialThresholdPct, v => S.EdgePartialThresholdPct = (int)v),
            SettingKey.Number("mouse_sensitivity", "mouseSensitivity",
                () => S.MouseSensitivity, v => S.MouseSensitivity = v),
            SettingKey.Number("wasd_speed", "wasdSpeed",
                () => S.WasdSpeed, v => S.WasdSpeed = v),
            SettingKey.Number("arrow_speed", "arrowSpeed",
                () => S.ArrowSpeed, v => S.ArrowSpeed = v),

            SettingKey.Number("construction_region", "constructionRegion",
                () => (int)S.ConstructionRegion, v => S.ConstructionRegion = (ConstructionRegion)(int)v),
            SettingKey.Number("construction_floor_height", "constructionFloorHeightMm",
                () => S.ConstructionFloorHeightMm, v => S.ConstructionFloorHeightMm = (int)v),
            SettingKey.Number("construction_masonry", "constructionMasonry",
                () => (int)S.ConstructionMasonry, v => S.ConstructionMasonry = (MasonryTechnology)(int)v),
            SettingKey.Number("construction_joint", "constructionJointMm",
                () => S.ConstructionJointMm, v => S.ConstructionJointMm = (int)v),
            SettingKey.Number("construction_waste", "constructionWastePct",
                () => S.ConstructionWastePct, v => S.ConstructionWastePct = (int)v),
            SettingKey.Number("construction_soil", "constructionSoil",
                () => (int)S.ConstructionSoil, v => S.ConstructionSoil = (SoilKind)(int)v),
            SettingKey.Number("construction_concrete", "constructionConcrete",
                () => (int)S.ConstructionConcrete, v => S.ConstructionConcrete = (ConcreteGrade)(int)v),
            SettingKey.Number("construction_sand", "constructionSandMm",
                () => S.ConstructionSandMm, v => S.ConstructionSandMm = (int)v),
            SettingKey.Number("construction_gravel", "constructionGravelMm",
                () => S.ConstructionGravelMm, v => S.ConstructionGravelMm = (int)v),
            SettingKey.Flag("construction_compacted", "constructionCompacted",
                () => S.ConstructionCompacted, v => S.ConstructionCompacted = v),

            SettingKey.Flag("photo_active", "photoActive",
                () => PhotoMode.Active, PhotoMode.SetActive),
            SettingKey.Number("photo_quality", "photoQuality",
                () => (int)S.PhotoQuality, v => S.PhotoQuality = (PhotoQualityPreset)Mathf.Clamp((int)v, 0, 3)),
            SettingKey.Flag("photo_shadows", "photoShadows",
                () => S.PhotoShadows, v => S.PhotoShadows = v),
            SettingKey.Flag("photo_soft_shadows", "photoSoftShadows",
                () => S.PhotoSoftShadows, v => S.PhotoSoftShadows = v),
            SettingKey.Flag("photo_anti_aliasing", "photoAntiAliasing",
                () => S.PhotoAntiAliasing, v => S.PhotoAntiAliasing = v),
            SettingKey.Flag("photo_supersampling", "photoSupersampling",
                () => S.PhotoSupersampling, v => S.PhotoSupersampling = v),
            SettingKey.Flag("photo_ambient_occlusion", "photoAmbientOcclusion",
                () => S.PhotoAmbientOcclusion, v => S.PhotoAmbientOcclusion = v),
            SettingKey.Flag("photo_bloom", "photoBloom",
                () => S.PhotoBloom, v => S.PhotoBloom = v),
            SettingKey.Flag("photo_vignette", "photoVignette",
                () => S.PhotoVignette, v => S.PhotoVignette = v),
            SettingKey.Flag("photo_ceiling", "photoCeiling",
                () => S.PhotoCeiling, v => S.PhotoCeiling = v),
            SettingKey.Flag("photo_ssgi", "photoSSGI",
                () => S.PhotoSSGI, v => S.PhotoSSGI = v),
            SettingKey.Flag("photo_lamp_shadows", "photoLampShadows",
                () => S.PhotoLampShadows, v => S.PhotoLampShadows = v),
            SettingKey.Flag("photo_hdr", "photoHdr",
                () => S.PhotoHdr, v => S.PhotoHdr = v),

            SettingKey.Number("photo_ambient", "photoAmbientPct",
                () => S.PhotoAmbientPct, v => S.PhotoAmbientPct = (int)v),
            SettingKey.Number("photo_floor_bounce", "photoFloorBouncePct",
                () => S.PhotoFloorBouncePct, v => S.PhotoFloorBouncePct = (int)v),
            SettingKey.Number("photo_ambient_sky", "photoAmbientSkyPct",
                () => S.PhotoAmbientSkyPct, v => S.PhotoAmbientSkyPct = (int)v),
            SettingKey.Number("photo_ambient_equator", "photoAmbientEquatorPct",
                () => S.PhotoAmbientEquatorPct, v => S.PhotoAmbientEquatorPct = (int)v),
            SettingKey.Number("photo_bounce_max", "photoBounceMaxPct",
                () => S.PhotoBounceMaxPct, v => S.PhotoBounceMaxPct = (int)v),
            SettingKey.Number("photo_exposure", "photoExposurePct",
                () => S.PhotoExposurePct, v => S.PhotoExposurePct = (int)v),
            SettingKey.Number("photo_contrast", "photoContrastPct",
                () => S.PhotoContrastPct, v => S.PhotoContrastPct = (int)v),
            SettingKey.Number("photo_saturation", "photoSaturationPct",
                () => S.PhotoSaturationPct, v => S.PhotoSaturationPct = (int)v),
            SettingKey.Number("photo_bloom_strength", "photoBloomPct",
                () => S.PhotoBloomPct, v => S.PhotoBloomPct = (int)v),
            SettingKey.Number("photo_bloom_threshold", "photoBloomThresholdPct",
                () => S.PhotoBloomThresholdPct, v => S.PhotoBloomThresholdPct = (int)v),
            SettingKey.Number("photo_tonemap", "photoTonemap",
                () => S.PhotoTonemap, v => S.PhotoTonemap = (int)v),
            SettingKey.Flag("photo_ao_full_res", "photoAoFullRes",
                () => S.PhotoAoFullRes, v => S.PhotoAoFullRes = v),
            SettingKey.Number("photo_bloom_clamp", "photoBloomClampPct",
                () => S.PhotoBloomClampPct, v => S.PhotoBloomClampPct = (int)v),
            SettingKey.Number("photo_vignette_strength", "photoVignettePct",
                () => S.PhotoVignettePct, v => S.PhotoVignettePct = (int)v),
            SettingKey.Number("photo_sun_shadow_strength", "photoSunShadowStrengthPct",
                () => S.PhotoSunShadowStrengthPct, v => S.PhotoSunShadowStrengthPct = (int)v),
            SettingKey.Number("photo_shadow_distance_mm", "photoShadowDistanceMm",
                () => S.PhotoShadowDistanceM * MM_PER_METRE, v => S.PhotoShadowDistanceM = (int)(v / MM_PER_METRE)),
            SettingKey.Number("photo_render_scale", "photoRenderScalePct",
                () => S.PhotoRenderScalePct, v => S.PhotoRenderScalePct = (int)v),
            SettingKey.Number("photo_shadowmap", "photoShadowMapPx",
                () => S.PhotoShadowMapPx, v => S.PhotoShadowMapPx = (int)v),
            SettingKey.Number("photo_lights_per_object", "photoLightsPerObject",
                () => S.PhotoLightsPerObject, v => S.PhotoLightsPerObject = (int)v),
            SettingKey.Number("photo_ao_intensity", "photoAoIntensityPct",
                () => S.PhotoAoIntensityPct, v => S.PhotoAoIntensityPct = (int)v),
            SettingKey.Number("photo_ao_radius", "photoAoRadiusMM",
                () => S.PhotoAoRadiusMM, v => S.PhotoAoRadiusMM = (int)v),
            SettingKey.Number("photo_ao_direct", "photoAoDirectPct",
                () => S.PhotoAoDirectPct, v => S.PhotoAoDirectPct = (int)v),
            SettingKey.Number("photo_ao_falloff_mm", "photoAoFalloffMm",
                () => S.PhotoAoFalloffM * MM_PER_METRE, v => S.PhotoAoFalloffM = (int)(v / MM_PER_METRE)),
            SettingKey.Number("photo_ssgi_strength", "photoSsgiStrengthPct",
                () => S.PhotoSsgiStrengthPct, v => S.PhotoSsgiStrengthPct = (int)v),
            SettingKey.Number("photo_ssgi_radius", "photoSsgiRadiusMM",
                () => S.PhotoSsgiRadiusMM, v => S.PhotoSsgiRadiusMM = (int)v),
            SettingKey.Number("photo_ssgi_samples", "photoSsgiSamples",
                () => S.PhotoSsgiSamples, v => S.PhotoSsgiSamples = (int)v),
            SettingKey.Number("photo_ssgi_resolution", "photoSsgiResolutionPct",
                () => S.PhotoSsgiResolutionPct, v => S.PhotoSsgiResolutionPct = (int)v),
            SettingKey.Number("photo_ssgi_blur", "photoSsgiBlurPx",
                () => S.PhotoSsgiBlurPx, v => S.PhotoSsgiBlurPx = (int)v),
        };

        public static SettingKey? Find(string wire)
        {
            foreach (var key in All)
                if (string.Equals(key.Wire, wire, StringComparison.Ordinal)) return key;
            return null;
        }
    }
}
