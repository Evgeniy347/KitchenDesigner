using System.Collections.Generic;
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
        hl.CreateMaterials();
        Assert.IsTrue(hl.MaterialsReady,
            "подсветка строит свои пять материалов сама — CreateMaterials молча выходит, "
            + "если шейдер URP не найден, и тогда весь тест ниже проверял бы пустоту");
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

    // ── Временное снятие подсветки (предпросмотр декора) ───────────

    /// <summary>Жёлтая подсветка выделения перекрашивает деталь, и оценить под
    /// ней показанную наведением текстуру нельзя. На время показа подсветка
    /// снимается — но выделение остаётся.</summary>
    [Test]
    public void SuppressHighlight_RestoresOwnMaterial_ButKeepsSelection()
    {
        LogAssert.ignoreFailingMessages = true;
        try
        {
            var el = MakeWithRenderer("Деталь_тест");
            var own = new Color(0.2f, 0.4f, 0.6f, 1f);
            el.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_BaseColor", own);

            var smGo = new GameObject("SelectionManager");
            var sm = smGo.AddComponent<SelectionManager>();
            _spawned.Add(smGo);

            sm.Select(el);
            var highlighted = el.GetComponent<MeshRenderer>().material.GetColor("_BaseColor");
            Assert.AreNotEqual(own, highlighted, "выделение красит деталь жёлтым");

            sm.SuppressHighlight(el);

            Assert.AreEqual(own, el.GetComponent<MeshRenderer>().material.GetColor("_BaseColor"),
                "BUG: подсветка не снята — текстуру под ней не видно");
            Assert.IsTrue(sm.IsHighlightSuppressed(el));
            Assert.IsTrue(sm.IsSelected(el), "само выделение снимать нельзя");
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
    }

    /// <summary>У стены и пола ApplyForElement по дороге зовёт RefreshHighlight
    /// (ветка ownDecorOnly). Пока подсветка снята, она возвращаться не должна —
    /// иначе предпросмотр текстуры на стене был бы жёлтым.</summary>
    [Test]
    public void RefreshHighlight_DoesNothing_WhileSuppressed()
    {
        LogAssert.ignoreFailingMessages = true;
        try
        {
            var wall = MakeWithRenderer("Стена_тест", wall: true);
            var own = new Color(0.2f, 0.4f, 0.6f, 1f);
            wall.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_BaseColor", own);

            var smGo = new GameObject("SelectionManager");
            var sm = smGo.AddComponent<SelectionManager>();
            _spawned.Add(smGo);

            sm.Select(wall);
            sm.SuppressHighlight(wall);
            sm.RefreshHighlight(wall);

            Assert.AreEqual(own, wall.GetComponent<MeshRenderer>().material.GetColor("_BaseColor"),
                "BUG: подсветка вернулась сама, поверх показанной текстуры");
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
    }

    [Test]
    public void ResumeHighlight_PutsHighlightBack()
    {
        LogAssert.ignoreFailingMessages = true;
        try
        {
            var el = MakeWithRenderer("Деталь_тест");
            var own = new Color(0.2f, 0.4f, 0.6f, 1f);
            el.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_BaseColor", own);

            var smGo = new GameObject("SelectionManager");
            var sm = smGo.AddComponent<SelectionManager>();
            _spawned.Add(smGo);

            sm.Select(el);
            sm.SuppressHighlight(el);
            sm.ResumeHighlight(el);

            Assert.IsFalse(sm.IsHighlightSuppressed(el));
            Assert.AreNotEqual(own, el.GetComponent<MeshRenderer>().material.GetColor("_BaseColor"),
                "BUG: после предпросмотра выделение не вернулось");
            Assert.IsTrue(sm.HasSavedMaterialFor(el));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
    }

    /// <summary>Элемент сняли с выделения, пока подсветка была снята: «вернуть»
    /// потом нечего, и флаг не должен пережить выделение.</summary>
    [Test]
    public void Deselect_ClearsSuppression()
    {
        LogAssert.ignoreFailingMessages = true;
        try
        {
            var el = MakeWithRenderer("Деталь_тест");
            var smGo = new GameObject("SelectionManager");
            var sm = smGo.AddComponent<SelectionManager>();
            _spawned.Add(smGo);

            sm.Select(el);
            sm.SuppressHighlight(el);
            sm.DeselectAll();

            Assert.IsFalse(sm.IsHighlightSuppressed(el),
                "BUG: снятая подсветка пережила выделение — следующее выделение будет без неё");

            sm.Select(el);
            Assert.IsTrue(sm.HasSavedMaterialFor(el), "новое выделение снова подсвечено");
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
    }

    // ── RaycastTransparentAware: клик сквозь прозрачные (интеграция с Physics) ──

    private Camera SetupTestCamera()
    {
        var camGo = new GameObject("TestCamera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 0, -10);
        cam.transform.rotation = Quaternion.identity;
        _spawned.Add(camGo);
        return cam;
    }

    private KitchenElement MakeWithCollider(string name, Vector3 pos,
        Vector3Int size, bool transparent = false)
    {
        var e = Make(name);
        e.DimensionsMM = size;
        e.transform.position = pos;
        if (transparent) { var d = e.Data; d.Transparent = true; }
        var bc = e.gameObject.AddComponent<BoxCollider>();
        bc.size = Vector3.one;
        bc.center = Vector3.zero;
        return e;
    }

    [Test]
    public void RaycastTransparentAware_NoShift_ReturnsFirstHit()
    {
        var cam = SetupTestCamera();
        var a = MakeWithCollider("A", new Vector3(0, 0, 0),
            new Vector3Int(800, 400, 18));
        var b = MakeWithCollider("B", new Vector3(0, 0, 2),
            new Vector3Int(800, 400, 18));
        Physics.SyncTransforms();

        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var result = SelectionManager.RaycastTransparentAware(ray, shiftHeld: false);

        Assert.AreEqual(a, result, "без Shift — первый попавшийся");
    }

    [Test]
    public void RaycastTransparentAware_Shift_SkipsTransparent()
    {
        var cam = SetupTestCamera();
        var transparent = MakeWithCollider("T", new Vector3(0, 0, 0),
            new Vector3Int(800, 400, 18), transparent: true);
        var opaque = MakeWithCollider("O", new Vector3(0, 0, 2),
            new Vector3Int(800, 400, 18));
        Physics.SyncTransforms();

        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var result = SelectionManager.RaycastTransparentAware(ray, shiftHeld: true);

        Assert.AreEqual(opaque, result, "Shift пропускает прозрачный, берёт непрозрачный за ним");
    }

    [Test]
    public void RaycastTransparentAware_Shift_AllTransparent_ReturnsNull()
    {
        var cam = SetupTestCamera();
        var t1 = MakeWithCollider("T1", new Vector3(0, 0, 0),
            new Vector3Int(800, 400, 18), transparent: true);
        var t2 = MakeWithCollider("T2", new Vector3(0, 0, 2),
            new Vector3Int(800, 400, 18), transparent: true);
        Physics.SyncTransforms();

        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var result = SelectionManager.RaycastTransparentAware(ray, shiftHeld: true);

        Assert.IsNull(result, "все прозрачные — null");
    }
}
