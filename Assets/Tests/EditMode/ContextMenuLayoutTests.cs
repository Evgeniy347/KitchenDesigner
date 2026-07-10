using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ContextMenuLayoutTests
{
    private Canvas _canvas;
    private ContextMenuUI _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu.Build(_canvas.transform);
    }

    [TearDown]
    public void Teardown()
    {
        if (_menu != null) Object.DestroyImmediate(_menu.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas.gameObject);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private FacadeElement MakeFacade(string name)
    {
        var go = new GameObject(name);
        var facade = go.AddComponent<FacadeElement>();
        facade.PartName = name;
        facade.DimensionsMM = new Vector3Int(400, 300, 18);
        _spawned.Add(go);
        return facade;
    }

    private KitchenElement MakeBoard(string name)
    {
        var go = new GameObject(name);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = new Vector3Int(800, 400, 18);
        _spawned.Add(go);
        return el;
    }

    [Test]
    public void Facade_GapSectionBottomAboveNextRow()
    {
        var facade = MakeFacade("F1");
        _menu.Open(facade);
        var panel = _canvas.transform.Find("ContextMenu");
        Assert.NotNull(panel);

        var gapSection = panel.Find("_GapSection");
        var xField = panel.Find("F_X, м");
        Assert.NotNull(gapSection, "gap section must exist for facade");
        Assert.NotNull(xField, "X position field must exist");

        var gapRt = gapSection.GetComponent<RectTransform>();
        var xRt = xField.GetComponent<RectTransform>();

        // Элементы заякорены к верху панели (pivot сверху): anchoredPosition.y —
        // верхняя кромка, низ = верх − высота.
        float gapBottom = gapRt.anchoredPosition.y - gapRt.sizeDelta.y;
        float xTop = xRt.anchoredPosition.y;

        Assert.GreaterOrEqual(gapBottom, xTop,
            "gap section bottom must be above the first position row");
    }

    [Test]
    public void Board_GapSectionInactive_PositionRowsShiftedUp()
    {
        var panel = _canvas.transform.Find("ContextMenu");
        Assert.NotNull(panel);
        var gapSection = panel.Find("_GapSection");
        Assert.NotNull(gapSection);

        var board = MakeBoard("B1");
        _menu.Open(board);

        Assert.IsFalse(gapSection.gameObject.activeSelf,
            "gap section must be hidden for regular board");
    }

    [Test]
    public void Facade_NoOverlap_BetweenGapAndNextRow()
    {
        var facade = MakeFacade("F1");
        _menu.Open(facade);
        var panel = _canvas.transform.Find("ContextMenu");

        var gapSection = panel.Find("_GapSection");
        var xInput = panel.Find("F_X, м");
        Assert.NotNull(gapSection);
        Assert.NotNull(xInput);

        var gapRt = gapSection.GetComponent<RectTransform>();
        var xRt = xInput.GetComponent<RectTransform>();

        float gapSectionBottom = gapRt.anchoredPosition.y - gapRt.sizeDelta.y;
        float xTop = xRt.anchoredPosition.y;

        Assert.GreaterOrEqual(gapSectionBottom, xTop,
            "gap section bottom must be above the first position row");

        foreach (var childName in new[] { "Gap_Ширина X, мм", "F_gap_Ширина X, мм",
                                          "Gap_Высота Y, мм", "F_gap_Высота Y, мм" })
        {
            var child = gapSection.Find(childName);
            Assert.NotNull(child, $"missing {childName}");
            var childRt = child.GetComponent<RectTransform>();
            // Дети секции тоже с верхним pivot и заякорены к верху секции:
            // низ в координатах панели = верх_секции + верх_ребёнка − высота.
            float childBottomPanel = gapRt.anchoredPosition.y + childRt.anchoredPosition.y
                                     - childRt.sizeDelta.y;
            Assert.GreaterOrEqual(childBottomPanel, xTop,
                $"{childName} must not overlap the X position row");
        }
    }

    [Test]
    public void GapSection_AddNewRow_AdjustsHeightAutomatically()
    {
        var panel = _canvas.transform.Find("ContextMenu");
        var gapSection = panel.Find("_GapSection");
        var gapRt = gapSection.GetComponent<RectTransform>();
        float h = gapRt.sizeDelta.y;

        Assert.Greater(h, 0);
        Assert.Less(h, 200, "section should not be unreasonably tall");
    }

    // Bug B: в режиме «деталь» подпись строки должна ехать вместе с полем.
    // Раньше сдвигалось только поле — подписи оставались на месте, поля
    // и блок «Повернуть на 90°» наезжали на них.
    [Test]
    public void Board_RowLabelAndFieldStayAligned()
    {
        var board = MakeBoard("B1");
        _menu.Open(board);
        var panel = _canvas.transform.Find("ContextMenu");

        foreach (var row in new[] { "X, м", "Y, м", "Z, м",
                                    "Поворот X°", "Поворот Y°", "Поворот Z°" })
        {
            var lbl = panel.Find("L_" + row).GetComponent<RectTransform>();
            var fld = panel.Find("F_" + row).GetComponent<RectTransform>();
            Assert.AreEqual(lbl.anchoredPosition.y, fld.anchoredPosition.y, 0.5f,
                $"label and field of «{row}» must be on the same row in board mode");
        }
    }

    // Bug B: блок «Повернуть на 90°» не должен перекрывать строку «Поворот Z°».
    [Test]
    public void Board_RotationLabelBelowRotationRows_NoOverlap()
    {
        var board = MakeBoard("B1");
        _menu.Open(board);
        var panel = _canvas.transform.Find("ContextMenu");

        var rotLbl = panel.Find("CtxRotLbl").GetComponent<RectTransform>();
        var rzLbl = panel.Find("L_Поворот Z°").GetComponent<RectTransform>();

        float rotLblTop = rotLbl.anchoredPosition.y;
        float rzBottom = rzLbl.anchoredPosition.y - rzLbl.sizeDelta.y;

        Assert.LessOrEqual(rotLblTop, rzBottom,
            "«Повернуть на 90°» must sit below the «Поворот Z°» row without overlap");
    }

    // Заголовок должен быть ВНУТРИ панели (верхняя кромка ниже верха панели),
    // а не выезжать над окном в режиме «деталь» — прямой репорт пользователя.
    [Test]
    public void Board_TitleStaysInsidePanel()
    {
        var board = MakeBoard("B1");
        _menu.Open(board);
        var panel = _canvas.transform.Find("ContextMenu");

        var title = panel.Find("CtxTitle").GetComponent<RectTransform>();
        // Заголовок заякорен к верху панели, pivot сверху → anchoredPosition.y
        // это его верхняя кромка (должна быть ниже верха панели, т.е. ≤ 0).
        float titleTopBelowPanelTop = title.anchoredPosition.y;
        Assert.LessOrEqual(titleTopBelowPanelTop, 0f,
            "title top must be below the panel top edge (inside the window)");
    }

    // Секция зазоров должна РЕАЛЬНО вмещать свои строки (иначе поля вылезают
    // за низ секции и наезжают на следующий блок).
    [Test]
    public void Facade_GapSectionContainsItsChildren()
    {
        var facade = MakeFacade("F1");
        _menu.Open(facade);
        var panel = _canvas.transform.Find("ContextMenu");
        var gapSection = panel.Find("_GapSection");
        var gapRt = gapSection.GetComponent<RectTransform>();

        // Дети с верхним pivot, заякорены к верху секции: верх = anchoredPosition.y
        // (0 — верхняя кромка секции), низ = верх − высота, дно секции = −sectionH.
        float sectionH = gapRt.sizeDelta.y;
        foreach (var childName in new[] { "CtxGapHdr", "Gap_Ширина X, мм", "F_gap_Ширина X, мм",
                                          "Gap_Высота Y, мм", "F_gap_Высота Y, мм" })
        {
            var child = gapSection.Find(childName).GetComponent<RectTransform>();
            float top = child.anchoredPosition.y;
            float bottom = child.anchoredPosition.y - child.sizeDelta.y;
            Assert.LessOrEqual(top, 0.5f, $"{childName} spills over section top");
            Assert.GreaterOrEqual(bottom, -sectionH - 0.5f, $"{childName} spills below section bottom");
        }
    }

    // Bug A: заголовок стоит вплотную над первой строкой, без большого провала.
    [Test]
    public void Title_SitsJustAboveFirstRow()
    {
        var board = MakeBoard("B1");
        _menu.Open(board);
        var panel = _canvas.transform.Find("ContextMenu");

        var title = panel.Find("CtxTitle").GetComponent<RectTransform>();
        var nameLbl = panel.Find("L_Название").GetComponent<RectTransform>();

        float titleBottom = title.anchoredPosition.y - title.sizeDelta.y;
        float nameTop = nameLbl.anchoredPosition.y;
        float gap = titleBottom - nameTop;

        Assert.GreaterOrEqual(gap, 0f, "title must not overlap the first row");
        Assert.LessOrEqual(gap, 20f, "gap between title and first row must be small");
    }

    // ── Открывание дверцы (только фасад) ─────────────────────────────

    [Test]
    public void Facade_HasDoorButton_WithOpenLabel()
    {
        var facade = MakeFacade("F1");
        _menu.Open(facade);
        var panel = _canvas.transform.Find("ContextMenu");

        var door = panel.Find("CtxDoor");
        Assert.NotNull(door, "у фасада должна быть кнопка открытия");
        Assert.IsTrue(door.gameObject.activeSelf, "кнопка активна для фасада");
        Assert.AreEqual("Открыть", door.GetComponentInChildren<Text>(true).text);
    }

    [Test]
    public void Facade_HasModeDropdown_With18Options()
    {
        var facade = MakeFacade("F1");
        _menu.Open(facade);
        var panel = _canvas.transform.Find("ContextMenu");

        var mode = panel.Find("CtxMode");
        Assert.NotNull(mode, "у фасада должен быть список режимов");
        Assert.IsTrue(mode.gameObject.activeSelf, "список активен для фасада");
        var dd = mode.GetComponent<Dropdown>();
        Assert.NotNull(dd, "CtxMode — это Dropdown");
        Assert.AreEqual(18, dd.options.Count, "18 режимов (12 рёбер + 6 ящиков)");
    }

    [Test]
    public void Board_DoorControls_Hidden()
    {
        var board = MakeBoard("B1");
        _menu.Open(board);
        var panel = _canvas.transform.Find("ContextMenu");

        foreach (var name in new[] { "CtxDoor", "CtxMode" })
        {
            var t = panel.Find(name);
            Assert.NotNull(t, $"{name} существует");
            Assert.IsFalse(t.gameObject.activeSelf, $"{name} должна быть скрыта для детали");
        }
    }

    [Test]
    public void DoorButton_Click_TogglesFacadeAndLabel()
    {
        var facade = MakeFacade("F1");
        _menu.Open(facade);
        var panel = _canvas.transform.Find("ContextMenu");
        var door = panel.Find("CtxDoor");
        var label = door.GetComponentInChildren<Text>(true);

        Assert.IsFalse(facade.IsOpen);
        door.GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(facade.IsOpen, "клик открывает дверцу");
        Assert.AreEqual("Закрыть", label.text);

        door.GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(facade.IsOpen, "повторный клик закрывает");
        Assert.AreEqual("Открыть", label.text);
    }

    [Test]
    public void ModeDropdown_Select_SetsFacadeMode()
    {
        var facade = MakeFacade("F1");
        _menu.Open(facade);
        var panel = _canvas.transform.Find("ContextMenu");
        var dd = panel.Find("CtxMode").GetComponent<Dropdown>();

        Assert.AreEqual(DoorMode.HingeFrontLeft, facade.Mode);
        dd.value = (int)DoorMode.DrawerOut;
        Assert.AreEqual(DoorMode.DrawerOut, facade.Mode, "выбор в списке ставит режим фасада");
        dd.value = (int)DoorMode.HingeBackTop;
        Assert.AreEqual(DoorMode.HingeBackTop, facade.Mode);
    }
}
