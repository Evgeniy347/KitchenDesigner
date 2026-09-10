using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Кромка как строка спецификации в погонных метрах (карта, «Кромка — это погонные
/// метры»), вместо только четырёх колонок Кромка L1..W2.</summary>
public class EdgeBandingSpecItemsTests
{
    [Test]
    public void For_800mm_Is0_8Meters()
    {
        var item = EdgeBandingSpecItems.For("0.4", 800);

        Assert.AreEqual(SpecUnit.LinearMeters, item.unit);
        Assert.AreEqual(0.8f, item.qty, 0.0001f);
        Assert.AreEqual(SpecSections.Furniture, item.section);
        Assert.IsFalse(item.hasDims, "кромка не короб — у неё нет WxHxD");
    }

    /// <summary>Противоположный вход: другая длина торца даёт другое число метров.</summary>
    [Test]
    public void For_DifferentLength_DifferentMeters()
    {
        var short_ = EdgeBandingSpecItems.For("0.4", 400);
        var long_ = EdgeBandingSpecItems.For("0.4", 1200);

        Assert.AreEqual(0.4f, short_.qty, 0.0001f);
        Assert.AreEqual(1.2f, long_.qty, 0.0001f);
    }

    /// <summary>Разная толщина кромки — разные строки: их нельзя смешивать в одну сумму метров.</summary>
    [Test]
    public void For_DifferentThickness_DifferentGroupKey()
    {
        var thin = EdgeBandingSpecItems.For("0.4", 800);
        var thick = EdgeBandingSpecItems.For("2.0", 800);

        Assert.AreNotEqual(thin.GroupKey(), thick.GroupKey());
    }
}
