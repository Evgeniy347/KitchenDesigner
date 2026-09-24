using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Геометрия и масса арматуры считаются с нуля, а не по таблице: масса погонного
/// метра стержня — это площадь сечения на плотность стали (7850 кг/м³, общее место
/// строительной механики, не пункт СП), и результат сходится с округлёнными значениями из
/// ГОСТ 34028 (0,617 / 0,888 / 1,578 кг/м для Ø10 / Ø12 / Ø16) с точностью до третьего знака —
/// таблица ГОСТ САМА посчитана этой же формулой. Поэтому здесь нет [Category
/// ("NormativeUnverified")]: коэффициент разрыхления грунта (FoundationLoosening) — отдельный,
/// действительно не сверенный вход, и его неопределённость не должна пачкать остальные пять
/// величин, которые считаются чистой геометрией.</summary>
public class FoundationQuantitiesTests
{
    [Test]
    public void RebarMassPerMetreKg_MatchesTheRoundedGost34028TableWithinRoundingError()
    {
        Assert.AreEqual(0.617d, FoundationQuantities.RebarMassPerMetreKg(10f), 0.002d,
            "Ø10: π×0,005² × 7850 = 0,6166 кг/м — таблица ГОСТ 34028 округляет её до 0,617");
        Assert.AreEqual(0.888d, FoundationQuantities.RebarMassPerMetreKg(12f), 0.002d,
            "Ø12: π×0,006² × 7850 = 0,8878 кг/м — таблица округляет до 0,888");
        Assert.AreEqual(1.578d, FoundationQuantities.RebarMassPerMetreKg(16f), 0.002d,
            "Ø16: π×0,008² × 7850 = 1,5783 кг/м — таблица округляет до 1,578");
    }

    [Test]
    public void Of_Strip10000x600x500_Sand100_Gravel100_OnLoam_MatchesHandCountedVolumes()
    {
        var result = FoundationQuantities.Of(SoilKind.Loam, 10000f, 600f, 500f, 100f, 100f,
            12f, 300f, 40f);

        Assert.AreEqual(0.6d, result.SandM3, 1e-6d, "10 × 0,6 × 0,1 = 0,6 м³ песка");
        Assert.AreEqual(0.6d, result.GravelM3, 1e-6d, "10 × 0,6 × 0,1 = 0,6 м³ щебня");
        Assert.AreEqual(3.0d, result.ConcreteM3, 1e-6d, "10 × 0,6 × 0,5 = 3,0 м³ бетона ленты");
        Assert.AreEqual(4.2d, result.ExcavationNaturalM3, 1e-6d,
            "траншея глубже самой ленты на толщину подушки: 10 × 0,6 × (0,5+0,1+0,1) = 4,2 м³ "
            + "в плотном теле");
        Assert.AreEqual(4.2d * 1.20d, result.ExcavationLooseM3, 1e-6d,
            "суглинок: Kp = 1,20 (FoundationLoosening, NormativeUnverified) — 4,2 × 1,20 = "
            + "5,04 м³ вывозимого рыхлого грунта");
        Assert.AreEqual(10.0d, result.FormworkM2, 1e-6d,
            "опалубка — два боковых борта траншеи: 2 × 10 × 0,5 = 10,0 м²");
    }

    [Test]
    public void RebarKg_Strip10000_Width600_Depth500_Cover40_Diameter12_Step300_Is_About92_3Kg()
    {
        double rebarKg = FoundationQuantities.RebarKg(10000f, 600f, 500f, 40f, 12f, 300f);

        Assert.AreEqual(92.28d, rebarKg, 0.1d,
            "4 продольных стержня по 10 м = 40 м. Хомуты: периметр (600−2×40)+(500−2×40) "
            + "с обеих сторон = 2×(520+420) = 1880 мм = 1,88 м; их ⌊10000/300⌋+1 = 34 штуки — "
            + "34 × 1,88 = 63,92 м. Итого 40 + 63,92 = 103,92 м × 0,888 кг/м (Ø12) ≈ 92,28 кг");
    }

    [Test]
    public void OfLoadBearingWalls_ReadsTheLengthFromFoundationLayout_NotFromACallerSum()
    {
        var a = WallCentreline.Of(new UnityEngine.Vector3(2f, 0f, 0f), UnityEngine.Quaternion.identity,
            new UnityEngine.Vector3Int(4000, 2700, 250));
        var b = WallCentreline.Of(new UnityEngine.Vector3(4f, 0f, 1.5f), UnityEngine.Quaternion.identity,
            new UnityEngine.Vector3Int(250, 2700, 3000));

        var result = FoundationQuantities.OfLoadBearingWalls(SoilKind.Sand, new[] { a, b },
            600f, 500f, 100f, 100f, 12f, 300f, 40f);

        Assert.AreEqual(7.0d * 0.6d * 0.5d, result.ConcreteM3, 1e-4d,
            "длина берётся из FoundationLayout.TotalLengthMm (Г-образный контур 4000+3000 = "
            + "7000 мм), а не пересчитывается заново здесь — иначе стык в общем углу считался "
            + "бы дважды тем же способом, что и в FoundationLayoutTests");
    }

    [Test]
    public void Of_ZeroLength_GivesZeroForEverything_NotNaNOrNegative()
    {
        var result = FoundationQuantities.Of(SoilKind.Clay, 0f, 600f, 500f, 100f, 100f,
            12f, 300f, 40f);

        Assert.AreEqual(0d, result.SandM3);
        Assert.AreEqual(0d, result.GravelM3);
        Assert.AreEqual(0d, result.ConcreteM3);
        Assert.AreEqual(0d, result.ExcavationNaturalM3);
        Assert.AreEqual(0d, result.ExcavationLooseM3);
        Assert.AreEqual(0d, result.FormworkM2);
        Assert.AreEqual(0d, result.RebarKg, "нулевая длина — ноль хомутов и ноль продольной "
            + "арматуры, а не отрицательное или NaN значение из деления/логарифма шага");
    }
}
