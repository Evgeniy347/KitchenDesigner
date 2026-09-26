using NUnit.Framework;
using KitchenDesigner.Core.Ventilation;

/// <summary>VNT-03, направление подтверждено (СП 60.13330 задаёт нормы воздухообмена по
/// помещениям), но точный пункт исполнителем не сверен и таблицы норм по типу помещения в
/// проекте нет (см. DuctRules.cs, RoomAirExchange.cs). Используется задокументированная в
/// задании общая альтернатива — кратность не менее 1 объём/час — см. NormativeUnverified,
/// docs/NORMATIVE-DEFAULTS.md §6.</summary>
[Category("NormativeUnverified")]
public class RoomAirExchangeTests
{
    [Test]
    public void RequiredM3PerHour_IsVolumeTimesOneAirChangePerHour()
    {
        Assert.AreEqual(30f, RoomAirExchange.RequiredM3PerHour(30f), 1e-6f,
            "кратность 1 об/ч — требуемый расход численно равен объёму помещения в м³");
    }

    [Test]
    public void IsBelowNorm_SuppliedLessThanVolume_IsTrue()
    {
        Assert.IsTrue(RoomAirExchange.IsBelowNorm(20f, 30f));
    }

    [Test]
    public void IsBelowNorm_SuppliedEqualsVolume_IsFalse_NotBelow()
    {
        Assert.IsFalse(RoomAirExchange.IsBelowNorm(30f, 30f),
            "ровно норма — это выполненное требование, а не нарушение");
    }

    [Test]
    public void IsBelowNorm_SuppliedExceedsVolume_IsFalse()
    {
        Assert.IsFalse(RoomAirExchange.IsBelowNorm(40f, 30f));
    }
}
