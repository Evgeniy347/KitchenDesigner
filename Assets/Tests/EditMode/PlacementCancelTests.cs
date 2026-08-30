using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Отмена размещения только что созданного объекта.
///
/// Здесь живут причины, которые раньше были комментариями в
/// PlacementController.cs: окно и дверь врезаны в стену СПИСКОМ, мойка и
/// варочная — в деталь, а деактивация объекта (в отличие от уничтожения) OnDestroy
/// не зовёт. Не снять врезку вручную — значит оставить дыру от объекта, которого
/// в проекте так и не появилось.</summary>
public class PlacementCancelTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private PlacementController _placement = null!;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        var host = new GameObject("Placement");
        _spawned.Add(host);
        _placement = host.AddComponent<PlacementController>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    private T Adopt<T>(GameObject go) where T : Component
    {
        _spawned.Add(go);
        return go.GetComponent<T>();
    }

    [Test]
    public void CancelledWindow_LeavesNoHoleInTheWall()
    {
        var wallGo = ElementFactory.CreateWall(new Vector3Int(3000, 2700, 100), "Стена", Vector3.zero);
        _spawned.Add(wallGo);
        var wall = wallGo.GetComponent<Wall>();

        var windowGo = ElementFactory.CreateWindow(new Vector3Int(800, 1200, 100), "Окно",
            new Vector3(0f, 1.2f, 0f));
        var window = Adopt<WindowElement>(windowGo);
        window.AttachToWall(wall);
        Assert.IsTrue(wall.HasWindow(window), "окно врезалось в стену");
        _placement.Begin(window);

        _placement.Cancel();

        Assert.IsFalse(wall.HasWindow(window),
            "деактивация объекта OnDestroy не зовёт, поэтому врезку снимают руками: "
            + "иначе в стене осталась бы дыра от неустановленного окна");
    }

    [Test]
    public void CancelledSink_LeavesNoHoleInTheCountertop()
    {
        var topGo = ElementFactory.CreatePart(new Vector3Int(1200, 40, 600), "Столешница", Vector3.zero);
        _spawned.Add(topGo);
        var top = topGo.GetComponent<KitchenElement>();

        var sinkGo = ElementFactory.CreateSink("Мойка", new Vector3(0f, 0.02f, 0f));
        var sink = Adopt<SinkElement>(sinkGo);
        sink.SnapToPart();
        Assert.AreEqual(1, top.AttachedCutouts.Count, "мойка врезалась в столешницу");
        _placement.Begin(sink);

        _placement.Cancel();

        Assert.AreEqual(0, top.AttachedCutouts.Count,
            "в столешнице не должно остаться дыры от мойки, которую так и не поставили");
    }

    [Test]
    public void CancelledObject_LeavesTheSceneRegistry()
    {
        var floorGo = ElementFactory.CreateFloor(new Vector3Int(3000, 100, 3000), "Пол", Vector3.zero);
        var floor = Adopt<FloorElement>(floorGo);
        _placement.Begin(floor);

        _placement.Cancel();

        Assert.IsFalse(floorGo.activeSelf, "объект убран со сцены");
        CollectionAssert.DoesNotContain(PartRegistry.GetAll(), floor,
            "и снят с учёта: незакоммиченный объект в стек отмены не попадал, "
            + "возвращать его некуда");
    }
}
