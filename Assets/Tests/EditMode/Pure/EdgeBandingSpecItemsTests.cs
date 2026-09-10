using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Кромка как строка спецификации в погонных метрах (карта, «Кромка — это погонные
/// метры»), вместо только четырёх колонок Кромка L1..W2.</summary>
public class EdgeBandingSpecItemsTests
{
    [Test]
    public void For_800mm_Is0_8Meters()
    {
        var item = EdgeBandingSpecItems.For("0.4", 800, "Белый");

        Assert.AreEqual(SpecUnit.LinearMeters, item.unit);
        Assert.AreEqual(0.8f, item.qty, 0.0001f);
        Assert.AreEqual(SpecSections.Furniture, item.section);
        Assert.IsFalse(item.hasDims, "кромка не короб — у неё нет WxHxD");
    }

    /// <summary>Противоположный вход: другая длина торца даёт другое число метров.</summary>
    [Test]
    public void For_DifferentLength_DifferentMeters()
    {
        var short_ = EdgeBandingSpecItems.For("0.4", 400, "Белый");
        var long_ = EdgeBandingSpecItems.For("0.4", 1200, "Белый");

        Assert.AreEqual(0.4f, short_.qty, 0.0001f);
        Assert.AreEqual(1.2f, long_.qty, 0.0001f);
    }

    /// <summary>Разная толщина кромки — разные строки: их нельзя смешивать в одну сумму метров.</summary>
    [Test]
    public void For_DifferentThickness_DifferentGroupKey()
    {
        var thin = EdgeBandingSpecItems.For("0.4", 800, "Белый");
        var thick = EdgeBandingSpecItems.For("2.0", 800, "Белый");

        Assert.AreNotEqual(thin.GroupKey(), thick.GroupKey());
    }

    /// <summary>Дефект приёмки №4: кромка продаётся по декору окантованной доски — та же
    /// толщина на двух разных декорах обязана лечь в РАЗНЫЕ строки, иначе закупка по одной
    /// общей строке физически невозможна (какой цвет заказывать?).</summary>
    [Test]
    public void For_DifferentMaterial_DifferentGroupKey()
    {
        var white = EdgeBandingSpecItems.For("0.4", 800, "Белый");
        var dark = EdgeBandingSpecItems.For("0.4", 800, "Ясень тёмный");

        Assert.AreNotEqual(white.GroupKey(), dark.GroupKey());
        Assert.AreEqual("Белый", white.material);
        Assert.AreEqual("Ясень тёмный", dark.material);
    }

    /// <summary>Противоположный вход: одинаковый декор — одна и та же группа, кромка не
    /// дробится на строки без причины.</summary>
    [Test]
    public void For_SameMaterial_SameGroupKey()
    {
        var a = EdgeBandingSpecItems.For("0.4", 800, "Белый");
        var b = EdgeBandingSpecItems.For("0.4", 400, "Белый");

        Assert.AreEqual(a.GroupKey(), b.GroupKey());
    }
}
