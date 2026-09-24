using NUnit.Framework;
using KitchenDesigner.Core.Construction;

/// <summary>Арматура плиты считается той же плотностью стали, что и лента фундамента —
/// FoundationQuantities.RebarMassPerMetreKg(диаметр) — а не второй копией формулы
/// «плотность × сечение». Здесь своя геометрия набора метров (сетка в две стороны по
/// прямоугольному пролёту), но масса метра арматуры одна на весь Pure/Construction.</summary>
public class SlabQuantitiesTests
{
    [Test]
    public void SlabQuantities_ConcreteM3_Area20m2_Thickness200mm_Is4m3()
    {
        Assert.AreEqual(4d, SlabQuantities.ConcreteM3(20d, 200f), 1e-9d,
            "20 м² × 0,2 м = 4 м³ — площадь полигона умножается на толщину напрямую, без "
            + "привязки к форме контура");
    }

    [Test]
    public void SlabQuantities_ConcreteM3_NegativeAreaOrZeroThickness_IsZero()
    {
        Assert.AreEqual(0d, SlabQuantities.ConcreteM3(-5d, 200f), 1e-9d,
            "отрицательная площадь — невалидный ввод, а не отрицательный бетон");
        Assert.AreEqual(0d, SlabQuantities.ConcreteM3(20d, 0f), 1e-9d,
            "нулевая толщина — нулевой объём, а не деление на ноль где-то дальше по цепочке");
    }

    [Test]
    public void SlabQuantities_MeshBarCountAcross_GrowsOnlyWhenAWholeStepIsCrossed()
    {
        Assert.AreEqual(1, SlabQuantities.MeshBarCountAcross(1f, 200f),
            "любой ненулевой пролёт — минимум один стержень");
        Assert.AreEqual(2, SlabQuantities.MeshBarCountAcross(200f, 200f),
            "ровно один шаг — два стержня, как WallQuantities.StudCount");
        Assert.AreEqual(2, SlabQuantities.MeshBarCountAcross(399f, 200f),
            "399 мм — ещё не два полных шага");
        Assert.AreEqual(3, SlabQuantities.MeshBarCountAcross(400f, 200f));
        Assert.AreEqual(0, SlabQuantities.MeshBarCountAcross(0f, 200f), "нулевой пролёт — стержней нет");
        Assert.AreEqual(0, SlabQuantities.MeshBarCountAcross(4000f, 0f),
            "нулевой шаг — невалидный ввод, а не стержень на каждый миллиметр");
    }

    [Test]
    public void SlabQuantities_MeshRebarLengthM_6000x4000_Step200mm_Is250m()
    {
        var lengthM = SlabQuantities.MeshRebarLengthM(6000f, 4000f, 200f);

        Assert.AreEqual(250d, lengthM, 1e-6d,
            "вдоль длины (6 м): 4 000 / 200 + 1 = 21 стержень × 4 м = 84... "
            + "нет: 21 стержень ИДЁТ по длине 6 м каждый — 21 × 6 = 126 м; "
            + "поперёк (вдоль ширины): 6 000 / 200 + 1 = 31 стержень × 4 м = 124 м; "
            + "126 + 124 = 250 м суммарно, посчитано руками");
    }

    [Test]
    public void SlabQuantities_MeshRebarLengthM_ZeroSpanOrStep_IsZero()
    {
        Assert.AreEqual(0d, SlabQuantities.MeshRebarLengthM(0f, 4000f, 200f), 1e-9d);
        Assert.AreEqual(0d, SlabQuantities.MeshRebarLengthM(6000f, 4000f, 0f), 1e-9d);
    }

    [Test]
    public void SlabQuantities_RebarKg_6000x4000_Step200mm_Diameter10mm_Is154_13kg()
    {
        var kg = SlabQuantities.RebarKg(6000f, 4000f, 200f, 10f);

        Assert.AreEqual(154.13438956674923d, kg, 1e-6d,
            "250 м (MeshRebarLengthM) × 0,616538 кг/м (FoundationQuantities."
            + "RebarMassPerMetreKg(10) — та же плотность стали 7850 кг/м³, что и у ленты "
            + "фундамента) = 154,134 кг");
    }

    [Test]
    public void SlabQuantities_RebarKg_UsesTheSamePerMetreMassAsFoundationQuantities()
    {
        var expectedPerMetre = FoundationQuantities.RebarMassPerMetreKg(12f);
        var kg = SlabQuantities.RebarKg(1000f, 1000f, 1000f, 12f);

        Assert.AreEqual(expectedPerMetre * SlabQuantities.MeshRebarLengthM(1000f, 1000f, 1000f),
            kg, 1e-9d,
            "плита не заводит вторую формулу «плотность × сечение» — она умножает свои "
            + "погонные метры на массу метра, взятую у FoundationQuantities");
    }

    [Test]
    public void SlabQuantities_RebarKg_ZeroStep_IsZero_NotACrash()
    {
        Assert.AreEqual(0d, SlabQuantities.RebarKg(6000f, 4000f, 0f, 10f), 1e-9d);
    }
}
