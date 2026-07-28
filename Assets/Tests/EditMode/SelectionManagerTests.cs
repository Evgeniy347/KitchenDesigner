using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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

    // ── Клик по детали внутри мультивыделения ─────────────────────

    [Test]
    public void ClickInsideMultiSelection_WithoutDrag_LeavesOnlyClickedElement()
    {
        EditModeManager.Reset();

        var a = Make("Доска A");
        var b = Make("Доска B");

        var smGo = new GameObject("SelectionManager");
        var sm = smGo.AddComponent<SelectionManager>();
        _spawned.Add(smGo);

        sm.SelectOnly(new List<KitchenElement> { a, b });
        Assert.AreEqual(2, sm.SelectedElements.Count, "выделены две детали");

        // Нажатие: выборка ещё цела — иначе пропал бы групповой drag.
        sm.HandleClickOnElement(b, ctrlHeld: false);
        Assert.AreEqual(2, sm.SelectedElements.Count, "на нажатии выделение не сжимается");
        Assert.AreEqual(b, sm.Selected);

        // Отпускание без перетаскивания — остаётся только та деталь, по которой кликнули.
        sm.HandleClickRelease();

        Assert.AreEqual(1, sm.SelectedElements.Count,
            "ЛКМ без Ctrl по детали мультивыделения оставляет выделенной только её");
        Assert.AreEqual(b, sm.Selected);
        Assert.IsFalse(sm.IsSelected(a), "вторая деталь снята с выделения");
    }

    // ── PickFromOrderedHits: клик сквозь прозрачные элементы по Shift ──

    private KitchenElement MakeTransparent(string name)
    {
        var e = Make(name);
        var d = e.Data;
        d.Transparent = true;
        return e;
    }

    [Test]
    public void Pick_NoShift_ReturnsFirst()
    {
        var a = Make("A");
        var hits = new List<KitchenElement> { a };
        Assert.AreEqual(a, SelectionManager.PickFromOrderedHits(hits, shiftHeld: false));
    }

    [Test]
    public void Pick_NoShift_TransparentInFront_ReturnsTransparent()
    {
        var transparent = MakeTransparent("Прозрачный");
        var opaque = Make("Непрозрачный");
        var hits = new List<KitchenElement> { transparent, opaque };
        Assert.AreEqual(transparent, SelectionManager.PickFromOrderedHits(hits, shiftHeld: false),
            "без Shift прозрачный элемент спереди не пропускается");
    }

    [Test]
    public void Pick_Shift_TransparentInFront_SkipsToOpaque()
    {
        var transparent = MakeTransparent("Прозрачный");
        var opaque = Make("Непрозрачный");
        var hits = new List<KitchenElement> { transparent, opaque };
        Assert.AreEqual(opaque, SelectionManager.PickFromOrderedHits(hits, shiftHeld: true),
            "с Shift прозрачный элемент пропускается, выбирается непрозрачный за ним");
    }

    [Test]
    public void Pick_Shift_AllTransparent_ReturnsNull()
    {
        var t1 = MakeTransparent("Прозрачный 1");
        var t2 = MakeTransparent("Прозрачный 2");
        var hits = new List<KitchenElement> { t1, t2 };
        Assert.IsNull(SelectionManager.PickFromOrderedHits(hits, shiftHeld: true),
            "все элементы прозрачные — выбирать нечего");
    }

    [Test]
    public void Pick_Shift_AllOpaque_ReturnsFirst()
    {
        var a = Make("A");
        var b = Make("B");
        var hits = new List<KitchenElement> { a, b };
        Assert.AreEqual(a, SelectionManager.PickFromOrderedHits(hits, shiftHeld: true),
            "Shift без прозрачных элементов — первый по порядку");
    }

    [Test]
    public void Pick_Shift_TwoTransparentThenOpaque()
    {
        var t1 = MakeTransparent("Прозрачный 1");
        var t2 = MakeTransparent("Прозрачный 2");
        var opaque = Make("Непрозрачный");
        var hits = new List<KitchenElement> { t1, t2, opaque };
        Assert.AreEqual(opaque, SelectionManager.PickFromOrderedHits(hits, shiftHeld: true),
            "два прозрачных подряд — пропускаются оба");
    }

    [Test]
    public void Pick_EmptyList_ReturnsNull()
    {
        var hits = new List<KitchenElement>();
        Assert.IsNull(SelectionManager.PickFromOrderedHits(hits, shiftHeld: false));
        Assert.IsNull(SelectionManager.PickFromOrderedHits(hits, shiftHeld: true));
    }

    [Test]
    public void Pick_Shift_NullElementsSkipped()
    {
        var opaque = Make("Непрозрачный");
        var hits = new List<KitchenElement> { null!, opaque };
        Assert.AreEqual(opaque, SelectionManager.PickFromOrderedHits(hits, shiftHeld: true),
            "null элементы пропускаются");
    }

    [Test]
    public void ClickInsideMultiSelection_WithCtrl_TogglesInsteadOfCollapsing()
    {
        EditModeManager.Reset();

        var a = Make("Доска A");
        var b = Make("Доска B");
        var c = Make("Доска C");

        var smGo = new GameObject("SelectionManager");
        var sm = smGo.AddComponent<SelectionManager>();
        _spawned.Add(smGo);

        sm.SelectOnly(new List<KitchenElement> { a, b, c });
        sm.HandleClickOnElement(c, ctrlHeld: true);
        sm.HandleClickRelease();

        Assert.AreEqual(2, sm.SelectedElements.Count,
            "Ctrl+клик снимает одну деталь и не сжимает выделение до одной");
        Assert.IsTrue(sm.IsSelected(a));
        Assert.IsTrue(sm.IsSelected(b));
    }

    // ── DeselectAll не оставляет orphan-записей в _savedMaterials ──

    private KitchenElement MakeWithRenderer(string name, bool wall = false)
    {
        var e = Make(name, wall);
        var go = e.gameObject;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = new Mesh();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        return e;
    }

    private static void InitHighlighterMaterials(ElementHighlighter hl)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        const BindingFlags f = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(ElementHighlighter).GetField("_validMaterial", f)!.SetValue(hl, new Material(shader));
        typeof(ElementHighlighter).GetField("_invalidMaterial", f)!.SetValue(hl, new Material(shader));
        typeof(ElementHighlighter).GetField("_dimmedMaterial", f)!.SetValue(hl, new Material(shader));
        typeof(ElementHighlighter).GetField("_validTransparentMaterial", f)!.SetValue(hl, new Material(shader));
        typeof(ElementHighlighter).GetField("_invalidTransparentMaterial", f)!.SetValue(hl, new Material(shader));
        typeof(ElementHighlighter).GetField("_materialsInitialized", f)!.SetValue(hl, true);
    }

    /// <summary>
    /// Баг: DeselectAll шёл по _selectedElements и дёргал RestoreMaterial,
    /// а тот триггерил ElementHighlighter.ApplyForElement → ApplyMaterial
    /// (ветка ownDecorOnly для стен) → RefreshHighlight → HighlightSelected
    /// — и заново сохранял материал в _savedMaterials, потому что список
    /// _selectedElements ещё не был очищен. После очистки орфан оставался
    /// навсегда.
    /// </summary>
    [Test]
    public void DeselectAll_LeavesNoOrphanSavedMaterials()
    {
        LogAssert.ignoreFailingMessages = true;
        try
        {
            var hlGo = new GameObject("ElementHighlighter");
            var hl = hlGo.AddComponent<ElementHighlighter>();
            InitHighlighterMaterials(hl);
            _spawned.Add(hlGo);

            var wallA = MakeWithRenderer("Стена_тест_A", wall: true);
            var wallB = MakeWithRenderer("Стена_тест_B", wall: true);

            var smGo = new GameObject("SelectionManager");
            var sm = smGo.AddComponent<SelectionManager>();
            _spawned.Add(smGo);

            sm.Select(wallA);
            Assert.AreEqual(1, sm.SelectedElements.Count);
            Assert.IsTrue(sm.HasSavedMaterialFor(wallA));

            sm.Select(wallB);

            Assert.AreEqual(1, sm.SelectedElements.Count, "только одна стена выделена");
            Assert.IsTrue(sm.IsSelected(wallB));
            Assert.IsFalse(sm.IsSelected(wallA));
            Assert.IsFalse(sm.HasSavedMaterialFor(wallA),
                "после DeselectAll у стены A НЕ ДОЛЖНО быть orphan-записи в _savedMaterials");
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
    }

    [Test]
    public void DeselectAll_CleansUpAllSavedMaterials()
    {
        LogAssert.ignoreFailingMessages = true;
        try
        {
            var hlGo = new GameObject("ElementHighlighter");
            var hl = hlGo.AddComponent<ElementHighlighter>();
            InitHighlighterMaterials(hl);
            _spawned.Add(hlGo);

            var wallA = MakeWithRenderer("Стена_тест_A", wall: true);
            var wallB = MakeWithRenderer("Стена_тест_B", wall: true);

            var smGo = new GameObject("SelectionManager");
            var sm = smGo.AddComponent<SelectionManager>();
            _spawned.Add(smGo);

            sm.Select(wallA);
            sm.AddToSelection(wallB);
            Assert.AreEqual(2, sm.SelectedElements.Count);
            Assert.IsTrue(sm.HasSavedMaterialFor(wallA));
            Assert.IsTrue(sm.HasSavedMaterialFor(wallB));

            sm.DeselectAll();

            Assert.AreEqual(0, sm.SelectedElements.Count, "ничего не выделено");
            Assert.IsNull(sm.Selected);
            Assert.IsFalse(sm.HasSavedMaterialFor(wallA),
                "после DeselectAll у стены A нет orphan-материала");
            Assert.IsFalse(sm.HasSavedMaterialFor(wallB),
                "после DeselectAll у стены B нет orphan-материала");
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
    }
}
