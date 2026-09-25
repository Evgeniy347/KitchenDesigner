using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>S4: FloorSlabRules подключён к SceneAnalyzer через SceneSlabSnapshot. The rule
/// itself (FLR-01, "gap to supporting wall") is proven on bare FloorSlabSurvey values in
/// FloorSlabRulesTests — this file proves the WIRING: a real Wall and a real FloorSlabElement,
/// spawned through ElementFactory, produce the right gap number and the right FLR-01 verdict
/// through the live scene snapshot, not through a hand-built survey.
///
/// The wall spans Y 0..2700mm (ElementFactory.CreateWall centres its transform, exactly like
/// SceneFoundationAnalysisTests' WallA: position.y = height/2 in units). The slab's own centre
/// is placed so its bottom face sits at a chosen height above the wall's top (2700mm) — the
/// scene snapshot must recover that same gap without being told it directly.</summary>
public class SceneFloorSlabAnalysisTests
{
    private const int SlabThicknessMm = 200;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private Wall SpawnLoadBearingWall()
    {
        var go = ElementFactory.CreateWall(new Vector3Int(4000, 2700, 250), "Wall",
            new Vector3(2f, 1.35f, 0f));
        _spawned.Add(go);
        var wall = go.GetComponent<Wall>();
        Assert.IsTrue(wall.LoadBearing,
            "ElementFactory.CreateWall по умолчанию ставит несущую кирпичную стену — тест "
            + "полагается на этот дефолт, а не переопределяет его сам");
        return wall;
    }

    private FloorSlabElement SpawnSlabWithBottomAtHeightMm(float bottomHeightMm)
    {
        var go = ElementFactory.CreateFloorSlab(4000, 3000, "Slab", Vector3.zero);
        _spawned.Add(go);
        var slab = go.GetComponent<FloorSlabElement>();
        slab.DimensionsMM = new Vector3Int(4000, SlabThicknessMm, 3000);

        float centreYMm = bottomHeightMm + SlabThicknessMm * 0.5f;
        var t = slab.transform;
        t.position = new Vector3(2f, centreYMm * 0.001f, 1.5f);
        slab.ApplyDimensions();
        return slab;
    }

    [Test]
    public void SlabFlushWithTheWallTop_Analyze_ReportsNoFlr01()
    {
        SpawnLoadBearingWall();
        SpawnSlabWithBottomAtHeightMm(2700f);

        var issues = SceneAnalyzer.Analyze();

        Assert.IsFalse(issues.Any(i => i.Code == IssueCatalog.CodeFloorSlabGapToSupportingWall),
            "плита ровно на стене (зазор 0) — FLR-01 звучать не должен, это противоположный "
            + "вход к следующему тесту");
    }

    [Test]
    public void SlabFloatingAboveTheWall_Analyze_ReportsFlr01()
    {
        SpawnLoadBearingWall();
        var slab = SpawnSlabWithBottomAtHeightMm(2710f);

        var issues = SceneAnalyzer.Analyze();

        Assert.IsTrue(issues.Any(i => i.Code == IssueCatalog.CodeFloorSlabGapToSupportingWall),
            "плита висит на 10 мм выше стены — FLR-01 обязан сработать через живой снимок "
            + "сцены, а не только на голом FloorSlabSurvey");
        var found = issues.First(i => i.Code == IssueCatalog.CodeFloorSlabGapToSupportingWall);
        Assert.AreEqual(slab.PartName, found.Detail);
    }

    [Test]
    public void SlabWithNoOverlappingWallBeneath_Analyze_ReportsNothing_NotACrash()
    {
        SpawnSlabWithBottomAtHeightMm(2700f);

        var issues = SceneAnalyzer.Analyze();

        Assert.IsFalse(issues.Any(i => i.Code == IssueCatalog.CodeFloorSlabGapToSupportingWall),
            "без единой несущей стены под плитой сравнивать не с чем — снимок обязан честно "
            + "промолчать про эту плиту, а не наугад завести число");
    }
}
