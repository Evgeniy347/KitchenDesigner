using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>review-construction.md #1: FoundationElement validated as a 1x1x1 m cube at its
/// own transform (usually Vector3.zero from the sidebar spawner) instead of the real strip,
/// which FoundationStripMesh draws along the load-bearing walls regardless of where the
/// transform sits. FoundationFootprint gives FoundationElement a real world-space box to
/// validate against - the bounding rectangle of the built polylines, expanded by half the
/// strip width, at the correct vertical half-depth below grade.</summary>
public class FoundationFootprintTests
{
    [Test]
    public void Of_NoPolylines_IsZeroSizeAtOrigin()
    {
        var footprint = FoundationFootprint.Of(new List<FoundationPolyline>(), 700, 2300);

        Assert.AreEqual(Vector3.zero, footprint.CentreWorld,
            "без единой стены страховать нечего - центр остаётся в начале координат, как "
            + "пустой RoofFrame у RoofElement без стен");
        Assert.AreEqual(Vector3Int.zero, footprint.SizeMm);
    }

    [Test]
    public void Of_OneStraightWall_CentresOnItsMidpoint_AndWidensByHalfTheStripWidth()
    {
        var points = new List<Vector3> { new Vector3(10f, 0f, 5f), new Vector3(10f, 0f, 15f) };
        var polylines = new List<FoundationPolyline> { new FoundationPolyline(points, 10000f) };

        var footprint = FoundationFootprint.Of(polylines, 700, 2300);

        float toU = AppConstants.MM_TO_UNITS;
        Assert.AreEqual(10f, footprint.CentreWorld.x, 1e-4f,
            "прогон идёт строго по X=10 - центр по X обязан остаться на оси прогона, лента "
            + "не гуляет вбок от своей осевой");
        Assert.AreEqual(10f, footprint.CentreWorld.z, 1e-4f,
            "прогон от Z=5 до Z=15 - середина 10");
        Assert.AreEqual(-2300f * toU * 0.5f, footprint.CentreWorld.y, 1e-4f,
            "по вертикали лента уходит от отметки планировки вниз на всю глубину - центр "
            + "на половине глубины, как у FoundationStripMesh (centreY = -depthUnits/2)");

        Assert.AreEqual(700, footprint.SizeMm.x,
            "прогон - точка по X (10..10), раздутая на полширины в каждую сторону: "
            + "итоговая ширина короба по X равна ширине ленты, 700 мм");
        Assert.AreEqual(2300, footprint.SizeMm.y);
        Assert.AreEqual(10700, footprint.SizeMm.z,
            "вдоль прогона (Z, 10000 мм) короб длиннее самого прогона ровно на ширину ленты - "
            + "по полширины на каждый конец, как торцевой напуск полосы фундамента");
    }
}
