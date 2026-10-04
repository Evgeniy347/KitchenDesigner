using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsPhotoTab
    {
        internal const string QualityId = "Quality";
        internal const string CustomCaptionNode = "QualityCustom";

        private readonly SettingsPage _page;

        private SegmentedControl? _quality;
        private TextMeshProUGUI? _customCaption;

        public SettingsPhotoTab(SettingsPage page) => _page = page;

        public void Build(KitchenSettings s)
        {
            _page.OnReset = ResetSection;

            _page.Section(Loc.T("settings.photo.section.quality"));
            BuildPresetRow(s);

            AddPresetLinkedToggle(Loc.T("settings.photo.shadows"), s.PhotoShadows,
                v => s.PhotoShadows = v, () => s.PhotoShadows);
            Hint(Loc.T("settings.photo.shadows"), hint: "settings.photo.shadows");
            AddPresetLinkedToggle(Loc.T("settings.photo.softShadows"), s.PhotoSoftShadows,
                v => s.PhotoSoftShadows = v, () => s.PhotoSoftShadows);
            Hint(Loc.T("settings.photo.softShadows"), hint: "settings.photo.softShadows");
            AddPresetLinkedToggle(Loc.T("settings.photo.antiAliasing"), s.PhotoAntiAliasing,
                v => s.PhotoAntiAliasing = v, () => s.PhotoAntiAliasing);
            Hint(Loc.T("settings.photo.antiAliasing"), hint: "settings.photo.antiAliasing");
            AddPresetLinkedToggle(Loc.T("settings.photo.supersampling"), s.PhotoSupersampling,
                v => s.PhotoSupersampling = v, () => s.PhotoSupersampling);
            Hint(Loc.T("settings.photo.supersampling"), hint: "settings.photo.supersampling");
            AddPresetLinkedToggle("Ambient occlusion", s.PhotoAmbientOcclusion,
                v => s.PhotoAmbientOcclusion = v, () => s.PhotoAmbientOcclusion);
            Hint("Ambient occlusion", hint: "settings.photo.ambientOcclusion");
            AddPresetLinkedToggle(Loc.T("settings.photo.bloom"), s.PhotoBloom,
                v => s.PhotoBloom = v, () => s.PhotoBloom);
            Hint(Loc.T("settings.photo.bloom"), hint: "settings.photo.bloom");
            AddPresetLinkedToggle(Loc.T("settings.photo.vignette"), s.PhotoVignette,
                v => s.PhotoVignette = v, () => s.PhotoVignette);
            Hint(Loc.T("settings.photo.vignette"), hint: "settings.photo.vignette");

            _page.Section(Loc.T("settings.photo.section.scene"));
            _page.AddSwitch(Loc.T("settings.photo.ceiling"), s.PhotoCeiling,
                v => { s.PhotoCeiling = v; PhotoMode.RefreshIfActive(); }, read: () => s.PhotoCeiling);
            Hint(Loc.T("settings.photo.ceiling"), hint: "settings.photo.ceiling");

            _page.AddSwitch(Loc.T("settings.photo.ssgi"), s.PhotoSSGI,
                v => { s.PhotoSSGI = v; PhotoMode.RefreshIfActive(); }, read: () => s.PhotoSSGI);
            Hint(Loc.T("settings.photo.ssgi"), hint: "settings.photo.ssgi");

            _page.AddSwitch(Loc.T("settings.photo.hdr"), s.PhotoHdr,
                v => { s.PhotoHdr = v; PhotoMode.RefreshIfActive(); }, read: () => s.PhotoHdr);
            Hint(Loc.T("settings.photo.hdr"), hint: "settings.photo.hdr");

            _page.Section(Loc.T("settings.photo.section.resolution"));
            AddSlider(Loc.T("settings.photo.renderScale"),
                KitchenSettings.PHOTO_RENDER_SCALE_MIN_PCT, KitchenSettings.PHOTO_RENDER_SCALE_MAX_PCT,
                s.PhotoRenderScalePct, Percent, v => s.PhotoRenderScalePct = v, () => s.PhotoRenderScalePct);
            Hint(Loc.T("settings.photo.renderScale"), hint: "settings.photo.renderScale");
            AddSlider(Loc.T("settings.photo.shadowMap"),
                KitchenSettings.PHOTO_SHADOWMAP_MIN_PX, KitchenSettings.PHOTO_SHADOWMAP_MAX_PX,
                s.PhotoShadowMapPx, Pixels, v => s.PhotoShadowMapPx = v, () => s.PhotoShadowMapPx);
            Hint(Loc.T("settings.photo.shadowMap"), hint: "settings.photo.shadowMap");
            AddSlider(Loc.T("settings.photo.lightsPerObject"),
                1, KitchenSettings.PHOTO_LIGHTS_PER_OBJECT_MAX,
                s.PhotoLightsPerObject, Plain, v => s.PhotoLightsPerObject = v, () => s.PhotoLightsPerObject);
            Hint(Loc.T("settings.photo.lightsPerObject"), hint: "settings.photo.lightsPerObject");

            _page.Section("Ambient occlusion");
            AddPresetLinkedToggle(Loc.T("settings.photo.aoFullRes"), s.PhotoAoFullRes,
                v => s.PhotoAoFullRes = v, () => s.PhotoAoFullRes);
            Hint(Loc.T("settings.photo.aoFullRes"), hint: "settings.photo.aoFullRes");
            AddSlider(Loc.T("settings.photo.aoIntensity"), 0, KitchenSettings.PHOTO_AO_INTENSITY_MAX_PCT,
                s.PhotoAoIntensityPct, Percent, v => s.PhotoAoIntensityPct = v, () => s.PhotoAoIntensityPct);
            Hint(Loc.T("settings.photo.aoIntensity"), hint: "settings.photo.aoIntensity");
            AddSlider(Loc.T("settings.photo.aoRadius"),
                KitchenSettings.PHOTO_AO_RADIUS_MIN_MM, KitchenSettings.PHOTO_AO_RADIUS_MAX_MM,
                s.PhotoAoRadiusMM, Millimetres, v => s.PhotoAoRadiusMM = v, () => s.PhotoAoRadiusMM);
            Hint(Loc.T("settings.photo.aoRadius"), hint: "settings.photo.aoRadius");
            AddSlider(Loc.T("settings.photo.aoDirect"), 0, 100,
                s.PhotoAoDirectPct, Percent, v => s.PhotoAoDirectPct = v, () => s.PhotoAoDirectPct);
            Hint(Loc.T("settings.photo.aoDirect"), hint: "settings.photo.aoDirect");
            AddSlider(Loc.T("settings.photo.aoFalloff"),
                KitchenSettings.PHOTO_AO_FALLOFF_MIN_M, KitchenSettings.PHOTO_AO_FALLOFF_MAX_M,
                s.PhotoAoFalloffM, Metres, v => s.PhotoAoFalloffM = v, () => s.PhotoAoFalloffM);
            Hint(Loc.T("settings.photo.aoFalloff"), hint: "settings.photo.aoFalloff");

            _page.Section(Loc.T("settings.photo.section.ssgi"));
            AddSlider(Loc.T("settings.photo.ssgiStrength"), 0, KitchenSettings.PHOTO_SSGI_STRENGTH_MAX_PCT,
                s.PhotoSsgiStrengthPct, Percent, v => s.PhotoSsgiStrengthPct = v, () => s.PhotoSsgiStrengthPct);
            Hint(Loc.T("settings.photo.ssgiStrength"), hint: "settings.photo.ssgiStrength");
            AddSlider(Loc.T("settings.photo.ssgiRadius"),
                KitchenSettings.PHOTO_SSGI_RADIUS_MIN_MM, KitchenSettings.PHOTO_SSGI_RADIUS_MAX_MM,
                s.PhotoSsgiRadiusMM, Millimetres, v => s.PhotoSsgiRadiusMM = v, () => s.PhotoSsgiRadiusMM);
            Hint(Loc.T("settings.photo.ssgiRadius"), hint: "settings.photo.ssgiRadius");
            AddSlider(Loc.T("settings.photo.ssgiSamples"),
                KitchenSettings.PHOTO_SSGI_SAMPLES_MIN, KitchenSettings.PHOTO_SSGI_SAMPLES_MAX,
                s.PhotoSsgiSamples, Plain, v => s.PhotoSsgiSamples = v, () => s.PhotoSsgiSamples);
            Hint(Loc.T("settings.photo.ssgiSamples"), hint: "settings.photo.ssgiSamples");
            AddSlider(Loc.T("settings.photo.ssgiResolution"),
                KitchenSettings.PHOTO_SSGI_RESOLUTION_MIN_PCT, KitchenSettings.PHOTO_SSGI_RESOLUTION_MAX_PCT,
                s.PhotoSsgiResolutionPct, Percent, v => s.PhotoSsgiResolutionPct = v, () => s.PhotoSsgiResolutionPct);
            Hint(Loc.T("settings.photo.ssgiResolution"), hint: "settings.photo.ssgiResolution");
            AddSlider(Loc.T("settings.photo.ssgiBlur"), 0, KitchenSettings.PHOTO_SSGI_BLUR_MAX_PX,
                s.PhotoSsgiBlurPx, Pixels, v => s.PhotoSsgiBlurPx = v, () => s.PhotoSsgiBlurPx);
            Hint(Loc.T("settings.photo.ssgiBlur"), hint: "settings.photo.ssgiBlur");
        }

        private void AddSlider(string label, int min, int max, int value, Func<int, string> format,
            Action<int> apply, Func<int> read)
        {
            _page.AddIntSlider(label, min, max, value, format,
                v => { apply(v); PhotoMode.RefreshIfActive(); }, read);
        }

        private static string Percent(int v) => v + " %";

        private static string Pixels(int v) => v + " px";

        private static string Millimetres(int v) => v + Loc.T("unit.mmSuffix");

        private static string Metres(int v) => v + Loc.T("unit.mSuffix");

        private static string Plain(int v) => v.ToString();

        private void Hint(string rowKey, string hint) => _page.Hint(rowKey, hint);

        public void RefreshPreset()
        {
            var s = KitchenSettings.Instance;
            if (_quality == null || _customCaption == null || s == null) return;

            var preset = PhotoQualityPresetTable.Detect(s);
            bool custom = preset == PhotoQualityPreset.Custom;
            _quality.SetValueWithoutNotify(custom ? 0 : (int)preset);
            if (custom)
                foreach (var segment in _quality.Segments)
                {
                    ((Image)segment.targetGraphic).color = UIStyle.Transparent;
                    segment.GetComponentInChildren<TMP_Text>().color = UIStyle.TextSecondary;
                }
            _customCaption.gameObject.SetActive(custom);
        }

        private void AddPresetLinkedToggle(string label, bool value, Action<bool> setter, Func<bool> read)
        {
            _page.AddSwitch(label, value, v => { setter(v); OnPresetLinkedToggleChanged(); }, read: read);
        }

        private void OnPresetLinkedToggleChanged()
        {
            var s = KitchenSettings.Instance;
            if (s != null) s.PhotoQuality = PhotoQualityPresetTable.Detect(s);
            RefreshPreset();
            PhotoMode.RefreshIfActive();
        }

        private void BuildPresetRow(KitchenSettings s)
        {
            var captions = new[]
            {
                PresetName(PhotoQualityPreset.Low),
                PresetName(PhotoQualityPreset.Medium),
                PresetName(PhotoQualityPreset.High),
            };
            var detected = PhotoQualityPresetTable.Detect(s);
            float width = _page.SegmentedWidthFor(captions);
            _quality = _page.AddSegmented(Loc.T("settings.photo.quality"), captions,
                detected == PhotoQualityPreset.Custom ? 0 : (int)detected, _ => { }, id: QualityId,
                width: width);
            for (int i = 0; i < _quality.Segments.Count; i++)
            {
                int index = i;
                _quality.Segments[i].onClick.AddListener(() => ApplyPreset(index));
            }

            var metrics = _page.Rows.Metrics;
            var row = _page.RowOf(QualityId)!.Root;
            _customCaption = UIFactory.CreateLabel(CustomCaptionNode, row, PresetName(PhotoQualityPreset.Custom),
                UIStyle.FontCaption, Vector2.zero,
                new Vector2(SettingsPage.ContentW - metrics.ValueX - width - UIStyle.Space2, metrics.ControlH),
                TextAnchor.MiddleLeft);
            _customCaption.color = UIStyle.TextSecondary;
            _customCaption.raycastTarget = false;
            _customCaption.enableWordWrapping = false;
            _customCaption.overflowMode = TextOverflowModes.Ellipsis;
            var rt = _customCaption.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(metrics.ValueX + width + UIStyle.Space2, 0f);
            RefreshPreset();
        }

        private void ApplyPreset(int index)
        {
            var s = KitchenSettings.Instance;
            PhotoQualityPresetTable.Apply(PhotoQualityPresetTable.NamedPresets[index], s);
            _page.Form.ReadBackFromSettings();
            RefreshPreset();
            PhotoMode.RefreshIfActive();
        }

        private void ResetSection()
        {
            var light = KitchenSettings.Instance.CaptureLightLook();
            SettingsSectionReset.Run(Loc.T("settings.reset.section"),
                (x, defaults) =>
                {
                    x.ResetPhotoLook();
                    x.ApplyLightLook(light);
                }, AfterReset);
        }

        private void AfterReset()
        {
            _page.Form.ReadBackFromSettings();
            RefreshPreset();
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
