using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsLightTab
    {
        private readonly SettingsRowFactory _rows;

        public SettingsLightTab(SettingsRowFactory rows) => _rows = rows;

        public void Build(Transform page, KitchenSettings s, float topY)
        {
            float y = topY;

            _rows.AddHeader(page, ref y, "Заполняющий свет");
            AddPhotoSlider(page, ref y, "Окружающий свет", 0, KitchenSettings.PHOTO_AMBIENT_MAX_PCT,
                s.PhotoAmbientPct, Percent, v => s.PhotoAmbientPct = v, () => s.PhotoAmbientPct);
            AddPhotoSlider(page, ref y, "Отскок от пола", 0, KitchenSettings.PHOTO_FLOOR_BOUNCE_MAX_PCT,
                s.PhotoFloorBouncePct, Percent, v => s.PhotoFloorBouncePct = v, () => s.PhotoFloorBouncePct);
            AddPhotoSlider(page, ref y, "Потолок сверху", 0, KitchenSettings.PHOTO_AMBIENT_PART_MAX_PCT,
                s.PhotoAmbientSkyPct, Percent, v => s.PhotoAmbientSkyPct = v, () => s.PhotoAmbientSkyPct);
            AddPhotoSlider(page, ref y, "Стены сбоку", 0, KitchenSettings.PHOTO_AMBIENT_PART_MAX_PCT,
                s.PhotoAmbientEquatorPct, Percent, v => s.PhotoAmbientEquatorPct = v,
                () => s.PhotoAmbientEquatorPct);
            AddPhotoSlider(page, ref y, "Предел отскока", 0, KitchenSettings.PHOTO_AMBIENT_PART_MAX_PCT,
                s.PhotoBounceMaxPct, Percent, v => s.PhotoBounceMaxPct = v, () => s.PhotoBounceMaxPct);

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Экспозиция и тон");
            AddPhotoSlider(page, ref y, "Экспозиция",
                KitchenSettings.PHOTO_EXPOSURE_MIN_PCT, KitchenSettings.PHOTO_EXPOSURE_MAX_PCT,
                s.PhotoExposurePct, ExposureValue, v => s.PhotoExposurePct = v, () => s.PhotoExposurePct);
            AddPhotoSlider(page, ref y, "Контраст",
                KitchenSettings.PHOTO_COLOR_MIN_PCT, KitchenSettings.PHOTO_COLOR_MAX_PCT,
                s.PhotoContrastPct, Percent, v => s.PhotoContrastPct = v, () => s.PhotoContrastPct);
            AddPhotoSlider(page, ref y, "Насыщенность",
                KitchenSettings.PHOTO_COLOR_MIN_PCT, KitchenSettings.PHOTO_COLOR_MAX_PCT,
                s.PhotoSaturationPct, Percent, v => s.PhotoSaturationPct = v, () => s.PhotoSaturationPct);

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Эффекты");
            AddPhotoSlider(page, ref y, "Сила свечения", 0, KitchenSettings.PHOTO_BLOOM_MAX_PCT,
                s.PhotoBloomPct, Percent, v => s.PhotoBloomPct = v, () => s.PhotoBloomPct);
            AddPhotoSlider(page, ref y, "Порог свечения", 0, KitchenSettings.PHOTO_BLOOM_THRESHOLD_MAX_PCT,
                s.PhotoBloomThresholdPct, Percent, v => s.PhotoBloomThresholdPct = v,
                () => s.PhotoBloomThresholdPct);
            AddPhotoSlider(page, ref y, "Сила виньетки", 0, FullPercent,
                s.PhotoVignettePct, Percent, v => s.PhotoVignettePct = v, () => s.PhotoVignettePct);

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Тени сцены");
            AddPhotoSlider(page, ref y, "Сила теней солнца", 0, FullPercent,
                s.PhotoSunShadowStrengthPct, Percent, v => s.PhotoSunShadowStrengthPct = v,
                () => s.PhotoSunShadowStrengthPct);
            AddPhotoSlider(page, ref y, "Дальность теней",
                KitchenSettings.PHOTO_SHADOW_DISTANCE_MIN_M, KitchenSettings.PHOTO_SHADOW_DISTANCE_MAX_M,
                s.PhotoShadowDistanceM, Meters, v => s.PhotoShadowDistanceM = v,
                () => s.PhotoShadowDistanceM);
            _rows.AddToggle(page, ref y, "Тени от ламп", s.PhotoLampShadows,
                v =>
                {
                    s.PhotoLampShadows = v;
                    LightSourceElement.RefreshAll();
                    PhotoMode.RefreshIfActive();
                }, read: () => s.PhotoLampShadows);
        }

        private const int FullPercent = 100;

        private void AddPhotoSlider(Transform page, ref float y, string label, int min, int max,
            int value, System.Func<int, string> format, System.Action<int> apply, System.Func<int> read)
        {
            _rows.AddIntSlider(page, ref y, label, min, max, value, format,
                v => { apply(v); PhotoMode.RefreshIfActive(); }, read);
        }

        private static string Percent(int v) => v + " %";
        private static string Meters(int v) => v + " м";
        private static string ExposureValue(int v) =>
            (v / 100f).ToString("+0.0;-0.0;0.0", System.Globalization.CultureInfo.InvariantCulture) + " EV";
    }
}
