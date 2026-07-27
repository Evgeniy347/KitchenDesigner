using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class SelectionManagerTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(string name, bool wall = false)
    {
        var go = new GameObject(name);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = new Vector3Int(800, 400, 18);
        if (wall) go.AddComponent<Wall>();
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        EditModeManager.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            PartRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
        PartRegistry.Clear();
    }

    // ── EditModeManager: классификация и доступность ──────────────

    [Test]
    public void Categorize_Wall_IsRoom()
    {
        var wall = Make("Стена", wall: true);
        Assert.AreEqual(EditModeManager.Category.Room,
            EditModeManager.Categorize(wall),
            "стена — конструкция помещения");
    }

    [Test]
    public void NormalMode_Wall_IsNotInteractable()
    {
        EditModeManager.Reset(); // ← Normal
        var wall = Make("Стена", wall: true);
        Assert.AreEqual(EditModeManager.Category.Room, EditModeManager.Categorize(wall));
        Assert.IsFalse(EditModeManager.IsInteractable(wall),
            "стена в обычном режиме НЕ доступна для клика мышью");
    }

    [Test]
    public void RoomMode_Wall_IsInteractable()
    {
        EditModeManager.SetMode(EditMode.Room);
        var wall = Make("Стена", wall: true);
        Assert.AreEqual(EditModeManager.Category.Room, EditModeManager.Categorize(wall));
        Assert.IsTrue(EditModeManager.IsInteractable(wall),
            "стена в режиме помещения доступна");
    }

    [Test]
    public void NormalMode_RegularBoard_IsInteractable()
    {
        EditModeManager.Reset();
        var board = Make("Доска", wall: false);
        Assert.AreEqual(EditModeManager.Category.Regular, EditModeManager.Categorize(board));
        Assert.IsTrue(EditModeManager.IsInteractable(board));
    }

    [Test]
    public void RoomMode_RegularBoard_IsNotInteractable()
    {
        EditModeManager.SetMode(EditMode.Room);
        var board = Make("Доска", wall: false);
        Assert.AreEqual(EditModeManager.Category.Regular, EditModeManager.Categorize(board));
        Assert.IsFalse(EditModeManager.IsInteractable(board));
    }

    [Test]
    public void NormalMode_Floor_IsNotInteractable()
    {
        EditModeManager.Reset();
        var go = new GameObject("Пол");
        var e = go.AddComponent<FloorElement>();
        e.PartName = "Пол";
        e.DimensionsMM = new Vector3Int(3000, 18, 3000);
        PartRegistry.Register(e);
        _spawned.Add(go);

        Assert.AreEqual(EditModeManager.Category.Room, EditModeManager.Categorize(e));
        Assert.IsFalse(EditModeManager.IsInteractable(e));
    }

    // ── SelectionManager: клик по недоступному элементу ──────────

    [Test]
    public void NormalMode_ClickWall_ShouldDeselect()
    {
        EditModeManager.Reset();

        var wall = Make("Стена", wall: true);
        var board = Make("Доска", wall: false);

        var smGo = new GameObject("SelectionManager");
        var sm = smGo.AddComponent<SelectionManager>();
        _spawned.Add(smGo);

        sm.Select(board);
        Assert.AreEqual(board, sm.Selected, "доска выделена");

        // Клик по стене в обычном режиме.
        // БАГ до исправления: IsInteractable(стена) = false → ранний return без DeselectAll().
        sm.HandleClickOnElement(wall, ctrlHeld: false);

        Assert.IsNull(sm.Selected,
            "клик по стене в обычном режиме должен снимать выделение");
    }

    [Test]
    public void RoomMode_ClickRegularBoard_ShouldDeselect()
    {
        EditModeManager.SetMode(EditMode.Room);

        var wall = Make("Стена", wall: true);
        var board = Make("Доска", wall: false);

        var smGo = new GameObject("SelectionManager");
        var sm = smGo.AddComponent<SelectionManager>();
        _spawned.Add(smGo);

        sm.Select(wall);
        Assert.AreEqual(wall, sm.Selected, "стена выделена");

        sm.HandleClickOnElement(board, ctrlHeld: false);

        Assert.IsNull(sm.Selected,
            "клик по доске в режиме помещения должен снимать выделение");
    }

    [Test]
    public void NormalMode_ClickInteractable_SelectsElement()
    {
        EditModeManager.Reset();

        var board = Make("Доска", wall: false);

        var smGo = new GameObject("SelectionManager");
        var sm = smGo.AddComponent<SelectionManager>();
        _spawned.Add(smGo);

        sm.HandleClickOnElement(board, ctrlHeld: false);

        Assert.AreEqual(board, sm.Selected,
            "клик по обычной доске выделяет её");
    }

    [Test]
    public void NormalMode_ClickFloor_Deselects()
    {
        EditModeManager.Reset();

        var board = Make("Доска", wall: false);

        var go = new GameObject("Пол");
        var floor = go.AddComponent<FloorElement>();
        floor.PartName = "Пол";
        floor.DimensionsMM = new Vector3Int(3000, 18, 3000);
        PartRegistry.Register(floor);
        _spawned.Add(go);

        var smGo = new GameObject("SelectionManager");
        var sm = smGo.AddComponent<SelectionManager>();
        _spawned.Add(smGo);

        sm.Select(board);
        Assert.AreEqual(board, sm.Selected);

        sm.HandleClickOnElement(floor, ctrlHeld: false);

        Assert.IsNull(sm.Selected,
            "клик по полу снимает выделение");
    }
}
