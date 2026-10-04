using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsLightTab
    {
        private readonly SettingsPage _page;

        public SettingsLightTab(SettingsPage page) => _page = page;

        public void Build(KitchenSettings s)
        {
            _page.OnReset = ResetToDefaults;
            _page.CanReset = () => !KitchenSettings.Instance.CaptureLightLook().IsDefault;

            _page.Section(Loc.T("settings.light.section.fill"));
            AddPhotoSlider(Loc.T("settings.light.ambient"), 0, KitchenSettings.PHOTO_AMBIENT_MAX_PCT,
                s.PhotoAmbientPct, Percent, v => s.PhotoAmbientPct = v, () => s.PhotoAmbientPct);
            Hint(Loc.T("settings.light.ambient"), hint: "settings.light.ambient");
            AddPhotoSlider(Loc.T("settings.light.floorBounce"), 0, KitchenSettings.PHOTO_FLOOR_BOUNCE_MAX_PCT,
                s.PhotoFloorBouncePct, Percent, v => s.PhotoFloorBouncePct = v, () => s.PhotoFloorBouncePct);
            Hint(Loc.T("settings.light.floorBounce"), hint: "settings.light.floorBounce");
            AddPhotoSlider(Loc.T("settings.light.sky"), 0, KitchenSettings.PHOTO_AMBIENT_PART_MAX_PCT,
                s.PhotoAmbientSkyPct, Percent, v => s.PhotoAmbientSkyPct = v, () => s.PhotoAmbientSkyPct);
            Hint(Loc.T("settings.light.sky"), hint: "settings.light.sky");
            AddPhotoSlider(Loc.T("settings.light.equator"), 0, KitchenSettings.PHOTO_AMBIENT_PART_MAX_PCT,
                s.PhotoAmbientEquatorPct, Percent, v => s.PhotoAmbientEquatorPct = v,
                () => s.PhotoAmbientEquatorPct);
            Hint(Loc.T("settings.light.equator"), hint: "settings.light.equator");
            AddPhotoSlider(Loc.T("settings.light.bounceMax"), 0, KitchenSettings.PHOTO_AMBIENT_PART_MAX_PCT,
                s.PhotoBounceMaxPct, Percent, v => s.PhotoBounceMaxPct = v, () => s.PhotoBounceMaxPct);
            Hint(Loc.T("settings.light.bounceMax"), hint: "settings.light.bounceMax");

            _page.Section(Loc.T("settings.light.section.exposure"));
            _page.AddDropdown(Loc.T("settings.light.tonemap"), TonemapNames(), s.PhotoTonemap,
                v => { s.PhotoTonemap = v; PhotoMode.RefreshIfActive(); },
                read: () => s.PhotoTonemap);
            Hint(Loc.T("settings.light.tonemap"), hint: "settings.light.tonemap");
            AddPhotoSlider(Loc.T("settings.light.exposure"),
                KitchenSettings.PHOTO_EXPOSURE_MIN_PCT, KitchenSettings.PHOTO_EXPOSURE_MAX_PCT,
                s.PhotoExposurePct, ExposureValue, v => s.PhotoExposurePct = v, () => s.PhotoExposurePct);
            Hint(Loc.T("settings.light.exposure"), hint: "settings.light.exposure");
            AddPhotoSlider(Loc.T("settings.light.contrast"),
                KitchenSettings.PHOTO_COLOR_MIN_PCT, KitchenSettings.PHOTO_COLOR_MAX_PCT,
                s.PhotoContrastPct, Percent, v => s.PhotoContrastPct = v, () => s.PhotoContrastPct);
            Hint(Loc.T("settings.light.contrast"), hint: "settings.light.contrast");
            AddPhotoSlider(Loc.T("settings.light.saturation"),
                KitchenSettings.PHOTO_COLOR_MIN_PCT, KitchenSettings.PHOTO_COLOR_MAX_PCT,
                s.PhotoSaturationPct, Percent, v => s.PhotoSaturationPct = v, () => s.PhotoSaturationPct);
            Hint(Loc.T("settings.light.saturation"), hint: "settings.light.saturation");

            _page.Section(Loc.T("settings.light.section.effects"));
            AddPhotoSlider(Loc.T("settings.light.bloomStrength"), 0, KitchenSettings.PHOTO_BLOOM_MAX_PCT,
                s.PhotoBloomPct, Percent, v => s.PhotoBloomPct = v, () => s.PhotoBloomPct);
            Hint(Loc.T("settings.light.bloomStrength"), hint: "settings.light.bloomStrength");
            AddPhotoSlider(Loc.T("settings.light.bloomThreshold"), 0, KitchenSettings.PHOTO_BLOOM_THRESHOLD_MAX_PCT,
                s.PhotoBloomThresholdPct, Percent, v => s.PhotoBloomThresholdPct = v,
                () => s.PhotoBloomThresholdPct);
            Hint(Loc.T("settings.light.bloomThreshold"), hint: "settings.light.bloomThreshold");
            AddPhotoSlider(Loc.T("settings.light.bloomClamp"),
                KitchenSettings.PHOTO_BLOOM_CLAMP_MIN_PCT, KitchenSettings.PHOTO_BLOOM_CLAMP_MAX_PCT,
                s.PhotoBloomClampPct, Percent, v => s.PhotoBloomClampPct = v,
                () => s.PhotoBloomClampPct);
            Hint(Loc.T("settings.light.bloomClamp"), hint: "settings.light.bloomClamp");
            AddPhotoSlider(Loc.T("settings.light.vignetteStrength"), 0, FullPercent,
                s.PhotoVignettePct, Percent, v => s.PhotoVignettePct = v, () => s.PhotoVignettePct);
            Hint(Loc.T("settings.light.vignetteStrength"), hint: "settings.light.vignetteStrength");

            _page.Section(Loc.T("settings.light.section.sceneShadows"));
            AddPhotoSlider(Loc.T("settings.light.sunShadowStrength"), 0, FullPercent,
                s.PhotoSunShadowStrengthPct, Percent, v => s.PhotoSunShadowStrengthPct = v,
                () => s.PhotoSunShadowStrengthPct);
            Hint(Loc.T("settings.light.sunShadowStrength"), hint: "settings.light.sunShadowStrength");
            AddPhotoSlider(Loc.T("settings.light.shadowDistance"),
                KitchenSettings.PHOTO_SHADOW_DISTANCE_MIN_M, KitchenSettings.PHOTO_SHADOW_DISTANCE_MAX_M,
                s.PhotoShadowDistanceM, Meters, v => s.PhotoShadowDistanceM = v,
                () => s.PhotoShadowDistanceM);
            Hint(Loc.T("settings.light.shadowDistance"), hint: "settings.light.shadowDistance");
            _page.AddSwitch(Loc.T("settings.light.lampShadows"), s.PhotoLampShadows,
                v =>
                {
                    s.PhotoLampShadows = v;
                    LightSourceElement.RefreshAll();
                    PhotoMode.RefreshIfActive();
                }, read: () => s.PhotoLampShadows);
            Hint(Loc.T("settings.light.lampShadows"), hint: "settings.light.lampShadows");
        }

        private void ResetToDefaults()
        {
            var s = KitchenSettings.Instance;
            SetSettingCommand.Push(Loc.T("settings.light.resetDefaults"), s.ApplyLightLook,
                s.CaptureLightLook(), PhotoLightLook.Defaults, AfterReset);
        }

        private void AfterReset()
        {
            _page.Form.ReadBackFromSettings();
            LightSourceElement.RefreshAll();
            PhotoMode.RefreshIfActive();
        }

        private const int FullPercent = 100;

        private void AddPhotoSlider(string label, int min, int max, int value,
            System.Func<int, string> format, System.Action<int> apply, System.Func<int> read)
        {
            _page.AddIntSlider(label, min, max, value, format,
                v => { apply(v); PhotoMode.RefreshIfActive(); }, read);
        }

        private static List<string> TonemapNames() => new()
        {
            TonemapName(KitchenSettings.PHOTO_TONEMAP_NONE),
            TonemapName(KitchenSettings.PHOTO_TONEMAP_NEUTRAL),
            TonemapName(KitchenSettings.PHOTO_TONEMAP_ACES),
        };

        private static string TonemapName(int v) => v switch
        {
            KitchenSettings.PHOTO_TONEMAP_NONE => Loc.T("settings.light.tonemapNone"),
            KitchenSettings.PHOTO_TONEMAP_NEUTRAL => Loc.T("settings.light.tonemapNeutral"),
            _ => "ACES",
        };

        private static string Percent(int v) => NumberFormat.WithUnit(NumberFormat.Integer(v), "%");
        private static string Meters(int v) => v + Loc.T("unit.mSuffix");
        private static string ExposureValue(int v) =>
            NumberFormat.WithUnit((v > 0 ? "+" : "") + NumberFormat.Fixed(v / 100f, 1), "EV");

        private void Hint(string rowKey, string hint) => _page.Hint(rowKey, hint);
    }
}
