using NUnit.Framework;
using KitchenDesigner.Core.Ventilation;

/// <summary>VNT-01: скорость воздуха в воздуховоде против рекомендуемых порогов (СП
/// 60.13330.2020, приложение Л, табл. Л.1 — числа NormativeUnverified, см.
/// docs/NORMATIVE-DEFAULTS.md §6). Здесь — только арифметика (площадь сечения, скорость),
/// без сцены и без выбора порога по связности (это DuctRulesTests).</summary>
public class DuctVelocityTests
{
    [Test]
    public void CrossSectionAreaM2_Round_IsPiRSquared()
    {
        var profile = DuctProfile.Round(200);
        float expected = (float)System.Math.PI * 0.1f * 0.1f;
        Assert.AreEqual(expected, DuctVelocity.CrossSectionAreaM2(profile), 1e-5f);
    }

    [Test]
    public void CrossSectionAreaM2_Rect_IsWidthTimesHeight()
    {
        var profile = DuctProfile.Rect(200, 150);
        Assert.AreEqual(0.2f * 0.15f, DuctVelocity.CrossSectionAreaM2(profile), 1e-6f);
    }

    [Test]
    public void MetresPerSecond_1000M3PerHour_Through1M2_Is0Point2778()
    {
        // 1000 м³/ч = 0,2778 м³/с; через 1 м² сечения — та же скорость числом.
        Assert.AreEqual(1000f / 3600f, DuctVelocity.MetresPerSecond(1000f, 1f), 1e-6f);
    }

    [Test]
    public void MetresPerSecond_ZeroArea_IsZero_NotDivisionByZero()
    {
        Assert.AreEqual(0f, DuctVelocity.MetresPerSecond(500f, 0f),
            "нулевое сечение — вырожденный случай, а не NaN/Infinity в найденной проблеме");
    }

    [Test]
    [Category("NormativeUnverified")]
    public void MaxRecommendedMs_OrdersMainAboveBranchAboveNearGrille()
    {
        float main = DuctVelocity.MaxRecommendedMs(DuctVelocityTier.Main);
        float branch = DuctVelocity.MaxRecommendedMs(DuctVelocityTier.Branch);
        float nearGrille = DuctVelocity.MaxRecommendedMs(DuctVelocityTier.NearGrille);

        Assert.Greater(main, branch, "магистраль допускает бОльшую скорость, чем ответвление");
        Assert.Greater(branch, nearGrille, "перед решёткой предел ниже, чем в ответвлении");
    }
}
