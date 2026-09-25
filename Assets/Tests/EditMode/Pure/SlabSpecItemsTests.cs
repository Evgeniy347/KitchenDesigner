using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Ведомость плиты перекрытия — SpecSections.Structures, две строки (бетон, арматура)
/// для монолитной плиты. Технология "Joists" (балочное перекрытие) не считается этим кодом
/// вообще — S1 не строил формулу для неё, и GetSpecItems честно возвращает ноль строк, а не
/// подставляет числа плиты под другую технологию.</summary>
public class SlabSpecItemsTests
{
    [Test]
    public void Of_Slab_6000x4000_Thickness200_ProducesConcreteAndRebarLines()
    {
        var items = SlabSpecItems.Of(SlabTechnology.Slab, 6000f, 4000f, 200f, 12f, 200f, "B20")
            .ToList();

        Assert.AreEqual(2, items.Count, "два ненулевых числа — две строки, бетон и арматура");
        foreach (var item in items)
            Assert.AreEqual(SpecSections.Structures, item.section,
                "строка плиты обязана попасть в раздел «" + SpecSections.Structures
                + "», иначе она осядет не в том разделе ведомости");
    }

    [Test]
    public void Of_Concrete_CarriesTheGradeAsMaterial_AndMatchesAreaTimesThickness()
    {
        var item = SlabSpecItems.Of(SlabTechnology.Slab, 6000f, 4000f, 200f, 12f, 200f, "B25")
            .Single(i => i.name == SlabSpecItems.ConcreteName);

        Assert.AreEqual("B25", item.material,
            "класс бетона обязан быть виден в строке — иначе плиты разных классов сольются");
        Assert.AreEqual(4.8f, item.qty, 1e-4f,
            "6 м × 4 м × 0,2 м = 4,8 м³ ровно, как и считает SlabQuantities.ConcreteM3");
        Assert.AreEqual(SpecUnit.VolumeM3, item.unit);
    }

    [Test]
    public void Of_Rebar_IsInKilograms_NotInMetres()
    {
        var item = SlabSpecItems.Of(SlabTechnology.Slab, 6000f, 4000f, 200f, 12f, 200f, "B20")
            .Single(i => i.name == SlabSpecItems.RebarName);

        Assert.AreEqual(SpecUnit.Kilograms, item.unit,
            "арматура покупается на вес, как у фундамента — метры без веса не сравнить с прайсом");
        Assert.Greater(item.qty, 0f);
    }

    [Test]
    public void Of_ZeroArea_ProducesNoLinesAtAll()
    {
        var items = SlabSpecItems.Of(SlabTechnology.Slab, 0f, 4000f, 200f, 12f, 200f, "B20");

        Assert.IsEmpty(items,
            "нулевая длина — нулевая площадь и нулевой объём: строк с нулём быть не должно");
    }

    [Test]
    public void Of_JoistsTechnology_ProducesNoLines_NotSlabNumbersUnderAnotherName()
    {
        var items = SlabSpecItems.Of(SlabTechnology.Joists, 6000f, 4000f, 200f, 12f, 200f, "B20");

        Assert.IsEmpty(items,
            "балочное перекрытие не считается формулой монолитной плиты — S1 не строил для "
            + "него отдельный расчёт, и честный ответ здесь — пусто, а не числа плиты под "
            + "чужой технологией");
    }
}
