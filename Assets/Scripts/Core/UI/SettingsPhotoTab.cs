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

            _photoActiveToggle = _rows.AddToggle(page, ref y, Loc.T("settings.photo.active"), PhotoMode.Active,
                v => PhotoMode.SetActive(v));
            Hint(Loc.T("settings.photo.active"), hint: "settings.photo.active");
            PhotoMode.Changed -= SyncActiveToggle;
            PhotoMode.Changed += SyncActiveToggle;

            y -= SettingsRowFactory.GapPx;
            BuildPresetRow(page, ref y, s);

            y -= SettingsRowFactory.GapPx;
            AddPresetLinkedToggle(page, ref y, Loc.T("settings.photo.shadows"), s.PhotoShadows,
                v => s.PhotoShadows = v, () => s.PhotoShadows);
            Hint(Loc.T("settings.photo.shadows"), hint: "settings.photo.shadows");
            AddPresetLinkedToggle(page, ref y, Loc.T("settings.photo.softShadows"), s.PhotoSoftShadows,
                v => s.PhotoSoftShadows = v, () => s.PhotoSoftShadows);
            Hint(Loc.T("settings.photo.softShadows"), hint: "settings.photo.softShadows");
            AddPresetLinkedToggle(page, ref y, Loc.T("settings.photo.antiAliasing"), s.PhotoAntiAliasing,
                v => s.PhotoAntiAliasing = v, () => s.PhotoAntiAliasing);
            Hint(Loc.T("settings.photo.antiAliasing"), hint: "settings.photo.antiAliasing");
            AddPresetLinkedToggle(page, ref y, Loc.T("settings.photo.supersampling"), s.PhotoSupersampling,
                v => s.PhotoSupersampling = v, () => s.PhotoSupersampling);
            Hint(Loc.T("settings.photo.supersampling"), hint: "settings.photo.supersampling");
            AddPresetLinkedToggle(page, ref y, "Ambient occlusion", s.PhotoAmbientOcclusion,
                v => s.PhotoAmbientOcclusion = v, () => s.PhotoAmbientOcclusion);
            Hint("Ambient occlusion", hint: "settings.photo.ambientOcclusion");
            AddPresetLinkedToggle(page, ref y, Loc.T("settings.photo.bloom"), s.PhotoBloom,
                v => s.PhotoBloom = v, () => s.PhotoBloom);
            Hint(Loc.T("settings.photo.bloom"), hint: "settings.photo.bloom");
            AddPresetLinkedToggle(page, ref y, Loc.T("settings.photo.vignette"), s.PhotoVignette,
                v => s.PhotoVignette = v, () => s.PhotoVignette);
            Hint(Loc.T("settings.photo.vignette"), hint: "settings.photo.vignette");

            y -= SettingsRowFactory.GapPx;
            _rows.AddToggle(page, ref y, Loc.T("settings.photo.ceiling"), s.PhotoCeiling,
                v => { s.PhotoCeiling = v; PhotoMode.RefreshIfActive(); }, read: () => s.PhotoCeiling);
            Hint(Loc.T("settings.photo.ceiling"), hint: "settings.photo.ceiling");

            _ssgiToggle = _rows.AddToggle(page, ref y, Loc.T("settings.photo.ssgi"), s.PhotoSSGI,
                v => { s.PhotoSSGI = v; PhotoMode.RefreshIfActive(); }, read: () => s.PhotoSSGI);
            Hint(Loc.T("settings.photo.ssgi"), hint: "settings.photo.ssgi");

            _rows.AddToggle(page, ref y, Loc.T("settings.photo.hdr"), s.PhotoHdr,
                v => { s.PhotoHdr = v; PhotoMode.RefreshIfActive(); }, read: () => s.PhotoHdr);
            Hint(Loc.T("settings.photo.hdr"), hint: "settings.photo.hdr");

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, Loc.T("settings.photo.section.resolution"));
            AddSlider(page, ref y, Loc.T("settings.photo.renderScale"),
                KitchenSettings.PHOTO_RENDER_SCALE_MIN_PCT, KitchenSettings.PHOTO_RENDER_SCALE_MAX_PCT,
                s.PhotoRenderScalePct, Percent, v => s.PhotoRenderScalePct = v, () => s.PhotoRenderScalePct);
            Hint(Loc.T("settings.photo.renderScale"), hint: "settings.photo.renderScale");
            AddSlider(page, ref y, Loc.T("settings.photo.shadowMap"),
                KitchenSettings.PHOTO_SHADOWMAP_MIN_PX, KitchenSettings.PHOTO_SHADOWMAP_MAX_PX,
                s.PhotoShadowMapPx, Pixels, v => s.PhotoShadowMapPx = v, () => s.PhotoShadowMapPx);
            Hint(Loc.T("settings.photo.shadowMap"), hint: "settings.photo.shadowMap");
            AddSlider(page, ref y, Loc.T("settings.photo.lightsPerObject"),
                1, KitchenSettings.PHOTO_LIGHTS_PER_OBJECT_MAX,
                s.PhotoLightsPerObject, Plain, v => s.PhotoLightsPerObject = v, () => s.PhotoLightsPerObject);
            Hint(Loc.T("settings.photo.lightsPerObject"), hint: "settings.photo.lightsPerObject");

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Ambient occlusion");
            AddPresetLinkedToggle(page, ref y, Loc.T("settings.photo.aoFullRes"), s.PhotoAoFullRes,
                v => s.PhotoAoFullRes = v, () => s.PhotoAoFullRes);
            Hint(Loc.T("settings.photo.aoFullRes"), hint: "settings.photo.aoFullRes");
            AddSlider(page, ref y, Loc.T("settings.photo.aoIntensity"), 0, KitchenSettings.PHOTO_AO_INTENSITY_MAX_PCT,
                s.PhotoAoIntensityPct, Percent, v => s.PhotoAoIntensityPct = v, () => s.PhotoAoIntensityPct);
            Hint(Loc.T("settings.photo.aoIntensity"), hint: "settings.photo.aoIntensity");
            AddSlider(page, ref y, Loc.T("settings.photo.aoRadius"),
                KitchenSettings.PHOTO_AO_RADIUS_MIN_MM, KitchenSettings.PHOTO_AO_RADIUS_MAX_MM,
                s.PhotoAoRadiusMM, Millimetres, v => s.PhotoAoRadiusMM = v, () => s.PhotoAoRadiusMM);
            Hint(Loc.T("settings.photo.aoRadius"), hint: "settings.photo.aoRadius");
            AddSlider(page, ref y, Loc.T("settings.photo.aoDirect"), 0, 100,
                s.PhotoAoDirectPct, Percent, v => s.PhotoAoDirectPct = v, () => s.PhotoAoDirectPct);
            Hint(Loc.T("settings.photo.aoDirect"), hint: "settings.photo.aoDirect");
            AddSlider(page, ref y, Loc.T("settings.photo.aoFalloff"),
                KitchenSettings.PHOTO_AO_FALLOFF_MIN_M, KitchenSettings.PHOTO_AO_FALLOFF_MAX_M,
                s.PhotoAoFalloffM, Metres, v => s.PhotoAoFalloffM = v, () => s.PhotoAoFalloffM);
            Hint(Loc.T("settings.photo.aoFalloff"), hint: "settings.photo.aoFalloff");

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, Loc.T("settings.photo.section.ssgi"));
            AddSlider(page, ref y, Loc.T("settings.photo.ssgiStrength"), 0, KitchenSettings.PHOTO_SSGI_STRENGTH_MAX_PCT,
                s.PhotoSsgiStrengthPct, Percent, v => s.PhotoSsgiStrengthPct = v, () => s.PhotoSsgiStrengthPct);
            Hint(Loc.T("settings.photo.ssgiStrength"), hint: "settings.photo.ssgiStrength");
            AddSlider(page, ref y, Loc.T("settings.photo.ssgiRadius"),
                KitchenSettings.PHOTO_SSGI_RADIUS_MIN_MM, KitchenSettings.PHOTO_SSGI_RADIUS_MAX_MM,
                s.PhotoSsgiRadiusMM, Millimetres, v => s.PhotoSsgiRadiusMM = v, () => s.PhotoSsgiRadiusMM);
            Hint(Loc.T("settings.photo.ssgiRadius"), hint: "settings.photo.ssgiRadius");
            AddSlider(page, ref y, Loc.T("settings.photo.ssgiSamples"),
                KitchenSettings.PHOTO_SSGI_SAMPLES_MIN, KitchenSettings.PHOTO_SSGI_SAMPLES_MAX,
                s.PhotoSsgiSamples, Plain, v => s.PhotoSsgiSamples = v, () => s.PhotoSsgiSamples);
            Hint(Loc.T("settings.photo.ssgiSamples"), hint: "settings.photo.ssgiSamples");
            AddSlider(page, ref y, Loc.T("settings.photo.ssgiResolution"),
                KitchenSettings.PHOTO_SSGI_RESOLUTION_MIN_PCT, KitchenSettings.PHOTO_SSGI_RESOLUTION_MAX_PCT,
                s.PhotoSsgiResolutionPct, Percent, v => s.PhotoSsgiResolutionPct = v, () => s.PhotoSsgiResolutionPct);
            Hint(Loc.T("settings.photo.ssgiResolution"), hint: "settings.photo.ssgiResolution");
            AddSlider(page, ref y, Loc.T("settings.photo.ssgiBlur"), 0, KitchenSettings.PHOTO_SSGI_BLUR_MAX_PX,
                s.PhotoSsgiBlurPx, Pixels, v => s.PhotoSsgiBlurPx = v, () => s.PhotoSsgiBlurPx);
            Hint(Loc.T("settings.photo.ssgiBlur"), hint: "settings.photo.ssgiBlur");
        }

        private void AddSlider(Transform page, ref float y, string label, int min, int max,
            int value, Func<int, string> format, Action<int> apply, Func<int> read)
        {
            _rows.AddIntSlider(page, ref y, label, min, max, value, format,
                v => { apply(v); PhotoMode.RefreshIfActive(); }, read);
        }

        private static string Percent(int v) => v + " %";

        private static string Pixels(int v) => v + " px";

        private static string Millimetres(int v) => v + Loc.T("unit.mmSuffix");

        private static string Metres(int v) => v + Loc.T("unit.mSuffix");

        private static string Plain(int v) => v.ToString();

        public void Dispose() => PhotoMode.Changed -= SyncActiveToggle;

        private void Hint(string rowKey, string hint) =>
            HintBadge.AttachAfterLabel(_rows.RowLabel(rowKey), hint);

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
            Set(Loc.T("settings.photo.shadows"), s.PhotoShadows);
            Set(Loc.T("settings.photo.softShadows"), s.PhotoSoftShadows);
            Set(Loc.T("settings.photo.antiAliasing"), s.PhotoAntiAliasing);
            Set(Loc.T("settings.photo.supersampling"), s.PhotoSupersampling);
            Set("Ambient occlusion", s.PhotoAmbientOcclusion);
            Set(Loc.T("settings.photo.bloom"), s.PhotoBloom);
            Set(Loc.T("settings.photo.vignette"), s.PhotoVignette);
        }

        private void BuildPresetRow(Transform parent, ref float y, KitchenSettings s)
        {
            var rowRect = SettingsRowFactory.CreateRow("RowPreset", parent, y);

            SettingsRowFactory.CreateRowLabel("Lbl_Quality", rowRect, Loc.T("settings.photo.quality"), 0f);

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
            PhotoQualityPreset.Low => Loc.T("settings.photo.presetLow"),
            PhotoQualityPreset.Medium => Loc.T("settings.photo.presetMedium"),
            PhotoQualityPreset.High => Loc.T("settings.photo.presetHigh"),
            _ => Loc.T("settings.photo.presetCustom")
        };
    }
}
