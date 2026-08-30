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

    // ── Секция накладок текстур ───────────────────────────────────────

    private KitchenElement MakeWall(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = new Vector3Int(3000, 2500, 100);
        go.AddComponent<Wall>();
        _spawned.Add(go);
        return el;
    }

    private KitchenElement OpenWallWithTwoOverlays()
    {
        var wall = MakeWall("Стена");
        wall.SetTextureOverlays(new[]
        {
            TextureOverlaySpec.FullFace(OverlaySide.A, "oak"),
            new TextureOverlaySpec(OverlaySide.A, "white", 100, 200, 1200, 900),
        });
        _menu!.Open(wall);
        Panel().Find("CtxTextures").GetComponent<Button>().onClick.Invoke();
        return wall;
    }

    private Button OrderButton(int row, bool up) =>
        Panel().Find($"CtxTexOrder{row}/CtxTex{(up ? "Up" : "Down")}{row}").GetComponent<Button>();

    [Test]
    public void Wall_TextureOrderArrows_SwapNeighbours_AndAreUndoable()
    {
        var wall = OpenWallWithTwoOverlays();

        OrderButton(0, up: false).onClick.Invoke();
        Assert.AreEqual("white", wall.TextureOverlays[0].MaterialId,
            "«вниз» опускает накладку по списку — она уходит под соседку");
        Assert.AreEqual("oak", wall.TextureOverlays[1].MaterialId);

        OrderButton(1, up: true).onClick.Invoke();
        Assert.AreEqual("oak", wall.TextureOverlays[0].MaterialId, "«вверх» возвращает порядок");

        CommandStack.Undo();
        Assert.AreEqual("white", wall.TextureOverlays[0].MaterialId,
            "смена порядка обязана быть отменяемой (правило 2 UI-GUIDELINES)");
    }

    [Test]
    public void Wall_TextureOrderArrows_AreDisabledAtListEnds()
    {
        OpenWallWithTwoOverlays();

        Assert.IsFalse(OrderButton(0, up: true).interactable, "верхнюю накладку выше не поднять");
        Assert.IsTrue(OrderButton(0, up: false).interactable);
        Assert.IsTrue(OrderButton(1, up: true).interactable);
        Assert.IsFalse(OrderButton(1, up: false).interactable, "нижнюю ниже не опустить");
    }

    [Test]
    public void Wall_TextureOrderColumn_FitsOneButtonCell()
    {
        OpenWallWithTwoOverlays();

        var column = Panel().Find("CtxTexOrder0").GetComponent<RectTransform>();
        var edit = Panel().Find("CtxTexEdit0").GetComponent<RectTransform>();
        Assert.AreEqual(edit.sizeDelta, column.sizeDelta,
            "колонка стрелок занимает ровно одну кнопочную клетку");

        var up = OrderButton(0, up: true).GetComponent<RectTransform>();
        var down = OrderButton(0, up: false).GetComponent<RectTransform>();
        Assert.AreEqual(column.sizeDelta.x, up.sizeDelta.x);
        Assert.Greater(up.anchoredPosition.y, down.anchoredPosition.y, "↑ сверху, ↓ снизу");
        Assert.LessOrEqual(up.sizeDelta.y * 2f, column.sizeDelta.y,
            "обе половинки помещаются в клетку по высоте");
    }

    // ── Предпросмотр декора наведением ────────────────────────────────
    private static int MaterialIndexOf(string id)
    {
        var all = MaterialCatalog.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i].id == id) return i;
        Assert.Fail($"декора «{id}» нет в каталоге");
        return -1;
    }

    private void Preview(int row, int option) => _menu!.Textures.PreviewMaterial(row, option);

    private void EndPreview() => _menu!.Textures.EndPreview();

    [Test]
    public void Wall_TextureHover_ShowsDecorOnElement_AndRestoresOnExit()
    {
        var wall = OpenWallWithTwoOverlays();

        Preview(0, MaterialIndexOf("white"));
        Assert.AreEqual("white", wall.TextureOverlays[0].MaterialId,
            "наведение на пункт показывает декор на объекте");
        Assert.AreEqual(2, wall.TextureOverlays.Count);

        EndPreview();
        Assert.AreEqual("oak", wall.TextureOverlays[0].MaterialId,
            "ушли с пункта, ничего не выбрав — набор возвращается как был");
    }

    [Test]
    public void Wall_AddRowHover_ShowsFutureOverlay_AndRemovesItOnExit()
    {
        var wall = OpenWallWithTwoOverlays();

        Preview(-1, MaterialIndexOf("white"));
        Assert.AreEqual(3, wall.TextureOverlays.Count,
            "в строке добавления накладки ещё нет — предпросмотр дорисовывает будущую");

        EndPreview();
        Assert.AreEqual(2, wall.TextureOverlays.Count);
    }

    [Test]
    public void Wall_TexturePreview_ThenSelect_UndoRestoresOriginalDecor()
    {
        var wall = OpenWallWithTwoOverlays();
        int white = MaterialIndexOf("white");

        Preview(0, white);
        // Пользователь всё-таки выбрал этот пункт — дропдаун шлёт onValueChanged.
        Panel().Find("CtxTexMat0").GetComponent<TMP_Dropdown>().value = white;
        Assert.AreEqual("white", wall.TextureOverlays[0].MaterialId);

        CommandStack.Undo();
        Assert.AreEqual("oak", wall.TextureOverlays[0].MaterialId,
            "Ctrl+Z возвращает исходный декор, а не показанный предпросмотром");
    }

    [Test]
    public void Wall_ClosingMenuDuringPreview_RestoresOverlays()
    {
        var wall = OpenWallWithTwoOverlays();

        Preview(0, MaterialIndexOf("white"));
        _menu!.Close();

        Assert.AreEqual("oak", wall.TextureOverlays[0].MaterialId,
            "показанная накладка не должна пережить закрытие панели");
    }

    [Test]
    public void Wall_AddSameTextureTwice_IsAllowed()
    {
        var wall = MakeWall("Стена");
        _menu!.Open(wall);
        Panel().Find("CtxTextures").GetComponent<Button>().onClick.Invoke();

        var add = Panel().Find("CtxTexAdd").GetComponent<Button>();
        add.onClick.Invoke();
        add.onClick.Invoke();

        Assert.AreEqual(2, wall.TextureOverlays.Count,
            "две одинаковые накладки — законное начало работы: их разводят ручками");
    }

    [Test]
    public void ConfirmDelete_SurvivesItsOwnConfirmingPress()
    {
        // Button шлёт onClick на ОТПУСКАНИИ, а сторож смотрит на нажатие: без
        // проверки «нажали по самой кнопке» взвод гас бы раньше клика, и кнопка
        // молча взводилась бы заново, ничего не удаляя.
        Assert.IsFalse(ConfirmDeleteButton.ShouldDisarm(10, 10, 9, true, false),
            "кадр взвода пропускаем");
        Assert.IsFalse(ConfirmDeleteButton.ShouldDisarm(20, 10, 20, true, false),
            "нажатие по самой кнопке взвод не снимает");
        Assert.IsTrue(ConfirmDeleteButton.ShouldDisarm(20, 10, 9, true, false),
            "нажатие мимо — снимает");
        Assert.IsFalse(ConfirmDeleteButton.ShouldDisarm(20, 10, 9, false, false),
            "без нажатия взвод держится");
        Assert.IsTrue(ConfirmDeleteButton.ShouldDisarm(20, 10, 20, false, true),
            "Escape и ПКМ гасят даже над кнопкой");
    }

    [Test]
    public void Wall_TextureDelete_NeedsTwoClicks()
    {
        var wall = OpenWallWithTwoOverlays();
        var del = Panel().Find("CtxTexDel0").GetComponent<Button>();

        del.onClick.Invoke();
        Assert.AreEqual(2, wall.TextureOverlays.Count, "первый клик спрашивает");
        del.onClick.Invoke();
        Assert.AreEqual(1, wall.TextureOverlays.Count, "второй удаляет");
        Assert.AreEqual("white", wall.TextureOverlays[0].MaterialId);
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

        var del = panel.Find("CtxGrooveDel0").GetComponent<Button>();
        del.onClick.Invoke();
        Assert.AreEqual(1, board.Grooves.Count,
            "первый клик только взводит кнопку (правило 3 UI-GUIDELINES)");
        del.onClick.Invoke();

        Assert.AreEqual(0, board.Grooves.Count);
        Assert.IsFalse(panel.Find("CtxGrooveSide0").gameObject.activeSelf);
        Assert.AreEqual("Пазы (0)  ▼", GroovesButtonText(panel));
    }

    [Test]
    public void Board_DeleteButton_ArmsThenConfirms_AndShowsGlyph()
    {
        var board = MakeBoard("B1");
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        _menu!.Open(board);
        ClickGroovesHeader();

        var panel = Panel();
        var del = panel.Find("CtxGrooveDel0").GetComponent<Button>();
        var label = del.GetComponentInChildren<TMP_Text>(true);
        var confirm = del.GetComponent<ConfirmDeleteButton>();
        Assert.NotNull(confirm, "у кнопки удаления обязано быть подтверждение");
        Assert.AreEqual(UIStyle.GlyphClose, label.text);

        del.onClick.Invoke();
        Assert.IsTrue(confirm!.Armed);
        Assert.AreEqual(UIStyle.GlyphConfirm, label.text,
            "взведённая кнопка спрашивает, а не удаляет молча");
        Assert.AreEqual(1, board.Grooves.Count);

        // Клик по любому другому контролу снимает взвод.
        confirm.Disarm();
        Assert.AreEqual(UIStyle.GlyphClose, label.text);
        del.onClick.Invoke();
        Assert.AreEqual(1, board.Grooves.Count,
            "после сброса счёт кликов начинается заново");
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
    // ── Секция зазоров ────────────────────────────────────────────────
    // Устроена как пазы: свёрнутая раскрывашка со счётчиком, поля — под ней.

    private void ExpandGaps() =>
        Panel().Find("CtxGaps").GetComponent<Button>().onClick.Invoke();

    private string GapHeaderText() =>
        Panel().Find("CtxGaps").GetComponentInChildren<TMP_Text>(true).text;

    [Test]
    public void Facade_GapsCollapsedOnOpen()
    {
        _menu!.Open(MakeFacade("F1"));

        Assert.IsTrue(Panel().Find("CtxGaps").gameObject.activeSelf,
            "заголовок секции зазоров виден у фасада");
        Assert.IsFalse(Panel().Find("F_gapLeft").gameObject.activeSelf,
            "меню открывается со свёрнутой секцией зазоров");
    }

    [Test]
    public void Board_HasGapsSection()
    {
        _menu!.Open(MakeBoard("B1"));

        Assert.IsTrue(Panel().Find("CtxGaps").gameObject.activeSelf,
            "зазоры есть и у обычной детали (по умолчанию нулевые)");
        Assert.IsTrue(GapHeaderText().StartsWith("Зазоры (0)"),
            $"у детали зазоров нет, а в заголовке «{GapHeaderText()}»");
    }

    [Test]
    public void GapHeader_CountsNonZeroSides()
    {
        var facade = MakeFacade("F1");
        facade.GapLeft = 2;
        facade.GapRight = 2;
        facade.GapTop = 2;
        facade.GapBottom = 0;
        _menu!.Open(facade);

        Assert.IsTrue(GapHeaderText().StartsWith("Зазоры (3)"),
            $"счётчик считает стороны с ненулевым зазором, а в заголовке «{GapHeaderText()}»");
    }

    [Test]
    public void GapsExpanded_ShowsAllSixFields()
    {
        _menu!.Open(MakeFacade("F1"));
        ExpandGaps();

        foreach (var name in new[] { "F_gapLeft", "F_gapRight", "F_gapTop",
                                     "F_gapBottom", "F_gapFront", "F_gapBack" })
            Assert.IsTrue(Panel().Find(name).gameObject.activeSelf,
                $"{name} должно быть видно в раскрытой секции");
    }

    [Test]
    public void GapRows_StayAbovePositionRow()
    {
        _menu!.Open(MakeFacade("F1"));
        ExpandGaps();
        var panel = Panel();

        var xRt = panel.Find("F_X, мм").GetComponent<RectTransform>();
        float xTop = xRt.anchoredPosition.y;

        foreach (var name in new[] { "CtxGaps", "F_gapLeft", "F_gapFront", "F_gapBack" })
        {
            var rt = panel.Find(name).GetComponent<RectTransform>();
            // Верхний pivot: низ строки = верх − высота.
            float bottom = rt.anchoredPosition.y - rt.sizeDelta.y;
            Assert.GreaterOrEqual(bottom, xTop,
                $"{name} наезжает на строку положения");
        }
    }

    [Test]
    public void Window_HasNoGapsSection()
    {
        var go = new GameObject("W1");
        var win = go.AddComponent<WindowElement>();
        win.PartName = "W1";
        win.DimensionsMM = new Vector3Int(800, 1200, 100);
        _spawned.Add(go);

        _menu!.Open(win);

        Assert.IsFalse(Panel().Find("CtxGaps").gameObject.activeSelf,
            "у окна зазоров нет");
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
