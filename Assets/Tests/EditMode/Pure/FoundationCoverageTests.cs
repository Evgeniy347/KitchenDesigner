using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>FND-05 сравнивает ТЕКУЩИЕ несущие стены с полилиниями существующей ленты — они
/// могут разойтись, если стену добавили или подвинули уже ПОСЛЕ того, как лента была
/// построена (F3, ещё не сделан). Поэтому проверка не «эта стена участвовала в построении
/// полилиний» (это тривиально верно всегда), а «эта стена лежит на одном из отрезков
/// полилиний» — геометрическое совпадение концов, а не участие в списке.</summary>
public class FoundationCoverageTests
{
    private static readonly Quaternion Rotate180AroundY = new Quaternion(0f, 1f, 0f, 0f);

    private static WallCentreline Wall(Vector3 centerUnits, Quaternion rotation,
        int lengthAxisXmm, int lengthAxisZmm) =>
        WallCentreline.Of(centerUnits, rotation, new Vector3Int(lengthAxisXmm, 2700, lengthAxisZmm));

    [Test]
    public void IsSegmentCovered_WallThatBuiltThePolyline_IsTrue()
    {
        var a = Wall(new Vector3(2f, 0f, 0f), Quaternion.identity, 4000, 250);
        var b = Wall(new Vector3(4f, 0f, 1.5f), Quaternion.identity, 250, 3000);
        var polylines = FoundationLayout.MergeIntoPolylines(new[] { a, b });

        Assert.IsTrue(FoundationCoverage.IsSegmentCovered(a, polylines),
            "стена A вошла в построение полилинии — она обязана считаться покрытой");
        Assert.IsTrue(FoundationCoverage.IsSegmentCovered(b, polylines),
            "то же самое для стены B");
    }

    [Test]
    public void IsSegmentCovered_AWallAddedAfterTheStripWasBuilt_IsFalse()
    {
        var a = Wall(new Vector3(2f, 0f, 0f), Quaternion.identity, 4000, 250);
        var b = Wall(new Vector3(4f, 0f, 1.5f), Quaternion.identity, 250, 3000);
        var polylines = FoundationLayout.MergeIntoPolylines(new[] { a, b });

        var addedLater = Wall(new Vector3(4f, 0f, -1f), Rotate180AroundY, 250, 2000);

        Assert.IsFalse(FoundationCoverage.IsSegmentCovered(addedLater, polylines),
            "FND-05: третья несущая стена появилась после того, как лента была построена "
            + "только из A и B — под ней ленты нет, и это обязано быть видно, а не потеряно "
            + "в общей длине контура");
    }

    [Test]
    public void IsSegmentCovered_AnUndefinedCentreline_IsFalse_NotAFalsePositive()
    {
        var a = Wall(new Vector3(2f, 0f, 0f), Quaternion.identity, 4000, 250);
        var polylines = FoundationLayout.MergeIntoPolylines(new[] { a });

        Assert.IsFalse(FoundationCoverage.IsSegmentCovered(default, polylines),
            "вырожденная осевая (IsDefined=false) не должна случайно совпасть с чем-либо и "
            + "пройти как «покрыта»");
    }

    [Test]
    public void IsSegmentCovered_EmptyPolylineList_IsFalse_ForAnyWall()
    {
        var a = Wall(new Vector3(2f, 0f, 0f), Quaternion.identity, 4000, 250);

        Assert.IsFalse(FoundationCoverage.IsSegmentCovered(a, System.Array.Empty<FoundationPolyline>()),
            "фундамента ещё нет вовсе — любая несущая стена не покрыта");
    }
}
