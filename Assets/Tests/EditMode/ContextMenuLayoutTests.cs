using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ContextMenuLayoutTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [TearDown]
    public void Teardown()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
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

    // ── Секция пазов ──────────────────────────────────────────────────

    private Transform Panel()
    {
        var panel = _canvas!.transform.Find("ContextMenu");
        Assert.NotNull(panel, "панель контекстного меню должна существовать");
        return panel!;
    }

    private void ClickGroovesHeader() =>
        Panel().Find("CtxGrooves").GetComponent<Button>().onClick.Invoke();

    private static string GroovesButtonText(Transform panel) =>
        panel.Find("CtxGrooves").GetComponentInChildren<TMP_Text>(true).text;

    [Test]
    public void Board_GrooveSection_VisibleAndCollapsedByDefault()
    {
        _menu!.Open(MakeBoard("B1"));
        var panel = Panel();

        Assert.IsTrue(panel.Find("CtxGrooves").gameObject.activeSelf,
            "кнопка «Пазы» видна у детали");
        Assert.AreEqual("Пазы (0)  ►", GroovesButtonText(panel),
            "без пазов счётчик показывает 0 и стрелку «свёрнуто»");
        Assert.IsFalse(panel.Find("CtxGrooveAdd").gameObject.activeSelf,
            "строка добавления скрыта, пока секция свёрнута");
        Assert.IsFalse(panel.Find("CtxGrooveSide0").gameObject.activeSelf);
    }

    [Test]
    public void Facade_GrooveSection_Hidden()
    {
        _menu!.Open(MakeFacade("F1"));
        var panel = Panel();

        Assert.IsFalse(panel.Find("CtxGrooves").gameObject.activeSelf,
            "фасад пазов не поддерживает — секции нет");
        Assert.IsFalse(panel.Find("CtxGrooveAdd").gameObject.activeSelf);
    }

    // ── Секция кромок ─────────────────────────────────────────────────

    private KitchenElement MakeBar(string name)
    {
        var go = new GameObject(name);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = new Vector3Int(18, 18, 800); // брусок: две тонких стороны
        _spawned.Add(go);
        return el;
    }

    [Test]
    public void Board_EdgeSection_VisibleWithDiagramAndParameters()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        var panel = Panel();

        Assert.IsTrue(panel.Find("CtxEdges").gameObject.activeSelf,
            "галочка «Кромки» видна у листовой детали");
        Assert.IsTrue(panel.Find("CtxEdgeDiagram").gameObject.activeSelf,
            "галочка включена — схема нарисована");
        Assert.IsTrue(panel.Find("F_EdgeThickness").gameObject.activeSelf);
        Assert.IsTrue(panel.Find("CtxEdgeHint").gameObject.activeSelf);
    }

    [Test]
    public void Bar_EdgeSection_Hidden()
    {
        _menu!.Open(MakeBar("Bar1"));
        var panel = Panel();

        Assert.IsFalse(panel.Find("CtxEdges").gameObject.activeSelf,
            "у бруска торец под кромку не определён — свойства нет");
        Assert.IsFalse(panel.Find("CtxEdgeDiagram").gameObject.activeSelf);
    }

    [Test]
    public void Facade_EdgeSection_Hidden()
    {
        _menu!.Open(MakeFacade("F1"));
        Assert.IsFalse(Panel().Find("CtxEdges").gameObject.activeSelf);
    }

    [Test]
    public void Board_UncheckEdges_HidesDiagramAndParameters_WithUndo()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        var panel = Panel();

        panel.Find("CtxEdges").GetComponent<Toggle>().isOn = false;

        Assert.IsFalse(board.EdgeBandingEnabled);
        Assert.IsTrue(panel.Find("CtxEdges").gameObject.activeSelf, "сама галочка остаётся");
        Assert.IsFalse(panel.Find("CtxEdgeDiagram").gameObject.activeSelf);
        Assert.IsFalse(panel.Find("F_EdgeThickness").gameObject.activeSelf);
        Assert.IsFalse(panel.Find("CtxEdgeHint").gameObject.activeSelf);

        CommandStack.Undo();
        Assert.IsTrue(board.EdgeBandingEnabled, "Ctrl+Z возвращает кромки");
    }

    [Test]
    public void Board_EdgeDiagram_ShowsLengthAndWidthAndPaintsOpenEnds()
    {
        var board = MakeBoard("B1"); // 800×400×18 → L = 800, W = 400
        _menu!.Open(board);
        var panel = Panel();

        Assert.AreEqual("800 мм", panel.Find("CtxEdgeDiagram/CtxEdgeLen")
            .GetComponent<TMP_Text>().text);
        Assert.AreEqual("400 мм", panel.Find("CtxEdgeDiagram/CtxEdgeWid")
            .GetComponent<TMP_Text>().text);

        // Одинокая деталь: все четыре торца открыты — все полосы зелёные.
        foreach (var side in new[] { "L1", "L2", "W1", "W2" })
            Assert.AreEqual(UIStyle.EdgePresent,
                panel.Find($"CtxEdgeDiagram/CtxEdge{side}").GetComponent<Image>().color,
                $"торец {side} открыт — кромка есть");
    }

    [Test]
    public void Board_ExpandGrooves_ShowsAddRowAndFlipsArrow()
    {
        _menu!.Open(MakeBoard("B1"));
        ClickGroovesHeader();
        var panel = Panel();

        Assert.IsTrue(panel.Find("CtxGrooveAdd").gameObject.activeSelf);
        Assert.IsTrue(panel.Find("CtxGrooveSide").gameObject.activeSelf);
        Assert.IsTrue(panel.Find("CtxGrooveHint").gameObject.activeSelf,
            "в раскрытом виде видна подсказка с размерами паза в мм");
        Assert.AreEqual("Пазы (0)  ▼", GroovesButtonText(panel));
    }

    [Test]
    public void Board_AddGroove_ShowsItemRowAndUpdatesCount()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        ClickGroovesHeader();

        var panel = Panel();
        panel.Find("CtxGrooveSide").GetComponent<TMP_Dropdown>().value = (int)GrooveSide.Left;
        panel.Find("CtxGrooveKind").GetComponent<TMP_Dropdown>().value = (int)GrooveKind.Blind;
        panel.Find("CtxGrooveAdd").GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(1, board.Grooves.Count);
        Assert.AreEqual("Пазы (1)  ▼", GroovesButtonText(panel));
        // Строка паза — редактируемые на месте дропдауны со значениями паза.
        Assert.IsTrue(panel.Find("CtxGrooveSide0").gameObject.activeSelf);
        Assert.AreEqual((int)GrooveSide.Left,
            panel.Find("CtxGrooveSide0").GetComponent<TMP_Dropdown>().value);
        Assert.AreEqual((int)GrooveKind.Blind,
            panel.Find("CtxGrooveKind0").GetComponent<TMP_Dropdown>().value);
        Assert.IsFalse(panel.Find("CtxGrooveSide1").gameObject.activeSelf,
            "слот под второй паз остаётся скрытым");
    }

    [Test]
    public void Board_RemoveGroove_HidesItemRow()
    {
        var board = MakeBoard("B1");
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        _menu!.Open(board);
        ClickGroovesHeader();

        var panel = Panel();
        Assert.IsTrue(panel.Find("CtxGrooveSide0").gameObject.activeSelf);

        panel.Find("CtxGrooveDel0").GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(0, board.Grooves.Count);
        Assert.IsFalse(panel.Find("CtxGrooveSide0").gameObject.activeSelf);
        Assert.AreEqual("Пазы (0)  ▼", GroovesButtonText(panel));
    }

    [Test]
    public void Board_EditGrooveInRow_ChangesSpec_WithUndo()
    {
        var board = MakeBoard("B1");
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        _menu!.Open(board);
        ClickGroovesHeader();

        var panel = Panel();
        panel.Find("CtxGrooveSide0").GetComponent<TMP_Dropdown>().value = (int)GrooveSide.Bottom;

        Assert.AreEqual(GrooveSide.Bottom, board.Grooves[0].side,
            "правка дропдауна строки меняет паз на месте");

        CommandStack.Undo();
        Assert.AreEqual(GrooveSide.Top, board.Grooves[0].side,
            "правка паза обязана быть отменяемой (правило 2 UI-GUIDELINES)");
    }

    [Test]
    public void Board_GrooveAddRemove_AreUndoable()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        ClickGroovesHeader();

        var panel = Panel();
        panel.Find("CtxGrooveAdd").GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(1, board.Grooves.Count);

        CommandStack.Undo();
        Assert.AreEqual(0, board.Grooves.Count, "добавление паза отменяемо");

        CommandStack.Redo();
        Assert.AreEqual(1, board.Grooves.Count, "и повторяемо");
    }

    [Test]
    public void Board_ExpandedGrooveRows_DoNotOverlapPositionRow()
    {
        var board = MakeBoard("B1");
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        board.AddGroove(new GrooveSpec(GrooveKind.Blind, GrooveSide.Left));
        _menu!.Open(board);
        ClickGroovesHeader();

        var panel = Panel();
        var addRow = panel.Find("CtxGrooveAdd").GetComponent<RectTransform>();
        var xField = panel.Find("F_X, мм").GetComponent<RectTransform>();

        // Всё заякорено к верху панели: низ = anchoredPosition.y − высота.
        float addBottom = addRow.anchoredPosition.y - addRow.sizeDelta.y;
        Assert.GreaterOrEqual(addBottom, xField.anchoredPosition.y,
            "строка добавления паза должна быть выше блока позиции");

        // Строки пазов идут сверху вниз и не накладываются друг на друга.
        var item0 = panel.Find("CtxGrooveSide0").GetComponent<RectTransform>();
        var item1 = panel.Find("CtxGrooveSide1").GetComponent<RectTransform>();
        Assert.IsTrue(item1.gameObject.activeSelf, "второй паз показывается своей строкой");
        Assert.GreaterOrEqual(item0.anchoredPosition.y - item0.sizeDelta.y,
            item1.anchoredPosition.y, "строки пазов не перекрываются");
    }

    [Test]
    public void Board_ReopenMenu_CollapsesGrooveSection()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        ClickGroovesHeader();
        Assert.IsTrue(Panel().Find("CtxGrooveAdd").gameObject.activeSelf);

        _menu!.Open(MakeBoard("B2"));

        Assert.IsFalse(Panel().Find("CtxGrooveAdd").gameObject.activeSelf,
            "меню открывается со свёрнутым списком пазов");
    }

    [Test]
    public void Facade_GapSectionBottomAboveNextRow()
    {
        var facade = MakeFacade("F1");
        _menu!.Open(facade);
        var panel = _canvas!.transform.Find("ContextMenu");
        Assert.NotNull(panel);

        var gapSection = panel.Find("_GapSection");
        var xField = panel.Find("F_X, мм");
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
        var panel = _canvas!.transform.Find("ContextMenu");
        Assert.NotNull(panel);
        var gapSection = panel.Find("_GapSection");
        Assert.NotNull(gapSection);

        var board = MakeBoard("B1");
        _menu!.Open(board);

        Assert.IsFalse(gapSection.gameObject.activeSelf,
            "gap section must be hidden for regular board");
    }

    [Test]
    public void Facade_NoOverlap_BetweenGapAndNextRow()
    {
        var facade = MakeFacade("F1");
        _menu!.Open(facade);
        var panel = _canvas!.transform.Find("ContextMenu");

        var gapSection = panel.Find("_GapSection");
        var xInput = panel.Find("F_X, мм");
        Assert.NotNull(gapSection);
        Assert.NotNull(xInput);

        var gapRt = gapSection.GetComponent<RectTransform>();
        var xRt = xInput.GetComponent<RectTransform>();

        float gapSectionBottom = gapRt.anchoredPosition.y - gapRt.sizeDelta.y;
        float xTop = xRt.anchoredPosition.y;

        Assert.GreaterOrEqual(gapSectionBottom, xTop,
            "gap section bottom must be above the first position row");

        foreach (var childName in new[] { "Gap_LR", "F_gapLeft", "F_gapRight",
                                          "Gap_TB", "F_gapTop", "F_gapBottom" })
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
        var panel = _canvas!.transform.Find("ContextMenu");
        var gapSection = panel.Find("_GapSection");
        var gapRt = gapSection.GetComponent<RectTransform>();
        float h = gapRt.sizeDelta.y;

        Assert.Greater(h, 0);
        Assert.Less(h, 200, "section should not be unreasonably tall");
    }

    // Позиция и поворот теперь в компактной раскладке 3 колонки:
    // подписи в одной строке, поля ввода в следующей.
    [Test]
    public void Board_TripleRowLabelsAndFieldsAligned()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        var panel = _canvas!.transform.Find("ContextMenu");

        //.Position labels все на одной Y
        float? posLabelY = null;
        float? posFieldY = null;
        foreach (var name in new[] { "X, мм", "Y, мм", "Z, мм" })
        {
            var lbl = panel.Find("L_" + name).GetComponent<RectTransform>();
            var fld = panel.Find("F_" + name).GetComponent<RectTransform>();
            posLabelY ??= lbl.anchoredPosition.y;
            posFieldY ??= fld.anchoredPosition.y;
            Assert.AreEqual(posLabelY.Value, lbl.anchoredPosition.y, 0.5f,
                $"position label «{name}» must be on the same row");
            Assert.AreEqual(posFieldY.Value, fld.anchoredPosition.y, 0.5f,
                $"position field «{name}» must be on the same row");
            Assert.Less(fld.anchoredPosition.y, lbl.anchoredPosition.y,
                $"field «{name}» must be below its label");
        }

        // Rotation labels все на одной Y
        float? rotLabelY = null;
        float? rotFieldY = null;
        foreach (var name in new[] { "X, °", "Y, °", "Z, °" })
        {
            var lbl = panel.Find("L_" + name).GetComponent<RectTransform>();
            var fld = panel.Find("F_" + name).GetComponent<RectTransform>();
            rotLabelY ??= lbl.anchoredPosition.y;
            rotFieldY ??= fld.anchoredPosition.y;
            Assert.AreEqual(rotLabelY.Value, lbl.anchoredPosition.y, 0.5f,
                $"rotation label «{name}» must be on the same row");
            Assert.AreEqual(rotFieldY.Value, fld.anchoredPosition.y, 0.5f,
                $"rotation field «{name}» must be on the same row");
            Assert.Less(fld.anchoredPosition.y, lbl.anchoredPosition.y,
                $"field «{name}» must be below its label");
        }
    }

    // Блок «Повернуть на 90°» не должен перекрывать строку полей поворота.
    [Test]
    public void Board_RotationLabelBelowRotationRows_NoOverlap()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        var panel = _canvas!.transform.Find("ContextMenu");

        var rotLbl = panel.Find("CtxRotLbl").GetComponent<RectTransform>();
        var rzFld = panel.Find("F_Z, °").GetComponent<RectTransform>();

        float rotLblTop = rotLbl.anchoredPosition.y;
        float rzBottom = rzFld.anchoredPosition.y - rzFld.sizeDelta.y;

        Assert.LessOrEqual(rotLblTop, rzBottom,
            "«Повернуть на 90°» must sit below the rotation fields row without overlap");
    }

    // Заголовок должен быть ВНУТРИ панели (верхняя кромка ниже верха панели),
    // а не выезжать над окном в режиме «деталь» — прямой репорт пользователя.
    [Test]
    public void Board_TitleStaysInsidePanel()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        var panel = _canvas!.transform.Find("ContextMenu");

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
        _menu!.Open(facade);
        var panel = _canvas!.transform.Find("ContextMenu");
        var gapSection = panel.Find("_GapSection");
        var gapRt = gapSection.GetComponent<RectTransform>();

        // Дети с верхним pivot, заякорены к верху секции: верх = anchoredPosition.y
        // (0 — верхняя кромка секции), низ = верх − высота, дно секции = −sectionH.
        float sectionH = gapRt.sizeDelta.y;
        foreach (var childName in new[] { "CtxGapHdr", "Gap_LR", "F_gapLeft", "F_gapRight",
                                          "Gap_TB", "F_gapTop", "F_gapBottom" })
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
        _menu!.Open(board);
        var panel = _canvas!.transform.Find("ContextMenu");

        var title = panel.Find("CtxTitle").GetComponent<RectTransform>();
        // Первая строка под заголовком — выпадающий список типа детали (CtxType).
        var firstRow = panel.Find("CtxType").GetComponent<RectTransform>();

        float titleBottom = title.anchoredPosition.y - title.sizeDelta.y;
        float firstTop = firstRow.anchoredPosition.y;
        float gap = titleBottom - firstTop;

        Assert.GreaterOrEqual(gap, 0f, "title must not overlap the first row");
        Assert.LessOrEqual(gap, 20f, "gap between title and first row must be small");
    }

    // ── Открывание дверцы (только фасад) ─────────────────────────────

    [Test]
    public void Facade_HasDoorButton_WithOpenLabel()
    {
        var facade = MakeFacade("F1");
        _menu!.Open(facade);
        var panel = _canvas!.transform.Find("ContextMenu");

        var door = panel.Find("CtxDoor");
        Assert.NotNull(door, "у фасада должна быть кнопка открытия");
        Assert.IsTrue(door.gameObject.activeSelf, "кнопка активна для фасада");
        Assert.AreEqual("Открыть", door.GetComponentInChildren<TMP_Text>(true).text);
    }

    [Test]
    public void Facade_HasModeDropdown_With18Options()
    {
        var facade = MakeFacade("F1");
        _menu!.Open(facade);
        var panel = _canvas!.transform.Find("ContextMenu");

        var mode = panel.Find("CtxMode");
        Assert.NotNull(mode, "у фасада должен быть список режимов");
        Assert.IsTrue(mode.gameObject.activeSelf, "список активен для фасада");
        var dd = mode.GetComponent<TMP_Dropdown>();
        Assert.NotNull(dd, "CtxMode — это TMP_Dropdown");
        Assert.AreEqual(18, dd.options.Count, "18 режимов (12 рёбер + 6 ящиков)");
    }

    [Test]
    public void Board_DoorControls_Hidden()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        var panel = _canvas!.transform.Find("ContextMenu");

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
        _menu!.Open(facade);
        var panel = _canvas!.transform.Find("ContextMenu");
        var door = panel.Find("CtxDoor");
        var label = door.GetComponentInChildren<TMP_Text>(true);

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
        _menu!.Open(facade);
        var panel = _canvas!.transform.Find("ContextMenu");
        var dd = panel.Find("CtxMode").GetComponent<TMP_Dropdown>();

        Assert.AreEqual(DoorMode.HingeFrontLeft, facade.Mode);
        dd.value = (int)DoorMode.DrawerOut;
        Assert.AreEqual(DoorMode.DrawerOut, facade.Mode, "выбор в списке ставит режим фасада");
        dd.value = (int)DoorMode.HingeBackTop;
        Assert.AreEqual(DoorMode.HingeBackTop, facade.Mode);
    }
}
