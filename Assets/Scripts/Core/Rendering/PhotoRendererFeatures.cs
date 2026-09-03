using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KitchenDesigner.Core
{
    internal static class PhotoRendererFeatures
    {
        private const string RendererDataListField = "m_RendererDataList";
        private const string SsaoSettingsField = "m_Settings";
        private const BindingFlags Hidden =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public const string SsaoTypeName = "ScreenSpaceAmbientOcclusion";

        private const float PercentToUnit = 0.01f;
        private const float MillimetreToMetre = 0.001f;

        public static void SilenceUntilPhotoMode()
        {
            SetActive(SsaoTypeName, false);
            SetActive(nameof(ScreenSpaceGIFeature), false);
        }

        public static void SetActive(string typeNameContains, bool enabled)
        {
            foreach (var feature in All())
                if (feature.GetType().Name.Contains(typeNameContains))
                    feature.SetActive(enabled);
        }

        public static void ConfigureAmbientOcclusion(KitchenSettings s)
        {
            foreach (var feature in All())
            {
                if (feature.GetType().Name != SsaoTypeName) continue;
                var settings = feature.GetType()
                    .GetField(SsaoSettingsField, Hidden)?.GetValue(feature);
                if (settings == null) continue;

                SetField(settings, "Intensity", s.PhotoAoIntensityPct * PercentToUnit);
                SetField(settings, "Radius", s.PhotoAoRadiusMM * MillimetreToMetre);
                SetField(settings, "DirectLightingStrength", s.PhotoAoDirectPct * PercentToUnit);
                SetField(settings, "Falloff", (float)s.PhotoAoFalloffM);
                SetBoolField(settings, "Downsample", !s.PhotoAoFullRes);
            }
        }

        public static void ConfigureScreenSpaceGI(KitchenSettings s)
        {
            foreach (var feature in All())
            {
                if (feature is not ScreenSpaceGIFeature ssgi) continue;
                ssgi.Configure(
                    s.PhotoSsgiStrengthPct * PercentToUnit,
                    s.PhotoSsgiRadiusMM * MillimetreToMetre,
                    s.PhotoSsgiSamples,
                    s.PhotoSsgiResolutionPct * PercentToUnit,
                    s.PhotoSsgiBlurPx);
            }
        }

        private static System.Collections.Generic.IEnumerable<ScriptableRendererFeature> All()
        {
            var asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset
                ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) yield break;

            var listField = typeof(UniversalRenderPipelineAsset).GetField(RendererDataListField, Hidden);
            if (listField?.GetValue(asset) is not ScriptableRendererData[] datas) yield break;

            foreach (var data in datas)
            {
                if (data == null) continue;
                foreach (var feature in data.rendererFeatures)
                    if (feature != null) yield return feature;
            }
        }

        private static void SetBoolField(object target, string name, bool value)
        {
            try
            {
                target.GetType().GetField(name, Hidden)?.SetValue(target, value);
            }
            catch (Exception)
            {
            }
        }

        private static void SetField(object target, string name, float value)
        {
            try
            {
                target.GetType().GetField(name, Hidden)?.SetValue(target, value);
            }
            catch (Exception)
            {
            }
        }
    }
}
