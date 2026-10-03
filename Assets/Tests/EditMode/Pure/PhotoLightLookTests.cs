using KitchenDesigner.Core;
using NUnit.Framework;

/// <summary>
/// Вкладка «Свет» сбрасывается кнопкой «По умолчанию» одним шагом отмены. Для этого набор её
/// настроек описан ОДНОЙ структурой, которую можно снять, сравнить с заводской и записать
/// обратно. Здесь стережётся то, что на экране не видно: заводской набор равен тому, что
/// даёт <c>ResetToDefaults</c>, а снимок и запись не теряют ни одного поля.
/// </summary>
public class PhotoLightLookTests
{
    private static PhotoLightLook EveryFieldMoved() => new PhotoLightLook(
        ambientPct: 111, floorBouncePct: 112, ambientSkyPct: 113, ambientEquatorPct: 114,
        bounceMaxPct: 115, tonemap: KitchenSettings.PHOTO_TONEMAP_ACES, exposurePct: 117,
        contrastPct: 18, saturationPct: 19, bloomPct: 20, bloomThresholdPct: 121,
        bloomClampPct: 122, vignettePct: 23, sunShadowStrengthPct: 24, shadowDistanceM: 25,
        lampShadows: false);

    [Test]
    public void Defaults_EqualTheLookOfAFreshSettingsObject()
    {
        var settings = new KitchenSettings();
        settings.ResetToDefaults();

        Assert.AreEqual(PhotoLightLook.Defaults, settings.CaptureLightLook(),
            "заводской набор вкладки «Свет» обязан совпадать с тем, что даёт ResetToDefaults: "
            + "иначе кнопка «По умолчанию» вернёт значения, отличные от заводских, а сама "
            + "останется включённой на свежем проекте");
        Assert.IsTrue(settings.CaptureLightLook().IsDefault);
    }

    [Test]
    public void ApplyThenCapture_KeepsEveryField()
    {
        var settings = new KitchenSettings();
        var moved = EveryFieldMoved();

        settings.ApplyLightLook(moved);

        Assert.AreEqual(moved, settings.CaptureLightLook(),
            "поле, потерянное при записи или снятии, не сбросится кнопкой и не вернётся отменой");
        Assert.IsFalse(settings.CaptureLightLook().IsDefault,
            "пара к проверке заводского набора: сдвинутый набор не может считаться заводским");
    }

    [Test]
    public void EveryField_BreaksEqualityWithTheDefaults_WhenMovedAlone()
    {
        var d = PhotoLightLook.Defaults;
        var variants = new[]
        {
            new PhotoLightLook(d.AmbientPct + 1, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct + 1, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct + 1, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct + 1, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct + 1, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap + 1, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct + 1, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct + 1, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct + 1, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct + 1, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct + 1, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct + 1, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct + 1, d.SunShadowStrengthPct, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct + 1, d.ShadowDistanceM, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM + 1, d.LampShadows),
            new PhotoLightLook(d.AmbientPct, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct, d.BounceMaxPct, d.Tonemap, d.ExposurePct, d.ContrastPct, d.SaturationPct, d.BloomPct, d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct, d.ShadowDistanceM, !d.LampShadows),
        };

        for (int i = 0; i < variants.Length; i++)
            Assert.AreNotEqual(d, variants[i],
                $"поле №{i} структуры не участвует в сравнении: изменение этой настройки не включило бы кнопку");
    }
}
