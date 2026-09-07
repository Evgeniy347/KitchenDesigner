using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Выравнивание кромки детали по стенкам паза — разметочный детент
/// «поставь полку по краю паза». Работает для ЛЮБОЙ детали, не только для ДВП.
///
/// Числа взяты из реальной сцены: стойка A34K1_upper_side_L (332×900×18,
/// поворот Y 180°, правая кромка X=1.585) и дощечка A34K1_upper_bottom
/// (254×1372×18, полуширина 127 мм), которую двигают по X от 1.380.</summary>
public class GrooveEdgeSnapTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private const float SideCenterX = 1.419f;
    private const float SideRightEdgeX = 1.585f;
    private const float ShelfHalfWidthM = 0.127f;

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = dims;
        el.transform.position = pos;
        el.transform.rotation = rot;
        return el;
    }

    private KitchenElement MakeSide()
    {
        // Поворот Y 180° переводит ЛОКАЛЬНУЮ левую кромку в МИРОВУЮ правую (X=1.585),
        // поэтому паз в сцене задаётся как "left".
        var side = Make("A34K1_upper_side_L", new Vector3Int(332, 900, 18),
            new Vector3(SideCenterX, 1.81f, -1.871f), ManagedRotation.Euler(0f, 180f, 0f));
        side.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Left));
        return side;
    }

    private KitchenElement MakeShelf(float x)
        => Make("A34K1_upper_bottom", new Vector3Int(254, 1372, 18),
            new Vector3(x, 1.81f, -2.566f), ManagedRotation.Euler(90f, 0f, 0f));

    [SetUp]
    public void SetUp() => KitchenSettings.Instance.SnapEnabled = true;

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    [Test]
    public void GrooveWalls_SitAt16And20mmFromEdge()
    {
        var side = MakeSide();
        var walls = side.GetGrooveWallFaces();

        Assert.AreEqual(2, walls.Length, "две стенки паза");

        var xs = new List<float>();
        foreach (var w in walls)
            if (!xs.Exists(v => Mathf.Abs(v - w.center.x) < 1e-5f)) xs.Add(w.center.x);
        xs.Sort();

        Assert.AreEqual(2, xs.Count, "две плоскости стенок");
        Assert.AreEqual(SideRightEdgeX - 0.020f, xs[0], 1e-4f, "дальняя стенка: 16+4=20 мм от кромки");
        Assert.AreEqual(SideRightEdgeX - 0.016f, xs[1], 1e-4f, "ближняя стенка: 16 мм от кромки");
    }

    [Test]
    public void Shelf_SnapsToNearGrooveWall_At1_442()
    {
        var side = MakeSide();
        var shelf = MakeShelf(1.380f);

        // Подводим почти вплотную к ближней стенке паза.
        var result = SnapSystem.TrySnap(shelf, new List<KitchenElement> { side },
            new Vector3(1.4405f, 1.81f, -2.566f));

        Assert.IsTrue(result.snapped, "должно поймать ближнюю стенку паза");
        Assert.AreEqual(1.442f, result.position.x, 1e-3f,
            "правое ребро дощечки встаёт на 16 мм от кромки стойки");
        Assert.AreEqual(SideRightEdgeX - 0.016f, result.position.x + ShelfHalfWidthM, 1e-3f);
    }

    [Test]
    public void Shelf_SnapsToFarGrooveWall_At1_438()
    {
        var side = MakeSide();
        var shelf = MakeShelf(1.380f);

        var result = SnapSystem.TrySnap(shelf, new List<KitchenElement> { side },
            new Vector3(1.4365f, 1.81f, -2.566f));

        Assert.IsTrue(result.snapped, "должно поймать дальнюю стенку паза");
        Assert.AreEqual(1.438f, result.position.x, 1e-3f,
            "правое ребро дощечки встаёт на 20 мм от кромки стойки");
    }

    [Test]
    public void Shelf_StillSnapsToBoardEdge_At1_458()
    {
        // Прежний детент по кромке стойки не должен пострадать.
        var side = MakeSide();
        var shelf = MakeShelf(1.380f);

        var result = SnapSystem.TrySnap(shelf, new List<KitchenElement> { side },
            new Vector3(1.4565f, 1.81f, -2.566f));

        Assert.IsTrue(result.snapped);
        Assert.AreEqual(1.458f, result.position.x, 1e-3f, "кромка дощечки заподлицо с кромкой стойки");
    }

    [Test]
    public void ThreeDetents_AreDistinctAndOrdered()
    {
        var side = MakeSide();
        var shelf = MakeShelf(1.380f);

        var detents = new List<float>();
        foreach (float probe in new[] { 1.4365f, 1.4405f, 1.4565f })
        {
            var r = SnapSystem.TrySnap(shelf, new List<KitchenElement> { side },
                new Vector3(probe, 1.81f, -2.566f));
            Assert.IsTrue(r.snapped, $"проба {probe} должна прилипнуть");
            detents.Add(Mathf.Round(r.position.x * 1000f) / 1000f);
        }

        CollectionAssert.AreEqual(new[] { 1.438f, 1.442f, 1.458f }, detents,
            "три детента: дальняя стенка, ближняя стенка, кромка");
    }

    [Test]
    public void BoardWithoutGrooves_HasNoWallFaces()
    {
        var plain = Make("plain", new Vector3Int(332, 900, 18), Vector3.zero, Quaternion.identity);
        Assert.AreEqual(0, plain.GetGrooveWallFaces().Length);
    }
}
