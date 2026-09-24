using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>RoofContour не строит настоящий многоугольник по графу стен — он только
/// охватывает все концы отрезков минимальным прямоугольником. Для прямоугольного дома это
/// и есть внешний контур; для Г-образного — уже нет, но ничего точнее сегодня не посчитано.
/// Это ровно тот «откат к прямоугольнику», о котором просил координатор для вальмовой
/// крыши (RoofPitchPlanesTests), и здесь он назван на уровне контура: перегородка внутри
/// периметра не меняет прямоугольник, хотя настоящий контур Г-образного дома у неё
/// отличался бы.</summary>
public class RoofContourTests
{
    private static WallCentreline Wall(Vector3 centerUnits, int lengthAxisXmm, int lengthAxisZmm) =>
        WallCentreline.Of(centerUnits, Quaternion.identity,
            new Vector3Int(lengthAxisXmm, 2700, lengthAxisZmm));

    private static WallCentreline[] RectangularWalls6000By4000()
    {
        return new[]
        {
            Wall(new Vector3(3f, 0f, 0f), 6000, 250),
            Wall(new Vector3(3f, 0f, 4f), 6000, 250),
            Wall(new Vector3(0f, 0f, 2f), 250, 4000),
            Wall(new Vector3(6f, 0f, 2f), 250, 4000),
        };
    }

    [Test]
    public void RoofContour_RectangularWalls_BoundingFootprint_Is6000By4000Mm()
    {
        var footprint = RoofContour.BoundingFootprint(RectangularWalls6000By4000());

        Assert.AreEqual(0f, footprint.MinXMm, 0.5f);
        Assert.AreEqual(6000f, footprint.MaxXMm, 0.5f);
        Assert.AreEqual(0f, footprint.MinZMm, 0.5f);
        Assert.AreEqual(4000f, footprint.MaxZMm, 0.5f,
            "4 стены прямоугольника 6 × 4 м дают прямоугольник 6000 × 4000 мм — "
            + "посчитано по угловым точкам вручную");
        Assert.AreEqual(6000f, footprint.WidthXMm, 0.5f);
        Assert.AreEqual(4000f, footprint.LengthZMm, 0.5f);
        Assert.IsTrue(footprint.LongAxisIsX, "6 000 мм по X длиннее 4 000 мм по Z");
    }

    [Test]
    public void RoofContour_InteriorPartitionWellWithinTheOuterWalls_DoesNotChangeTheFootprint()
    {
        var walls = RectangularWalls6000By4000();
        var withPartition = new WallCentreline[walls.Length + 1];
        walls.CopyTo(withPartition, 0);
        withPartition[walls.Length] = Wall(new Vector3(2f, 0f, 2f), 250, 2000);

        var footprintWithout = RoofContour.BoundingFootprint(walls);
        var footprintWith = RoofContour.BoundingFootprint(withPartition);

        Assert.AreEqual(footprintWithout.MinXMm, footprintWith.MinXMm, 0.5f);
        Assert.AreEqual(footprintWithout.MaxXMm, footprintWith.MaxXMm, 0.5f);
        Assert.AreEqual(footprintWithout.MinZMm, footprintWith.MinZMm, 0.5f);
        Assert.AreEqual(footprintWithout.MaxZMm, footprintWith.MaxZMm, 0.5f,
            "перегородка на 2, от Z=1 до Z=3 м, целиком внутри периметра 0..4 м по Z и не "
            + "трогает X=2 м — граница прямоугольника обязана остаться прежней. Настоящий "
            + "Г-образный контур на месте такой перегородки имел бы вырез, но "
            + "BoundingFootprint его не видит: это и есть откат к прямоугольнику");
    }

    [Test]
    public void RoofContour_NullCentrelines_IsAllZero()
    {
        var footprint = RoofContour.BoundingFootprint(null);

        Assert.AreEqual(0f, footprint.MinXMm, 1e-6f);
        Assert.AreEqual(0f, footprint.MaxXMm, 1e-6f);
        Assert.AreEqual(0f, footprint.MinZMm, 1e-6f);
        Assert.AreEqual(0f, footprint.MaxZMm, 1e-6f);
    }

    [Test]
    public void RoofContour_OnlyUndefinedCentrelines_AreSkipped_NotCrashed()
    {
        var undefined = WallCentreline.Of(Vector3.zero, Quaternion.identity, new Vector3Int(0, 2700, 0));
        Assert.IsFalse(undefined.IsDefined, "нулевая длина стены — это WallCentreline.Of, "
            + "возвращающий default, как и задокументировано в самом типе");

        var footprint = RoofContour.BoundingFootprint(new[] { undefined });

        Assert.AreEqual(0f, footprint.MaxXMm, 1e-6f,
            "неопределённая стена не должна попасть в границы наравне с настоящей");
    }
}
