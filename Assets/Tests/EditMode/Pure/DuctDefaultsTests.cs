using NUnit.Framework;
using KitchenDesigner.Core.Ventilation;

/// <summary>Обычные дефолты полей воздуховода (не цитаты пункта норматива, как PostStepMm у
/// FenceDefaults) — тест называет их поимённо, чтобы правка была видна как красная строка.
/// Диапазон 100х150...300х300 подтверждён пользователем (DuctRectSizesTests); середина этого
/// диапазона и круглого ряда 100..315 взята дефолтом поля, без претензии на точную цитату.</summary>
public class DuctDefaultsTests
{
    [Test]
    public void DefaultLengthMm_Is1250_TheStandardSheetMetalSectionLength()
    {
        Assert.AreEqual(1250, DuctDefaults.DefaultLengthMm);
    }

    [Test]
    public void DefaultRoundDiameterMm_IsWithinTheConfirmedRoundRange()
    {
        Assert.GreaterOrEqual(DuctDefaults.DefaultRoundDiameterMm, DuctProfile.MinRoundDiameterMm);
        Assert.LessOrEqual(DuctDefaults.DefaultRoundDiameterMm, DuctProfile.MaxRoundDiameterMm);
    }

    [Test]
    public void DefaultRectSize_IsOneOfTheConfirmedCandidates()
    {
        CollectionAssert.Contains(DuctRectSizes.Candidates,
            (DuctDefaults.DefaultRectWidthMm, DuctDefaults.DefaultRectHeightMm),
            "дефолт прямоугольного сечения обязан быть одним из подтверждённого ряда, "
            + "а не произвольным числом рядом с ним");
    }

    [Test]
    public void AirflowRange_DefaultLiesInsideMinMax()
    {
        Assert.GreaterOrEqual(DuctDefaults.DefaultAirflowM3PerHour, DuctDefaults.MinAirflowM3PerHour);
        Assert.LessOrEqual(DuctDefaults.DefaultAirflowM3PerHour, DuctDefaults.MaxAirflowM3PerHour);
    }
}
