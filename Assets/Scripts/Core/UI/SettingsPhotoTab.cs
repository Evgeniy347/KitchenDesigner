using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsPhotoTab
    {
        private readonly SettingsRowFactory _rows;
        private readonly Dictionary<string, Toggle> _presetLinkedToggles = new();

        private Button? _presetButton;
        private Toggle? _photoActiveToggle;
        private Toggle? _ssgiToggle;

        public SettingsPhotoTab(SettingsRowFactory rows) => _rows = rows;

        public void Build(Transform page, KitchenSettings s, float topY)
        {
            float y = topY;

            _photoActiveToggle = _rows.AddToggle(page, ref y, "Фоторежим", PhotoMode.Active,
                v => PhotoMode.SetActive(v));
            PhotoMode.Changed -= SyncActiveToggle;
            PhotoMode.Changed += SyncActiveToggle;

            y -= SettingsRowFactory.GapPx;
            BuildPresetRow(page, ref y, s);

            y -= SettingsRowFactory.GapPx;
            AddPresetLinkedToggle(page, ref y, "Тени", s.PhotoShadows,
                v => s.PhotoShadows = v, () => s.PhotoShadows);
            AddPresetLinkedToggle(page, ref y, "Мягкие тени", s.PhotoSoftShadows,
                v => s.PhotoSoftShadows = v, () => s.PhotoSoftShadows);
            AddPresetLinkedToggle(page, ref y, "Сглаживание", s.PhotoAntiAliasing,
                v => s.PhotoAntiAliasing = v, () => s.PhotoAntiAliasing);
            AddPresetLinkedToggle(page, ref y, "Суперсэмплинг", s.PhotoSupersampling,
                v => s.PhotoSupersampling = v, () => s.PhotoSupersampling);
            AddPresetLinkedToggle(page, ref y, "Ambient occlusion", s.PhotoAmbientOcclusion,
                v => s.PhotoAmbientOcclusion = v, () => s.PhotoAmbientOcclusion);
            AddPresetLinkedToggle(page, ref y, "Свечение (bloom)", s.PhotoBloom,
                v => s.PhotoBloom = v, () => s.PhotoBloom);
            AddPresetLinkedToggle(page, ref y, "Виньетка", s.PhotoVignette,
                v => s.PhotoVignette = v, () => s.PhotoVignette);

            y -= SettingsRowFactory.GapPx;
            _rows.AddToggle(page, ref y, "Потолок по стенам", s.PhotoCeiling,
                v => { s.PhotoCeiling = v; PhotoMode.RefreshIfActive(); }, read: () => s.PhotoCeiling);

            _ssgiToggle = _rows.AddToggle(page, ref y, "Отражённый свет (SSGI, опытный)", s.PhotoSSGI,
                v => { s.PhotoSSGI = v; PhotoMode.RefreshIfActive(); }, read: () => s.PhotoSSGI);

            _rows.AddToggle(page, ref y, "Расширенный диапазон (HDR)", s.PhotoHdr,
                v => { s.PhotoHdr = v; PhotoMode.RefreshIfActive(); }, read: () => s.PhotoHdr);

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Разрешение и тени");
            AddSlider(page, ref y, "Масштаб рендера",
                KitchenSettings.PHOTO_RENDER_SCALE_MIN_PCT, KitchenSettings.PHOTO_RENDER_SCALE_MAX_PCT,
                s.PhotoRenderScalePct, Percent, v => s.PhotoRenderScalePct = v, () => s.PhotoRenderScalePct);
            AddSlider(page, ref y, "Карта теней",
                KitchenSettings.PHOTO_SHADOWMAP_MIN_PX, KitchenSettings.PHOTO_SHADOWMAP_MAX_PX,
                s.PhotoShadowMapPx, Pixels, v => s.PhotoShadowMapPx = v, () => s.PhotoShadowMapPx);
            AddSlider(page, ref y, "Ламп на объект",
                1, KitchenSettings.PHOTO_LIGHTS_PER_OBJECT_MAX,
                s.PhotoLightsPerObject, Plain, v => s.PhotoLightsPerObject = v, () => s.PhotoLightsPerObject);

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Ambient occlusion");
            AddSlider(page, ref y, "Сила AO", 0, KitchenSettings.PHOTO_AO_INTENSITY_MAX_PCT,
                s.PhotoAoIntensityPct, Percent, v => s.PhotoAoIntensityPct = v, () => s.PhotoAoIntensityPct);
            AddSlider(page, ref y, "Радиус AO",
                KitchenSettings.PHOTO_AO_RADIUS_MIN_MM, KitchenSettings.PHOTO_AO_RADIUS_MAX_MM,
                s.PhotoAoRadiusMM, Millimetres, v => s.PhotoAoRadiusMM = v, () => s.PhotoAoRadiusMM);
            AddSlider(page, ref y, "AO по прямому свету", 0, 100,
                s.PhotoAoDirectPct, Percent, v => s.PhotoAoDirectPct = v, () => s.PhotoAoDirectPct);
            AddSlider(page, ref y, "Затухание AO",
                KitchenSettings.PHOTO_AO_FALLOFF_MIN_M, KitchenSettings.PHOTO_AO_FALLOFF_MAX_M,
                s.PhotoAoFalloffM, Metres, v => s.PhotoAoFalloffM = v, () => s.PhotoAoFalloffM);

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Отражённый свет (SSGI)");
            AddSlider(page, ref y, "Сила SSGI", 0, KitchenSettings.PHOTO_SSGI_STRENGTH_MAX_PCT,
                s.PhotoSsgiStrengthPct, Percent, v => s.PhotoSsgiStrengthPct = v, () => s.PhotoSsgiStrengthPct);
            AddSlider(page, ref y, "Радиус SSGI",
                KitchenSettings.PHOTO_SSGI_RADIUS_MIN_MM, KitchenSettings.PHOTO_SSGI_RADIUS_MAX_MM,
                s.PhotoSsgiRadiusMM, Millimetres, v => s.PhotoSsgiRadiusMM = v, () => s.PhotoSsgiRadiusMM);
            AddSlider(page, ref y, "Выборок SSGI",
                KitchenSettings.PHOTO_SSGI_SAMPLES_MIN, KitchenSettings.PHOTO_SSGI_SAMPLES_MAX,
                s.PhotoSsgiSamples, Plain, v => s.PhotoSsgiSamples = v, () => s.PhotoSsgiSamples);
            AddSlider(page, ref y, "Разрешение SSGI",
                KitchenSettings.PHOTO_SSGI_RESOLUTION_MIN_PCT, KitchenSettings.PHOTO_SSGI_RESOLUTION_MAX_PCT,
                s.PhotoSsgiResolutionPct, Percent, v => s.PhotoSsgiResolutionPct = v, () => s.PhotoSsgiResolutionPct);
            AddSlider(page, ref y, "Сглаживание SSGI", 0, KitchenSettings.PHOTO_SSGI_BLUR_MAX_PX,
                s.PhotoSsgiBlurPx, Pixels, v => s.PhotoSsgiBlurPx = v, () => s.PhotoSsgiBlurPx);
        }

        private void AddSlider(Transform page, ref float y, string label, int min, int max,
            int value, Func<int, string> format, Action<int> apply, Func<int> read)
        {
            _rows.AddIntSlider(page, ref y, label, min, max, value, format,
                v => { apply(v); PhotoMode.RefreshIfActive(); }, read);
        }

        private static string Percent(int v) => v + " %";

        private static string Pixels(int v) => v + " px";

        private static string Millimetres(int v) => v + " мм";

        private static string Metres(int v) => v + " м";

        private static string Plain(int v) => v.ToString();

        public void Dispose() => PhotoMode.Changed -= SyncActiveToggle;

        public void SyncActiveToggle()
        {
            if (_photoActiveToggle != null)
                _photoActiveToggle.SetIsOnWithoutNotify(PhotoMode.Active);
        }

        public void RefreshPresetLabel()
        {
            var s = KitchenSettings.Instance;
            var label = _presetButton != null
                ? _presetButton.GetComponentInChildren<TMPro.TextMeshProUGUI>()
                : null;
            if (label != null && s != null) label.text = PresetName(PhotoQualityPresetTable.Detect(s));
        }


        private void AddPresetLinkedToggle(Transform page, ref float y, string label, bool value,
            Action<bool> setter, Func<bool> read)
        {
            var toggle = _rows.AddToggle(page, ref y, label, value,
                v => { setter(v); OnPresetLinkedToggleChanged(); }, read: read);
            _presetLinkedToggles[label] = toggle;
        }

        private void OnPresetLinkedToggleChanged()
        {
            var s = KitchenSettings.Instance;
            if (s != null) s.PhotoQuality = PhotoQualityPresetTable.Detect(s);
            RefreshPresetLabel();
            PhotoMode.RefreshIfActive();
        }

        private void SyncPresetLinkedTogglesFromSettings(KitchenSettings s)
        {
            void Set(string key, bool v)
            {
                if (_presetLinkedToggles.TryGetValue(key, out var tg)) tg.SetIsOnWithoutNotify(v);
            }
            Set("Тени", s.PhotoShadows);
            Set("Мягкие тени", s.PhotoSoftShadows);
            Set("Сглаживание", s.PhotoAntiAliasing);
            Set("Суперсэмплинг", s.PhotoSupersampling);
            Set("Ambient occlusion", s.PhotoAmbientOcclusion);
            Set("Свечение (bloom)", s.PhotoBloom);
            Set("Виньетка", s.PhotoVignette);
        }

        private void BuildPresetRow(Transform parent, ref float y, KitchenSettings s)
        {
            var rowRect = SettingsRowFactory.CreateRow("RowPreset", parent, y);

            SettingsRowFactory.CreateRowLabel("Lbl_Quality", rowRect, "Качество", 0f);

            _presetButton = UIFactory.CreateButton("Btn_Quality", rowRect,
                PresetName(PhotoQualityPresetTable.Detect(s)),
                new Vector2(SettingsRowFactory.ContentW * 0.5f - SettingsRowFactory.ControlW * 0.5f, 0),
                new Vector2(SettingsRowFactory.ControlW, SettingsRowFactory.RowH),
                () => CyclePreset(s));

            y -= SettingsRowFactory.RowStep;
        }

        private void CyclePreset(KitchenSettings s)
        {
            var next = PhotoQualityPresetTable.Next(PhotoQualityPresetTable.Detect(s));
            PhotoQualityPresetTable.Apply(next, s);
            SyncPresetLinkedTogglesFromSettings(s);
            RefreshPresetLabel();
            PhotoMode.RefreshIfActive();
        }

        private static string PresetName(PhotoQualityPreset preset) => preset switch
        {
            PhotoQualityPreset.Low => "Низкое",
            PhotoQualityPreset.Medium => "Среднее",
            PhotoQualityPreset.High => "Высокое",
            _ => "Свои настройки"
        };
    }
}
