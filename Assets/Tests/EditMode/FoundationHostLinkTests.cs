using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>test-results/review-construction.md #10: the strip and its cached BuiltPolylines
/// were rebuilt only from FoundationElement.ApplyDimensions, which itself only ran on a
/// width/depth change — adding, moving or deleting a load-bearing wall left the strip stale,
/// and the only in-app remedy was to nudge the width and back. FoundationHostLink gives the
/// scene's periodic settle pass (SceneChangeTracker.SettleDerivedLinks, the same place
/// ScrewLegHostLink and PipeFittingSizeLink already rebuild THEIR derived geometry) a reason
/// to rebuild the foundation too, but only when the walls it was last built from actually
/// differ from the current ones — "есть ли у того, что я сейчас построю, хоть один читатель"
/// (agents/TEST-DESIGN.md) guards the empty-scene case, and the centreline comparison guards
/// against rebuilding a mesh nobody asked to change.</summary>
public class FoundationHostLinkTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private GameObject SpawnWall(Vector3Int dimensionsMm, string name, Vector3 position)
    {
        var go = ElementFactory.CreateWall(dimensionsMm, name, position);
        _spawned.Add(go);
        return go;
    }

    private FoundationElement SpawnFoundation()
    {
        var go = ElementFactory.CreateFoundation(FoundationElement.DEFAULT_WIDTH_MM,
            FoundationElement.DEFAULT_DEPTH_MM, "Foundation", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<FoundationElement>();
    }

    [Test]
    public void ApplyAll_NoFoundationInScene_ReturnsZero_NotACrash()
    {
        SpawnWall(new Vector3Int(4000, 2700, 250), "WallA", new Vector3(2f, 1.35f, 0f));

        Assert.AreEqual(0, FoundationHostLink.ApplyAll(PartRegistry.All),
            "без единого FoundationElement перестраивать нечего — функция обязана выйти "
            + "до того, как соберёт осевые несущих стен по всей сцене");
    }

    [Test]
    public void ApplyAll_NothingChangedSinceTheLastBuild_RebuildsNothing()
    {
        SpawnWall(new Vector3Int(4000, 2700, 250), "WallA", new Vector3(2f, 1.35f, 0f));
        SpawnWall(new Vector3Int(250, 2700, 3000), "WallB", new Vector3(4f, 1.35f, 1.5f));
        SpawnFoundation();

        int rebuilt = FoundationHostLink.ApplyAll(PartRegistry.All);

        Assert.AreEqual(0, rebuilt,
            "фундамент уже построен по ровно этим двум стенам на спавне — повторный вызов "
            + "без единого изменения в сцене обязан не трогать меш заново");
    }

    [Test]
    public void ApplyAll_WallAddedAfterTheStripWasBuilt_RebuildsTheStrip()
    {
        SpawnWall(new Vector3Int(4000, 2700, 250), "WallA", new Vector3(2f, 1.35f, 0f));
        SpawnWall(new Vector3Int(250, 2700, 3000), "WallB", new Vector3(4f, 1.35f, 1.5f));
        var foundation = SpawnFoundation();

        Assert.AreEqual(2, foundation.LastBuiltCentrelines.Count,
            "посылка: на спавне фундамент уже видел ровно две стены");

        SpawnWall(new Vector3Int(250, 2700, 2000), "WallAddedLater", new Vector3(4f, 1.35f, -1f));

        int rebuilt = FoundationHostLink.ApplyAll(PartRegistry.All);

        Assert.AreEqual(1, rebuilt,
            "третья несущая стена появилась ПОСЛЕ постройки ленты — ApplyAll обязан заметить "
            + "разницу в осевых и перестроить именно этот фундамент");
        Assert.AreEqual(3, foundation.LastBuiltCentrelines.Count,
            "после перестройки закэшированные осевые обязаны включать все три стены");
    }

    [Test]
    public void ApplyAll_WallDeletedAfterTheStripWasBuilt_RebuildsTheStrip()
    {
        SpawnWall(new Vector3Int(4000, 2700, 250), "WallA", new Vector3(2f, 1.35f, 0f));
        var wallB = SpawnWall(new Vector3Int(250, 2700, 3000), "WallB", new Vector3(4f, 1.35f, 1.5f));
        var foundation = SpawnFoundation();
        Assert.AreEqual(2, foundation.LastBuiltCentrelines.Count);

        Object.DestroyImmediate(wallB);
        _spawned.Remove(wallB);

        int rebuilt = FoundationHostLink.ApplyAll(PartRegistry.All);

        Assert.AreEqual(1, rebuilt,
            "удалённая несущая стена обязана заставить ApplyAll перестроить ленту без неё");
        Assert.AreEqual(1, foundation.LastBuiltCentrelines.Count);
    }

    [Test]
    public void ApplyAll_WallLoadBearingToggledOff_RebuildsTheStripWithoutThatWall()
    {
        SpawnWall(new Vector3Int(4000, 2700, 250), "WallA", new Vector3(2f, 1.35f, 0f));
        var wallBGo = SpawnWall(new Vector3Int(250, 2700, 3000), "WallB", new Vector3(4f, 1.35f, 1.5f));
        var foundation = SpawnFoundation();
        Assert.AreEqual(2, foundation.LastBuiltCentrelines.Count);

        wallBGo.GetComponent<Wall>().LoadBearing = false;

        int rebuilt = FoundationHostLink.ApplyAll(PartRegistry.All);

        Assert.AreEqual(1, rebuilt,
            "снятая несущая способность выводит стену из списка осевых — фундамент обязан "
            + "перестроиться без неё, а не ждать ручной правки ширины/глубины");
        Assert.AreEqual(1, foundation.LastBuiltCentrelines.Count);
    }
}
