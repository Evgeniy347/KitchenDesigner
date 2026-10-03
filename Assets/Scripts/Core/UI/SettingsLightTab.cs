using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsLightTab
    {
        private readonly SettingsRowFactory _rows;

        internal const string DefaultsButtonName = "Btn_LightDefaults";

        public SettingsLightTab(SettingsRowFactory rows) => _rows = rows;

        public void Build(Transform page, KitchenSettings s, float topY)
        {
            float y = topY;

            BuildDefaultsRow(page, ref y);

            _rows.AddHeader(page, ref y, Loc.T("settings.light.section.fill"));
            AddPhotoSlider(page, ref y, Loc.T("settings.light.ambient"), 0, KitchenSettings.PHOTO_AMBIENT_MAX_PCT,
                s.PhotoAmbientPct, Percent, v => s.PhotoAmbientPct = v, () => s.PhotoAmbientPct);
            Hint(Loc.T("settings.light.ambient"), hint: "settings.light.ambient");
            AddPhotoSlider(page, ref y, Loc.T("settings.light.floorBounce"), 0, KitchenSettings.PHOTO_FLOOR_BOUNCE_MAX_PCT,
                s.PhotoFloorBouncePct, Percent, v => s.PhotoFloorBouncePct = v, () => s.PhotoFloorBouncePct);
            Hint(Loc.T("settings.light.floorBounce"), hint: "settings.light.floorBounce");
            AddPhotoSlider(page, ref y, Loc.T("settings.light.sky"), 0, KitchenSettings.PHOTO_AMBIENT_PART_MAX_PCT,
                s.PhotoAmbientSkyPct, Percent, v => s.PhotoAmbientSkyPct = v, () => s.PhotoAmbientSkyPct);
            Hint(Loc.T("settings.light.sky"), hint: "settings.light.sky");
            AddPhotoSlider(page, ref y, Loc.T("settings.light.equator"), 0, KitchenSettings.PHOTO_AMBIENT_PART_MAX_PCT,
                s.PhotoAmbientEquatorPct, Percent, v => s.PhotoAmbientEquatorPct = v,
                () => s.PhotoAmbientEquatorPct);
            Hint(Loc.T("settings.light.equator"), hint: "settings.light.equator");
            AddPhotoSlider(page, ref y, Loc.T("settings.light.bounceMax"), 0, KitchenSettings.PHOTO_AMBIENT_PART_MAX_PCT,
                s.PhotoBounceMaxPct, Percent, v => s.PhotoBounceMaxPct = v, () => s.PhotoBounceMaxPct);
            Hint(Loc.T("settings.light.bounceMax"), hint: "settings.light.bounceMax");

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, Loc.T("settings.light.section.exposure"));
            AddPhotoSlider(page, ref y, Loc.T("settings.light.tonemap"),
                KitchenSettings.PHOTO_TONEMAP_NONE, KitchenSettings.PHOTO_TONEMAP_ACES,
                s.PhotoTonemap, TonemapName, v => s.PhotoTonemap = v, () => s.PhotoTonemap);
            Hint(Loc.T("settings.light.tonemap"), hint: "settings.light.tonemap");
            AddPhotoSlider(page, ref y, Loc.T("settings.light.exposure"),
                KitchenSettings.PHOTO_EXPOSURE_MIN_PCT, KitchenSettings.PHOTO_EXPOSURE_MAX_PCT,
                s.PhotoExposurePct, ExposureValue, v => s.PhotoExposurePct = v, () => s.PhotoExposurePct);
            Hint(Loc.T("settings.light.exposure"), hint: "settings.light.exposure");
            AddPhotoSlider(page, ref y, Loc.T("settings.light.contrast"),
                KitchenSettings.PHOTO_COLOR_MIN_PCT, KitchenSettings.PHOTO_COLOR_MAX_PCT,
                s.PhotoContrastPct, Percent, v => s.PhotoContrastPct = v, () => s.PhotoContrastPct);
            Hint(Loc.T("settings.light.contrast"), hint: "settings.light.contrast");
            AddPhotoSlider(page, ref y, Loc.T("settings.light.saturation"),
                KitchenSettings.PHOTO_COLOR_MIN_PCT, KitchenSettings.PHOTO_COLOR_MAX_PCT,
                s.PhotoSaturationPct, Percent, v => s.PhotoSaturationPct = v, () => s.PhotoSaturationPct);
            Hint(Loc.T("settings.light.saturation"), hint: "settings.light.saturation");

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, Loc.T("settings.light.section.effects"));
            AddPhotoSlider(page, ref y, Loc.T("settings.light.bloomStrength"), 0, KitchenSettings.PHOTO_BLOOM_MAX_PCT,
                s.PhotoBloomPct, Percent, v => s.PhotoBloomPct = v, () => s.PhotoBloomPct);
            Hint(Loc.T("settings.light.bloomStrength"), hint: "settings.light.bloomStrength");
            AddPhotoSlider(page, ref y, Loc.T("settings.light.bloomThreshold"), 0, KitchenSettings.PHOTO_BLOOM_THRESHOLD_MAX_PCT,
                s.PhotoBloomThresholdPct, Percent, v => s.PhotoBloomThresholdPct = v,
                () => s.PhotoBloomThresholdPct);
            Hint(Loc.T("settings.light.bloomThreshold"), hint: "settings.light.bloomThreshold");
            AddPhotoSlider(page, ref y, Loc.T("settings.light.bloomClamp"),
                KitchenSettings.PHOTO_BLOOM_CLAMP_MIN_PCT, KitchenSettings.PHOTO_BLOOM_CLAMP_MAX_PCT,
                s.PhotoBloomClampPct, Percent, v => s.PhotoBloomClampPct = v,
                () => s.PhotoBloomClampPct);
            Hint(Loc.T("settings.light.bloomClamp"), hint: "settings.light.bloomClamp");
            AddPhotoSlider(page, ref y, Loc.T("settings.light.vignetteStrength"), 0, FullPercent,
                s.PhotoVignettePct, Percent, v => s.PhotoVignettePct = v, () => s.PhotoVignettePct);
            Hint(Loc.T("settings.light.vignetteStrength"), hint: "settings.light.vignetteStrength");

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, Loc.T("settings.light.section.sceneShadows"));
            AddPhotoSlider(page, ref y, Loc.T("settings.light.sunShadowStrength"), 0, FullPercent,
                s.PhotoSunShadowStrengthPct, Percent, v => s.PhotoSunShadowStrengthPct = v,
                () => s.PhotoSunShadowStrengthPct);
            Hint(Loc.T("settings.light.sunShadowStrength"), hint: "settings.light.sunShadowStrength");
            AddPhotoSlider(page, ref y, Loc.T("settings.light.shadowDistance"),
                KitchenSettings.PHOTO_SHADOW_DISTANCE_MIN_M, KitchenSettings.PHOTO_SHADOW_DISTANCE_MAX_M,
                s.PhotoShadowDistanceM, Meters, v => s.PhotoShadowDistanceM = v,
                () => s.PhotoShadowDistanceM);
            Hint(Loc.T("settings.light.shadowDistance"), hint: "settings.light.shadowDistance");
            _rows.AddToggle(page, ref y, Loc.T("settings.light.lampShadows"), s.PhotoLampShadows,
                v =>
                {
                    s.PhotoLampShadows = v;
                    LightSourceElement.RefreshAll();
                    PhotoMode.RefreshIfActive();
                }, read: () => s.PhotoLampShadows);
            Hint(Loc.T("settings.light.lampShadows"), hint: "settings.light.lampShadows");
        }

        private void BuildDefaultsRow(Transform page, ref float y)
        {
            var row = SettingsRowFactory.CreateRow("RowLightDefaults", page, y);
            var button = UIFactory.CreateButton(DefaultsButtonName, row, Loc.T("settings.light.resetDefaults"),
                new Vector2(SettingsRowFactory.ContentW * 0.5f - SettingsRowFactory.ControlW * 0.5f, 0),
                new Vector2(SettingsRowFactory.ControlW, SettingsRowFactory.RowH), ResetToDefaults);
            SettingsDefaultsButton.Attach(button, () => KitchenSettings.Instance.CaptureLightLook().IsDefault);
            y -= SettingsRowFactory.RowStep;
        }

        private void ResetToDefaults()
        {
            var s = KitchenSettings.Instance;
            SetSettingCommand.Push(Loc.T("settings.light.resetDefaults"), s.ApplyLightLook,
                s.CaptureLightLook(), PhotoLightLook.Defaults, AfterReset);
        }

        private void AfterReset()
        {
            _rows.ReadBackFromSettings();
            LightSourceElement.RefreshAll();
            PhotoMode.RefreshIfActive();
        }

        private const int FullPercent = 100;

        private void AddPhotoSlider(Transform page, ref float y, string label, int min, int max,
            int value, System.Func<int, string> format, System.Action<int> apply, System.Func<int> read)
        {
            _rows.AddIntSlider(page, ref y, label, min, max, value, format,
                v => { apply(v); PhotoMode.RefreshIfActive(); }, read);
        }

        private static string TonemapName(int v) => v switch
        {
            KitchenSettings.PHOTO_TONEMAP_NONE => Loc.T("settings.light.tonemapNone"),
            KitchenSettings.PHOTO_TONEMAP_NEUTRAL => Loc.T("settings.light.tonemapNeutral"),
            _ => "ACES",
        };

        private static string Percent(int v) => v + " %";
        private static string Meters(int v) => v + Loc.T("unit.mSuffix");
        private static string ExposureValue(int v) =>
            (v / 100f).ToString("+0.0;-0.0;0.0", System.Globalization.CultureInfo.InvariantCulture) + " EV";

        private void Hint(string rowKey, string hint) =>
            HintBadge.AttachAfterLabel(_rows.RowLabel(rowKey), hint);
    }
}
