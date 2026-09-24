using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Ведомость фундамента — те же шесть чисел, что считает FoundationQuantities, только
/// разложенные по строкам SpecSections.Foundation. Ноль здесь означает «этого пункта в проекте
/// нет» (например, опалубка не нужна), а не «строка со значением 0» — иначе ведомость печатала
/// бы пустые строки для того, чего не заказывали.</summary>
public class FoundationSpecItemsTests
{
    private static readonly FoundationQuantitiesResult Sample = new FoundationQuantitiesResult(
        excavationNaturalM3: 4.2d, excavationLooseM3: 5.04d, sandM3: 0.6d, gravelM3: 0.6d,
        concreteM3: 3.0d, formworkM2: 10.0d, rebarKg: 92.28d);

    [Test]
    public void Of_SampleQuantities_ProducesSixLines_EachInFoundationSection()
    {
        var items = FoundationSpecItems.Of(Sample, "B20").ToList();

        Assert.AreEqual(6, items.Count, "шесть ненулевых величин — шесть строк, ни одна не "
            + "потеряна и ни одна не задвоена");
        foreach (var item in items)
            Assert.AreEqual(SpecSections.Foundation, item.section,
                "каждая строка фундамента обязана попасть в раздел «" + SpecSections.Foundation
                + "», иначе она осядет там же, где и стены");
    }

    [Test]
    public void Of_Excavation_UsesTheLooseVolume_NotTheNaturalOne()
    {
        var item = FoundationSpecItems.Of(Sample, "B20")
            .Single(i => i.name == FoundationSpecItems.ExcavationName);

        Assert.AreEqual(5.04f, item.qty, 1e-4f,
            "в ведомость и на вывоз идёт РЫХЛЫЙ объём (5,04 м³ = 4,2 × Kp), а не объём траншеи "
            + "в плотном теле (4,2 м³) — иначе заказ самосвалов будет занижен");
        Assert.AreEqual(SpecUnit.VolumeM3, item.unit);
    }

    [Test]
    public void Of_Concrete_CarriesTheGradeAsMaterial()
    {
        var item = FoundationSpecItems.Of(Sample, "B20")
            .Single(i => i.name == FoundationSpecItems.ConcreteName);

        Assert.AreEqual("B20", item.material,
            "класс бетона обязан быть виден В СТРОКЕ ведомости — иначе две ленты разных "
            + "классов сольются в одну сумму кубометров без возможности различить, где какой");
        Assert.AreEqual(3.0f, item.qty, 1e-4f);
    }

    [Test]
    public void Of_Rebar_IsInKilograms_NotInMetres()
    {
        var item = FoundationSpecItems.Of(Sample, "B20")
            .Single(i => i.name == FoundationSpecItems.RebarName);

        Assert.AreEqual(SpecUnit.Kilograms, item.unit,
            "арматура в ведомости покупается на вес, как и у стен/перекрытий — метры без веса "
            + "не сравнить с прайсом металлобазы");
        Assert.AreEqual(92.28f, item.qty, 1e-3f);
    }

    [Test]
    public void Of_AZeroQuantity_ProducesNoLine_NotALineWithZero()
    {
        var noFormwork = new FoundationQuantitiesResult(4.2d, 5.04d, 0.6d, 0.6d, 3.0d,
            formworkM2: 0d, rebarKg: 92.28d);

        var items = FoundationSpecItems.Of(noFormwork, "B20").ToList();

        Assert.IsFalse(items.Any(i => i.name == FoundationSpecItems.FormworkName),
            "опалубка не заказана (0 м²) — строки с нулём в ведомости быть не должно, "
            + "иначе пользователь ищет, чего в проекте нет");
        Assert.AreEqual(5, items.Count, "пять оставшихся ненулевых строк, не шесть");
    }

    [Test]
    public void Of_AllZero_ProducesNoLinesAtAll()
    {
        var empty = new FoundationQuantitiesResult(0d, 0d, 0d, 0d, 0d, 0d, 0d);

        Assert.IsEmpty(FoundationSpecItems.Of(empty, "B20"),
            "лента нулевой длины (нет несущих стен ещё) не должна печатать шесть пустых строк "
            + "в ведомости");
    }
}
