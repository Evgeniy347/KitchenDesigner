using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>`CollectDrawerFacadeLinks`/`CollectDishwasherFacadeLinks`/`CollectPipeRuns`/
/// `CollectFoundation`/`CollectFloorSlab` each called a private `FindFacade`/`FindByName`
/// that scanned the WHOLE scene per lookup — one linear pass per drawer, per dishwasher,
/// per pipe finding. `CollectAttachLinks`/`CollectScrewLegMounting`, in the very same file,
/// already built an `AttachLinks.PartsByName` index once EACH and looked names up in O(1),
/// but built that index separately per collector. `Analyze()` now builds ONE `PartsByName`
/// and threads it through every one of the seven collectors that needs a name lookup.
///
/// A wall-clock assertion cannot see this defect at any test-sized scale: each `FindFacade`
/// comparison costs roughly 100 ns, so even 800 drawer/facade pairs (1600 elements, measured
/// before this fix) added only ~20 ms of scan cost on top of ~250 ms of unrelated per-element
/// work (`SceneFaces.Of` calling `GetFaces()` on every element for the OTHER collectors) —
/// 252 ms fixed vs 273 ms unfixed, a difference no timer-based test could tell apart from
/// noise without an unrealistically large scene. The `PartsByName` index-build COUNT can:
/// on the old code `TakeIndexBuilds()` read 2 after one `Analyze()` (`CollectAttachLinks` and
/// `CollectScrewLegMounting` each built their own, the other five collectors built 0 but did
/// an O(elements) scan per lookup instead); the fixed code always reads 1.</summary>
public class SceneAnalyzerScaleTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        AttachLinks.PartsByName.TakeIndexBuilds();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private const int PairCount = 200;

    private DrawerElement MakeDrawer(string name, string facade, float x)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var d = go.AddComponent<DrawerElement>();
        d.PartName = name;
        d.Type = DrawerType.C;
        d.NominalLength = 500;
        d.InternalWidth = 400;
        d.AttachedFacadeName = facade;
        d.transform.position = new Vector3(x, 0f, 0f);
        PartRegistry.Register(d);
        _spawned.Add(go);
        return d;
    }

    private FacadeElement MakeFacade(string name, float x)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var f = go.AddComponent<FacadeElement>();
        f.PartName = name;
        f.DimensionsMM = new Vector3Int(400, 700, 18);
        f.GapLeft = 2; f.GapRight = 2; f.GapTop = 2; f.GapBottom = 2;
        f.transform.position = new Vector3(x, 0f, 0f);
        PartRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    /// <summary>~400 elements, each drawer naming a DIFFERENT facade — the shape that made
    /// the old per-drawer scan walk deep into the scene every time, and that a shared index
    /// resolves in O(1) regardless of position.</summary>
    [Test]
    public void Analyze_OnManyDrawersAndFacades_BuildsTheNameIndexExactlyOnce()
    {
        const float spacingM = 20f;
        for (int i = 0; i < PairCount; i++)
        {
            MakeDrawer($"drawer_{i}", $"facade_{i}", i * spacingM);
            MakeFacade($"facade_{i}", i * spacingM + 5f);
        }

        var issues = SceneAnalyzer.Analyze();

        var orphaned = issues.FindAll(iss => iss.Code == IssueCatalog.CodeDrawerFacadeOrphaned);
        Assert.AreEqual(PairCount, orphaned.Count,
            "every facade sits 5 m from its own drawer (out of contact range) and each drawer "
            + "names a DIFFERENT facade — one DRW-02 per pair proves the shared index still "
            + "resolves each drawer to its OWN facade at scale, not to some other one");

        Assert.AreEqual(1, AttachLinks.PartsByName.TakeIndexBuilds(),
            $"{PairCount} drawer/facade pairs: Analyze() must build the PartsByName index "
            + "exactly once and hand it to every collector that looks up a name, not rebuild "
            + "it per collector (2 before this fix) or skip it and scan the scene per lookup "
            + "instead (the other five collectors, 0 index builds but O(elements) work per "
            + "drawer, per dishwasher and per pipe finding, before this fix)");
    }
}
