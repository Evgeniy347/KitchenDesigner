using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Створка окна и полотно двери живут ВНУТРИ стены, а не рядом с ней.
/// Стена для них — не препятствие, а проём: она обнимает створку со всех
/// сторон, и створка выходит из неё, как ящик из корпуса.
///
/// Регресс: когда проверка раскрытия перестала выбрасывать соседа, которого
/// деталь касается закрытой, и стала гасить только его ТЕНЬ за деталью, от
/// стены остались активными куски слева, справа, сверху и снизу проёма. На
/// петле по кромке тыльное ребро створки уходит за линию петли на толщину
/// полотна — прямо в эти куски, — и створка замирала почти сразу. Тень
/// правильна для фасада, у которого сосед стоит СБОКУ; для детали, стоящей
/// внутри соседа, рама гасится целиком.
///
/// Старые тесты этого не ловили: они спавнили дверь и окно вообще без стены,
/// и в пустой сцене открывать их было нечему мешать.</summary>
public class WallSashOpeningReproTests
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

    private GameObject Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go;
    }

    private void MakeWall() =>
        Spawn(ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), "Wall",
            new Vector3(0f, 1.25f, -1.5f)));

    private DoorElement MakeDoorInWall()
    {
        var go = Spawn(ElementFactory.CreateDoor(new Vector3Int(900, 2050, 100), "Door",
            new Vector3(0f, 1.025f, -1.5f)));
        var door = go.GetComponent<DoorElement>();
        door.SnapToWall();
        return door;
    }

    private WindowElement MakeWindowInWall()
    {
        var go = Spawn(ElementFactory.CreateWindow(new Vector3Int(900, 1000, 100), "Win",
            new Vector3(0f, 1.6f, -1.5f)));
        var window = go.GetComponent<WindowElement>();
        window.SnapToWall();
        return window;
    }

    [Test]
    public void Door_InItsOwnWall_OpensFully()
    {
        MakeWall();
        var door = MakeDoorInWall();

        door.SetOpen(true);
        door.StepDoor(1f);

        Assert.AreEqual(1f, door.DoorProgress, 1e-4f,
            "стена, в проёме которой стоит дверь, — её рама, а не препятствие: "
            + "полотно обязано раскрыться полностью");
    }

    [Test]
    public void Window_InItsOwnWall_OpensFully()
    {
        MakeWall();
        var window = MakeWindowInWall();

        window.SetOpen(true);
        window.StepDoor(1f);

        Assert.AreEqual(1f, window.DoorProgress, 1e-4f,
            "то же для оконной створки: рама держит её со всех сторон, и на "
            + "петле по кромке тыльное ребро заходит за линию петли — это ход, "
            + "а не столкновение");
    }

    [Test]
    public void Door_InItsWall_StillStopsAtARealObstacle()
    {
        MakeWall();
        var door = MakeDoorInWall();
        Spawn(ElementFactory.CreatePart(new Vector3Int(900, 2000, 18), "Obstacle",
            new Vector3(0f, 1.025f, -1.35f)));

        door.SetOpen(true);
        door.StepDoor(1f);

        Assert.Less(door.DoorProgress, 1f,
            "контроль: стена гасится потому, что дверь стоит ВНУТРИ неё, а не "
            + "потому, что для дверей проверка отключена — шкаф перед проёмом "
            + "по-прежнему останавливает полотно");
        Assert.Greater(door.DoorProgress, 0f, "но приоткрыться она успевает");
    }

    [Test]
    public void Window_InItsWall_StillStopsAtARealObstacle()
    {
        MakeWall();
        var window = MakeWindowInWall();
        Spawn(ElementFactory.CreatePart(new Vector3Int(900, 900, 18), "Obstacle",
            new Vector3(0f, 1.6f, -1.35f)));

        window.SetOpen(true);
        window.StepDoor(1f);

        Assert.Less(window.DoorProgress, 1f,
            "второй контроль, для окна: препятствие перед проёмом останавливает "
            + "створку, иначе «рама не мешает» было бы неотличимо от «не "
            + "проверяется ничего»");
    }
}
