using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>review-construction.md #1: FenceElement kept `transform.localScale = Vector3.one`
/// and never overrode EffectiveScale, so its validation body was a 1x1x1 m cube around the
/// pivot instead of the real 6000x2000x8 sheet - a cabinet 2 m from the fence's centre went
/// unnoticed, and one 400 mm in front of the (8 mm) sheet raised a false Overlap. PipeElement
/// and RoofElement already show the fix: EffectiveScale from FurnitureLayout.PhysicalScale.</summary>
public class FenceElementValidationBoxTests
{
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

    private FenceElement SpawnFence(Vector3 position)
    {
        var go = ElementFactory.CreateFence(6000, 2000, "Fence", position);
        _spawned.Add(go);
        return go.GetComponent<FenceElement>();
    }

    private GameObject SpawnBox(Vector3Int dimensionsMm, string name, Vector3 position)
    {
        var go = ElementFactory.CreatePart(dimensionsMm, name, position);
        _spawned.Add(go);
        return go;
    }

    [Test]
    public void GetVertices_SpansTheRealSheet_NotAOneMeterCube()
    {
        var fence = SpawnFence(Vector3.zero);

        var vertices = fence.GetVertices();
        float minX = vertices.Min(v => v.x), maxX = vertices.Max(v => v.x);
        float minZ = vertices.Min(v => v.z), maxZ = vertices.Max(v => v.z);

        Assert.AreEqual(6f, maxX - minX, 0.01f,
            "забор 6000 мм длиной обязан проверяться на всю длину, 6 м, а не на 1 м куба");
        Assert.Less(maxZ - minZ, 0.5f,
            "лист профнастила 8 мм толщиной не должен раздуваться до метра по толщине - "
            + "иначе цель в 400 мм перед листом ловится, хотя до листа далеко");
    }

    [Test]
    public void CabinetTwoMetresFromCentre_TouchingTheFence_IsCaught()
    {
        SpawnFence(Vector3.zero);
        SpawnBox(new Vector3Int(400, 400, 400), "Cabinet", new Vector3(2f, 0f, 0f));

        var issues = SceneAnalyzer.Analyze();

        Assert.IsTrue(issues.Any(i => i.Code == IssueCatalog.CodeOverlap),
            "шкаф в 2 м от центра забора (6000 мм длиной, реальная половина досягаемости "
            + "3 м) касается листа - COL-01 обязан сработать; куб 1x1x1 м вокруг пивота "
            + "этого не видел (review-construction.md #1)");
    }

    [Test]
    public void CabinetFourHundredMillimetresInFrontOfTheThinSheet_IsNotCaught()
    {
        SpawnFence(Vector3.zero);
        SpawnBox(new Vector3Int(400, 400, 400), "Cabinet", new Vector3(0f, 0f, 0.4f));

        var issues = SceneAnalyzer.Analyze();

        Assert.IsFalse(issues.Any(i => i.Code == IssueCatalog.CodeOverlap),
            "лист профнастила — 8 мм толщиной, шкаф в 400 мм перед ним нигде его не "
            + "касается; куб 1x1x1 м вокруг пивота раньше ловил его как ложное "
            + "пересечение (review-construction.md #1)");
    }
}
