using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class SelectionManagerGizmoPickTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private GameObject Make(string name)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        return go;
    }

    private KitchenElement MakeBoard(string name, bool transparent = false)
    {
        var go = Make(name);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = new Vector3Int(800, 400, 18);
        e.Transparent = transparent;
        go.AddComponent<BoxCollider>();
        PartRegistry.Register(e);
        return e;
    }

    private Collider MakeResizeHandle(string name)
    {
        var go = Make(name);
        go.AddComponent<ResizeHandle>().faceIndex = 4;
        return go.AddComponent<BoxCollider>();
    }

    private Collider MakeOverlayHandle(string name)
    {
        var go = Make(name);
        go.AddComponent<TextureOverlayHandle>().edge = 0;
        return go.AddComponent<BoxCollider>();
    }

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var e = go.GetComponent<KitchenElement>();
            if (e != null) PartRegistry.Unregister(e);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
        PartRegistry.Clear();
    }

    [Test]
    public void HandleTipInFront_NoShift_ReturnsBoardBehind()
    {
        var board = MakeBoard("Доска");
        var handle = MakeResizeHandle("ResizeHandle_4");
        var hits = new List<Collider> { handle, board.GetComponent<Collider>() };
        Assert.AreEqual(board, SelectionManager.PickElementFromOrderedColliders(hits, shiftHeld: false),
            "выделенная деталь обставлена ручками ресайза; ПКМ по наконечнику обязан " +
            "открывать свойства детали за ним, а не молчать");
    }

    [Test]
    public void OverlayHandleInFront_NoShift_ReturnsElementBehind()
    {
        var board = MakeBoard("Доска");
        var overlay = MakeOverlayHandle("TextureOverlayHandle_2");
        var hits = new List<Collider> { overlay, board.GetComponent<Collider>() };
        Assert.AreEqual(board, SelectionManager.PickElementFromOrderedColliders(hits, shiftHeld: false),
            "маркеры области накладки — тот же гизмо-мусор на пути луча");
    }

    [Test]
    public void OnlyGizmoHits_ReturnsNull()
    {
        var handle = MakeResizeHandle("ResizeHandle_0");
        var overlay = MakeOverlayHandle("TextureOverlayHandle_0");
        Assert.IsNull(SelectionManager.PickElementFromOrderedColliders(
            new List<Collider> { handle, overlay }, shiftHeld: false),
            "за гизмо ничего нет — выбирать нечего");
    }

    [Test]
    public void NonElementBlockerInFront_NoShift_ReturnsNull()
    {
        var blocker = Make("Blocker");
        var blockerCol = blocker.AddComponent<BoxCollider>();
        var board = MakeBoard("Доска");
        Assert.IsNull(SelectionManager.PickElementFromOrderedColliders(
            new List<Collider> { blockerCol, board.GetComponent<Collider>() }, shiftHeld: false),
            "сквозь не-деталь луч не проходит: протыкаем ТОЛЬКО гизмо-маркеры");
    }

    [Test]
    public void Shift_TransparentBehindGizmo_SkipsToOpaque()
    {
        var glass = MakeBoard("Стекло", transparent: true);
        var opaque = MakeBoard("Доска");
        var handle = MakeResizeHandle("ResizeHandle_1");
        var hits = new List<Collider>
        {
            handle, glass.GetComponent<Collider>(), opaque.GetComponent<Collider>()
        };
        Assert.AreEqual(opaque, SelectionManager.PickElementFromOrderedColliders(hits, shiftHeld: true),
            "Shift-семантика сохранена: гизмо пропускаем, затем прозрачную деталь, берём непрозрачную");
    }

    [Test]
    public void NoShift_TransparentBehindGizmo_ReturnsTransparent()
    {
        var glass = MakeBoard("Стекло", transparent: true);
        var handle = MakeResizeHandle("ResizeHandle_3");
        var hits = new List<Collider> { handle, glass.GetComponent<Collider>() };
        Assert.AreEqual(glass, SelectionManager.PickElementFromOrderedColliders(hits, shiftHeld: false),
            "без Shift прозрачная деталь за гизмо — законная цель клика");
    }

    [Test]
    public void GizmoColliderOnChild_IsRecognized()
    {
        var handleGo = Make("ResizeHandle_5");
        handleGo.AddComponent<ResizeHandle>().faceIndex = 5;
        var child = Make("Tip");
        child.transform.SetParent(handleGo.transform, false);
        var col = child.AddComponent<BoxCollider>();
        Assert.IsTrue(SelectionManager.IsGizmoCollider(col),
            "маркер ищет родителя по иерархии: коллайдер на ребёнке ручки — тоже гизмо");
    }

    [Test]
    public void PlainBoardCollider_IsNotGizmo()
    {
        var board = MakeBoard("Доска");
        Assert.IsFalse(SelectionManager.IsGizmoCollider(board.GetComponent<Collider>()));
    }
}
