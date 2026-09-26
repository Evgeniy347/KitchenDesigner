using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>FoundationRules.Collect — оркестратор, который SceneAnalyzer зовёт через
/// SceneFoundationSnapshot (F5): по списку FoundationSurvey и FoundationWallSpan строит
/// ConstructionFinding, склеивая шесть атомарных правил FoundationRules с текстами
/// FoundationFindings. Сами правила уже доказаны парами противоположных входов в
/// FoundationRulesTests — здесь проверяется только склейка: правильный код на правильном
/// elementId, и что стена ищет СВОЮ ленту, а не первую попавшуюся.</summary>
public class FoundationRulesCollectTests
{
    private static WallCentreline Wall(Vector3 centerUnits, int lengthAxisXmm, int lengthAxisZmm) =>
        WallCentreline.Of(centerUnits, Quaternion.identity,
            new Vector3Int(lengthAxisXmm, 2700, lengthAxisZmm));

    private static FoundationSurvey Foundation(string id, SoilKind soil, float widthMm, float depthMm,
        WallCentreline covering) =>
        new FoundationSurvey(id, soil, widthMm, depthMm, sandMm: 100f, gravelMm: 100f,
            compacted: true, rebarDiameterMm: 12f, rebarStepMm: widthMm, coverMm: 24f,
            frostDepthKnown: false, frostDepthMm: 0f,
            polylines: FoundationLayout.MergeIntoPolylines(new[] { covering }));

    [Test]
    public void Collect_NullFoundations_IsEmpty_NotACrash()
    {
        var findings = FoundationRules.Collect(null, null);
        Assert.AreEqual(0, findings.Count);
    }

    [Test]
    public void Collect_NoLoadBearingWalls_StillChecksTheFoundationItself()
    {
        var wall = Wall(new Vector3(2f, 0f, 0f), 4000, 250);
        var foundation = new FoundationSurvey("Lenta-1", SoilKind.Clay, 600f, 300f,
            sandMm: 100f, gravelMm: 100f, compacted: true, rebarDiameterMm: 12f, rebarStepMm: 600f,
            coverMm: 24f, frostDepthKnown: true, frostDepthMm: 1200f,
            polylines: FoundationLayout.MergeIntoPolylines(new[] { wall }));

        var findings = FoundationRules.Collect(new[] { foundation }, null);

        Assert.IsTrue(findings.Any(f => f.Code == "FND-01"),
            "заложение 300 мм на глине мельче глубины промерзания — FND-01 обязан прозвучать "
            + "даже когда список стен не передан вовсе");
    }

    [Test]
    public void Collect_CushionThinnerThanMinimum_ReportsFnd03()
    {
        var wall = Wall(new Vector3(2f, 0f, 0f), 4000, 250);
        var foundation = new FoundationSurvey("Lenta-1", SoilKind.Sand, 600f, 700f,
            sandMm: 50f, gravelMm: 100f, compacted: true, rebarDiameterMm: 12f, rebarStepMm: 600f,
            coverMm: 24f, frostDepthKnown: false, frostDepthMm: 0f,
            polylines: FoundationLayout.MergeIntoPolylines(new[] { wall }));

        var findings = FoundationRules.Collect(new[] { foundation }, null);

        Assert.AreEqual(1, findings.Count(f => f.Code == "FND-03"),
            "песок 50 мм тоньше минимума 100 мм при трамбовке — ровно одно нарушение FND-03");
        Assert.IsFalse(findings.Any(f => f.Code == "FND-01"),
            "песчаный грунт — FND-01 на нём не действует вовсе (FoundationRules.DepthMeetsFrostRule)");
    }

    [Test]
    public void Collect_RebarCoverAndStepBothOff_ReportsTwoFnd04Findings()
    {
        var wall = Wall(new Vector3(2f, 0f, 0f), 4000, 250);
        var foundation = new FoundationSurvey("Lenta-1", SoilKind.Sand, 600f, 700f,
            sandMm: 100f, gravelMm: 100f, compacted: true, rebarDiameterMm: 12f,
            rebarStepMm: 601f, coverMm: 23f, frostDepthKnown: false, frostDepthMm: 0f,
            polylines: FoundationLayout.MergeIntoPolylines(new[] { wall }));

        var findings = FoundationRules.Collect(new[] { foundation }, null);

        Assert.AreEqual(2, findings.Count(f => f.Code == "FND-04"),
            "23 мм защитного слоя (< 2×Ø12=24) и шаг 601 мм (> ширины 600) — два разных "
            + "нарушения FND-04 у одной и той же ленты, каждое по своему условию");
    }

    [Test]
    public void Collect_WallCoveredButSoleTooNarrowForTheSoil_ReportsFnd02_NamingTheWall()
    {
        var wall = Wall(new Vector3(2f, 0f, 0f), 4000, 250);
        var foundation = Foundation("Lenta-1", SoilKind.Loam, 400f, 700f, wall);
        var span = new FoundationWallSpan("Wall-A", wall, thicknessMm: 250f);

        var findings = FoundationRules.Collect(new[] { foundation }, new[] { span });

        var fnd02 = findings.Single(f => f.Code == "FND-02");
        Assert.AreEqual("Wall-A", fnd02.ElementId,
            "FND-02 обязан называть СТЕНУ, у которой не хватает подошвы, а не ленту "
            + "(минимум для суглинка и стены 250 мм — 450 мм, лента у́же на 50 мм)");
    }

    [Test]
    public void Collect_WallCoveredAndSoleWideEnough_ReportsNoFnd02()
    {
        var wall = Wall(new Vector3(2f, 0f, 0f), 4000, 250);
        var foundation = Foundation("Lenta-1", SoilKind.Loam, 700f, 700f, wall);
        var span = new FoundationWallSpan("Wall-A", wall, thicknessMm: 250f);

        var findings = FoundationRules.Collect(new[] { foundation }, new[] { span });

        Assert.IsFalse(findings.Any(f => f.Code == "FND-02"),
            "700 мм шире минимума 450 мм для суглинка и стены 250 мм — противоположный вход "
            + "к предыдущему тесту");
    }

    [Test]
    public void Collect_WallNotCoveredByAnyFoundation_ReportsFnd05_AndSkipsFnd02()
    {
        var covered = Wall(new Vector3(2f, 0f, 0f), 4000, 250);
        var notCovered = Wall(new Vector3(4f, 0f, -1f), 250, 2000);
        var foundation = Foundation("Lenta-1", SoilKind.Loam, 1f, 700f, covered);
        var span = new FoundationWallSpan("Wall-Later", notCovered, thicknessMm: 250f);

        var findings = FoundationRules.Collect(new[] { foundation }, new[] { span });

        Assert.AreEqual(1, findings.Count, "ровно одно нарушение — FND-05, не два");
        var fnd05 = findings.Single();
        Assert.AreEqual("FND-05", fnd05.Code);
        Assert.AreEqual("Wall-Later", fnd05.ElementId,
            "непокрытая стена обязана быть названа по имени, а не по ленте");
    }
}
