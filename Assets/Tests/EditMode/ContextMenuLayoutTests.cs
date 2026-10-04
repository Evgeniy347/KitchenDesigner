using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

public class ContextMenuLayoutTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    /// <summary>Панель строится ОДИН раз на класс, а не в каждом из сорока тестов:
    /// сборка контекстного меню — 0,31 с, и сорок сборок это 12,6 с из 193-секундного
    /// прогона EditMode при бюджете 170.
    ///
    /// Переоткрытие ОДНОЙ панели — это и есть боевой сценарий: в приложении панель
    /// живёт всю сессию, а <c>ContextMenuUI.Open</c> и есть её сброс (гасит пазы,
    /// текстуры, световые связи, зазоры, закрывает превью, снимает взвод кнопок
    /// удаления через <c>Refresh</c> списков, перечитывает поля, зовёт <c>Show</c>
    /// каждому редактору, <c>RelayoutForTarget</c>, <c>SetIsOnWithoutNotify</c>,
    /// <c>ClearHighlights</c> + <c>TrackAllFields</c>, <c>SyncEnabledState</c>).
    /// Через <c>Open</c> проходит КАЖДЫЙ тест этого класса, кроме
    /// <see cref="ConfirmDelete_SurvivesItsOwnConfirmingPress"/>, который панель не
    /// трогает вовсе (чистая арифметика <c>ConfirmDeleteButton.ShouldDisarm</c>).
    /// «Своя панель на каждый тест» проверяла сценарий, которого в приложении нет.
    ///
    /// Единственное, что <c>Open</c> сбрасывал НЕ всегда, — списки дропдаунов
    /// «Прикрепить к» и «Фасад»: они перестраивались только для типа, попавшего в
    /// свою ветку, и устаревший набор опций переживал переключение. Это закрыто в
    /// продукте (<c>ContextMenuUI.Open</c> перестраивает оба всегда), а не оснасткой:
    /// с условной перестройкой одна панель на класс была бы честной ровно до
    /// первого теста, который прочитает опции.
    ///
    /// Элементы теста уносятся в <c>[TearDown]</c>, и панель перед этим закрывается:
    /// <c>Close</c> обнуляет <c>_target</c>, иначе живая панель осталась бы с
    /// уничтоженной деталью в руках.</summary>
    [OneTimeSetUp]
    public void BuildThePanelOnce()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [OneTimeTearDown]
    public void DropThePanel()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
    }

    [TearDown]
    public void Teardown()
    {
        if (_menu != null)
        {
            _menu!.Close();
            _menu!.TestHooks.ForgetSectionStates();
        }
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
        var panel = _canvas!.transform.FindNode("ContextMenu");
        Assert.NotNull(panel, "панель контекстного меню должна существовать");
        return panel!;
    }

    private void ClickGroovesHeader() => SetSection("Grooves", expanded: true);

    private void SetSection(string id, bool expanded) =>
        Panel().FindNode("Sec_" + id).GetComponent<CollapsibleSection>().SetExpanded(expanded, notify: true);

    private static string CountOf(Transform panel, string id) =>
        panel.FindNode("Sec_" + id).FindNode(CollapsibleSection.CountNode).GetComponent<TMP_Text>().text;

    private static bool ExpandedOf(Transform panel, string id) =>
        panel.FindNode("Sec_" + id).GetComponent<CollapsibleSection>().Expanded;

    [Test]
    public void Board_GrooveSection_VisibleAndCollapsedByDefault()
    {
        _menu!.Open(MakeBoard("B1"));
        var panel = Panel();

        Assert.IsTrue(panel.FindNode("Sec_Grooves").IsShown(),
            "заголовок «Пазы» виден у детали");
        Assert.AreEqual("0", CountOf(panel, "Grooves"), "без пазов счётчик показывает 0");
        Assert.IsFalse(ExpandedOf(panel, "Grooves"), "секция пазов свёрнута по умолчанию");
        Assert.IsTrue(panel.FindNode("CtxGrooveAdd").IsShown(),
            "ссылка «+ Добавить» живёт в шапке и видна и в свёрнутом виде");
        Assert.IsFalse(panel.FindNode("CtxGrooveHint").IsShown(),
            "подсказка скрыта, пока секция свёрнута");
        Assert.IsFalse(panel.FindNode("CtxGrooveSide0").IsShown());
    }

    [Test]
    public void Facade_GrooveSection_Hidden()
    {
        _menu!.Open(MakeFacade("F1"));
        var panel = Panel();

        Assert.IsFalse(panel.FindNode("Sec_Grooves").IsShown(),
            "фасад пазов не поддерживает — секции нет");
        Assert.IsFalse(panel.FindNode("CtxGrooveAdd").IsShown());
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
        SetSection("Textures", expanded: true);
        return wall;
    }

    private Button OrderButton(int row, bool up) =>
        Panel().FindNode($"CtxTexOrder{row}/CtxTex{(up ? "Up" : "Down")}{row}").GetComponent<Button>();

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

        var column = Panel().FindNode("CtxTexOrder0").GetComponent<RectTransform>();
        var edit = Panel().FindNode("CtxTexEdit0").GetComponent<RectTransform>();
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
    public void Wall_TexturePreview_ThenSelect_UndoRestoresOriginalDecor()
    {
        var wall = OpenWallWithTwoOverlays();
        int white = MaterialIndexOf("white");

        Preview(0, white);
        // Пользователь всё-таки выбрал этот пункт — дропдаун шлёт onValueChanged.
        Panel().FindNode("CtxTexMat0").GetComponent<TMP_Dropdown>().value = white;
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
        SetSection("Textures", expanded: true);

        var add = Panel().FindNode("CtxTexAdd").GetComponent<Button>();
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
        var del = Panel().FindNode("CtxTexDel0").GetComponent<Button>();

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

        Assert.IsTrue(panel.FindNode("CtxEdges").gameObject.activeInHierarchy,
            "галочка «Кромки» видна у листовой детали");
        Assert.IsTrue(panel.FindNode("CtxEdgeDiagram").gameObject.activeInHierarchy,
            "галочка включена — схема нарисована");
        Assert.IsTrue(panel.FindNode("F_EdgeThickness").gameObject.activeInHierarchy);
        Assert.IsTrue(panel.FindNode("CtxEdgeHint").gameObject.activeInHierarchy);
    }

    [Test]
    public void Bar_EdgeSection_Hidden()
    {
        _menu!.Open(MakeBar("Bar1"));
        var panel = Panel();

        Assert.IsFalse(panel.FindNode("CtxEdges").gameObject.activeInHierarchy,
            "у бруска торец под кромку не определён — свойства нет");
        Assert.IsFalse(panel.FindNode("CtxEdgeDiagram").gameObject.activeInHierarchy);
    }

    [Test]
    public void Facade_EdgeSection_Hidden()
    {
        _menu!.Open(MakeFacade("F1"));
        Assert.IsFalse(Panel().FindNode("CtxEdges").gameObject.activeInHierarchy);
    }

    [Test]
    public void Board_UncheckEdges_HidesDiagramAndParameters_WithUndo()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        var panel = Panel();

        panel.FindNode("CtxEdges").GetComponent<Toggle>().isOn = false;

        Assert.IsFalse(board.EdgeBandingEnabled);
        Assert.IsTrue(panel.FindNode("CtxEdges").gameObject.activeInHierarchy, "сама галочка остаётся");
        Assert.IsFalse(panel.FindNode("CtxEdgeDiagram").gameObject.activeInHierarchy);
        Assert.IsFalse(panel.FindNode("F_EdgeThickness").gameObject.activeInHierarchy);
        Assert.IsFalse(panel.FindNode("CtxEdgeHint").gameObject.activeInHierarchy);

        CommandStack.Undo();
        Assert.IsTrue(board.EdgeBandingEnabled, "Ctrl+Z возвращает кромки");
    }

    [Test]
    public void Board_EdgeDiagram_ShowsLengthAndWidthAndPaintsOpenEnds()
    {
        var board = MakeBoard("B1"); // 800×400×18 → L = 800, W = 400
        _menu!.Open(board);
        var panel = Panel();

        Assert.AreEqual("800 мм", panel.FindNode("CtxEdgeDiagram/CtxEdgeLen")
            .GetComponent<TMP_Text>().text);
        Assert.AreEqual("400 мм", panel.FindNode("CtxEdgeDiagram/CtxEdgeWid")
            .GetComponent<TMP_Text>().text);

        // Одинокая деталь: все четыре торца открыты — все полосы зелёные.
        foreach (var side in new[] { "L1", "L2", "W1", "W2" })
            Assert.AreEqual(UIStyle.EdgePresent,
                panel.FindNode($"CtxEdgeDiagram/CtxEdge{side}").GetComponent<Image>().color,
                $"торец {side} открыт — кромка есть");

        // Цвет продублирован формой (UI-GUIDELINES правило 10): сторона С кромкой
        // залита сплошь, сторона БЕЗ кромки — пустой контур. Иначе жёлтый,
        // зелёный и красный неразличимы при дальтонизме и в оттенках серого.
        foreach (var side in new[] { "L1", "L2", "W1", "W2" })
            Assert.IsFalse(panel.FindNode($"CtxEdgeDiagram/CtxEdge{side}/CtxEdge{side}Hole")
                .GetComponent<Image>().enabled,
                $"торец {side} с кромкой залит сплошь, а не нарисован контуром");
    }

    [Test]
    public void Board_ExpandGrooves_ShowsHintAndFlipsChevron()
    {
        _menu!.Open(MakeBoard("B1"));
        ClickGroovesHeader();
        var panel = Panel();

        Assert.IsTrue(panel.FindNode("CtxGrooveAdd").IsShown());
        Assert.IsTrue(panel.FindNode("CtxGrooveHint").IsShown(),
            "в раскрытом виде видна подсказка с размерами паза в мм");
        Assert.AreEqual(UIStyle.GlyphExpanded,
            panel.FindNode("Sec_Grooves").FindNode(CollapsibleSection.ChevronNode).GetComponent<TMP_Text>().text);
        Assert.AreEqual("0", CountOf(panel, "Grooves"));
    }

    [Test]
    public void Board_AddGroove_ShowsItemRowAndUpdatesCount()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);

        var panel = Panel();
        panel.FindNode("CtxGrooveAdd").GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(1, board.Grooves.Count);
        Assert.AreEqual(new GrooveSpec(GrooveKind.Through, GrooveSide.Top), board.Grooves[0],
            "ссылка «+ Добавить» кладёт первый свободный паз: сквозной сверху");
        Assert.AreEqual("1", CountOf(panel, "Grooves"));
        Assert.IsTrue(ExpandedOf(panel, "Grooves"), "добавление раскрывает секцию — результат должен быть виден");
        Assert.IsTrue(panel.FindNode("CtxGrooveSide0").IsShown());
        Assert.AreEqual((int)GrooveSide.Top,
            panel.FindNode("CtxGrooveSide0").GetComponent<TMP_Dropdown>().value);
        Assert.AreEqual((int)GrooveKind.Through,
            panel.FindNode("CtxGrooveKind0").GetComponent<TMP_Dropdown>().value);
        Assert.IsFalse(panel.FindNode("CtxGrooveSide1").IsShown(),
            "слот под второй паз остаётся скрытым");
    }

    [Test]
    public void Board_AddGroove_TwiceGivesTwoDifferentGrooves()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        var add = Panel().FindNode("CtxGrooveAdd").GetComponent<Button>();

        add.onClick.Invoke();
        add.onClick.Invoke();

        Assert.AreEqual(2, board.Grooves.Count);
        Assert.AreNotEqual(board.Grooves[0], board.Grooves[1],
            "одинаковый паз дважды не нужен — вторая ссылка берёт следующий свободный");
    }

    [Test]
    public void Board_RemoveGroove_HidesItemRow()
    {
        var board = MakeBoard("B1");
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        _menu!.Open(board);
        ClickGroovesHeader();

        var panel = Panel();
        Assert.IsTrue(panel.FindNode("CtxGrooveSide0").IsShown());

        var del = panel.FindNode("CtxGrooveDel0").GetComponent<Button>();
        del.onClick.Invoke();
        Assert.AreEqual(1, board.Grooves.Count,
            "первый клик только взводит кнопку (правило 3 UI-GUIDELINES)");
        del.onClick.Invoke();

        Assert.AreEqual(0, board.Grooves.Count);
        Assert.IsFalse(panel.FindNode("CtxGrooveSide0").IsShown());
        Assert.AreEqual("0", CountOf(panel, "Grooves"));
    }

    [Test]
    public void Board_DeleteButton_ArmsThenConfirms_AndShowsGlyph()
    {
        var board = MakeBoard("B1");
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        _menu!.Open(board);
        ClickGroovesHeader();

        var panel = Panel();
        var del = panel.FindNode("CtxGrooveDel0").GetComponent<Button>();
        var label = del.GetComponentInChildren<TMP_Text>(true);
        var confirm = del.GetComponent<ConfirmDeleteButton>();
        Assert.NotNull(confirm, "у кнопки удаления обязано быть подтверждение");
        Assert.AreEqual(UIStyle.GlyphClose, label.text);

        del.onClick.Invoke();
        Assert.IsTrue(confirm!.Armed);
        Assert.AreEqual(UIStyle.GlyphConfirm, label.text,
            "взведённая кнопка спрашивает, а не удаляет молча");
        Assert.AreEqual(1, board.Grooves.Count);

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
        panel.FindNode("CtxGrooveSide0").GetComponent<TMP_Dropdown>().value = (int)GrooveSide.Bottom;

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

        var panel = Panel();
        panel.FindNode("CtxGrooveAdd").GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(1, board.Grooves.Count);

        CommandStack.Undo();
        Assert.AreEqual(0, board.Grooves.Count, "добавление паза отменяемо");

        CommandStack.Redo();
        Assert.AreEqual(1, board.Grooves.Count, "и повторяемо");
    }

    private static float TopOf(Transform node) =>
        RowOf(node).anchoredPosition.y;

    private static float BottomOf(Transform node)
    {
        var row = RowOf(node);
        return row.anchoredPosition.y - row.sizeDelta.y;
    }

    private static RectTransform RowOf(Transform node)
    {
        for (var t = node; t != null; t = t.parent)
            if (t.name.StartsWith("Row_")) return (RectTransform)t;
        Assert.Fail($"у узла {node.name} нет строки Row_* среди предков");
        return null!;
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
        var position = panel.FindNode("Vec_Position_X")!;

        var item0 = panel.FindNode("CtxGrooveSide0")!;
        var item1 = panel.FindNode("CtxGrooveSide1")!;
        Assert.IsTrue(item1.IsShown(), "второй паз показывается своей строкой");
        Assert.GreaterOrEqual(BottomOf(item0), TopOf(item1), "строки пазов не перекрываются");
        Assert.GreaterOrEqual(BottomOf(item1), TopOf(position),
            "строки пазов должны быть выше блока позиции");
    }

    [Test]
    public void Board_ReopenSameType_KeepsTheGrooveSectionAsTheUserLeftIt()
    {
        _menu!.Open(MakeBoard("B1"));
        ClickGroovesHeader();
        Assert.IsTrue(ExpandedOf(Panel(), "Grooves"));

        _menu!.Open(MakeBoard("B2"));

        Assert.IsTrue(ExpandedOf(Panel(), "Grooves"),
            "свёрнутость помнится по типу элемента на сессию (UI-GUIDELINES D7)");
    }

    [Test]
    public void Board_GrooveSection_StartsCollapsed_ForAFreshSession()
    {
        _menu!.Open(MakeBoard("B1"));

        Assert.IsFalse(ExpandedOf(Panel(), "Grooves"),
            "пазы — редкая секция: без памяти меню открывается со свёрнутыми пазами");
    }

    [Test]
    public void SectionMemory_IsPerElementType()
    {
        _menu!.Open(MakeBoard("B1"));
        ClickGroovesHeader();
        _menu!.Open(MakeWall("Стена"));
        Assert.IsFalse(ExpandedOf(Panel(), "Grooves"),
            "у стены пазы ещё не открывали — память по типу, а не по секции вообще");
        SetSection("Grooves", expanded: true);
        SetSection("Grooves", expanded: false);

        _menu!.Open(MakeBoard("B3"));

        Assert.IsTrue(ExpandedOf(Panel(), "Grooves"),
            "пока открывали стену, состояние секций детали не потерялось");
    }
    // ── Секция зазоров ────────────────────────────────────────────────
    // Устроена как пазы: свёрнутая раскрывашка со счётчиком, поля — под ней.

    private void ExpandGaps() => SetSection("Gaps", expanded: true);

    private string GapCount() => CountOf(Panel(), "Gaps");

    [Test]
    public void Facade_GapsCollapsedOnOpen()
    {
        _menu!.Open(MakeFacade("F1"));

        Assert.IsTrue(Panel().FindNode("Sec_Gaps").gameObject.activeInHierarchy,
            "заголовок секции зазоров виден у фасада");
        Assert.IsFalse(Panel().FindNode("F_gapLeft").gameObject.activeInHierarchy,
            "меню открывается со свёрнутой секцией зазоров");
    }

    [Test]
    public void Board_HasGapsSection()
    {
        _menu!.Open(MakeBoard("B1"));

        Assert.IsTrue(Panel().FindNode("Sec_Gaps").IsShown(),
            "зазоры есть и у обычной детали (по умолчанию нулевые)");
        Assert.AreEqual("0", GapCount(), "у детали зазоров нет — счётчик показывает 0");
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

        Assert.AreEqual("3", GapCount(),
            "счётчик считает стороны с ненулевым зазором");
    }

    [Test]
    public void GapsExpanded_ShowsAllSixFields()
    {
        _menu!.Open(MakeFacade("F1"));
        ExpandGaps();

        foreach (var name in new[] { "F_gapLeft", "F_gapRight", "F_gapTop",
                                     "F_gapBottom", "F_gapFront", "F_gapBack" })
            Assert.IsTrue(Panel().FindNode(name).gameObject.activeInHierarchy,
                $"{name} должно быть видно в раскрытой секции");
    }

    [Test]
    public void GapRows_StayAbovePositionRow()
    {
        _menu!.Open(MakeFacade("F1"));
        ExpandGaps();
        var panel = Panel();

        float positionTop = TopOf(panel.FindNode("Vec_Position_X")!);

        foreach (var name in new[] { "Sec_Gaps", "F_gapLeft", "F_gapFront", "F_gapBack" })
            Assert.GreaterOrEqual(BottomOf(panel.FindNode(name)!), positionTop,
                $"{name} наезжает на строку положения");
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

        Assert.IsFalse(Panel().FindNode("Sec_Gaps").IsShown(),
            "у окна зазоров нет");
    }

    [Test]
    public void Board_PositionAndRotation_AreTwoVectorRows_ThreeFieldsEach()
    {
        _menu!.Open(MakeBoard("B1"));
        var panel = Panel();

        foreach (var vector in new[] { "Position", "Rotation" })
        {
            float? y = null;
            foreach (var axis in VectorField.AxisNames)
            {
                var field = panel.FindNode($"Vec_{vector}_{axis}")!;
                Assert.IsTrue(field.IsShown(), $"{vector}: поле оси {axis} видно у обычной детали");
                y ??= TopOf(field);
                Assert.AreEqual(y.Value, TopOf(field), 0.5f, $"{vector}: три поля стоят в одной строке");
            }
        }
        Assert.Greater(TopOf(panel.FindNode("Vec_Position_X")!), TopOf(panel.FindNode("Vec_Rotation_X")!),
            "позиция выше поворота");
    }

    [Test]
    public void Board_RotationRow_HasATurnButtonPerAxis()
    {
        _menu!.Open(MakeBoard("B1"));
        var panel = Panel();

        foreach (var axis in VectorField.AxisNames)
        {
            var field = panel.FindNode($"Vec_Rotation_{axis}")!;
            Assert.IsNotNull(field.FindNode(ContextMenuUI.RotateButtonPrefix + axis),
                $"поворот на 90° — иконка внутри поля оси {axis}, а не полоса из трёх кнопок");
        }
    }

    [Test]
    public void TurnButton_RotatesTheElementAroundItsAxis_ByAQuarter()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        var button = Panel().FindNode("CtxRotY")!.GetComponent<Button>();

        button.onClick.Invoke();

        Assert.AreEqual(90f, board.transform.rotation.eulerAngles.y, 0.5f,
            "кнопка ↻ в поле Y поворачивает деталь на 90° вокруг Y");
    }

    [Test]
    public void Window_RotationRow_ShowsOnlyYaw_AndTheTurnIsAHalfTurn()
    {
        var go = new GameObject("W1");
        var win = go.AddComponent<WindowElement>();
        win.PartName = "W1";
        win.DimensionsMM = new Vector3Int(800, 1200, 100);
        _spawned.Add(go);

        _menu!.Open(win);
        var panel = Panel();

        Assert.IsFalse(panel.FindNode("Vec_Rotation_X").IsShown(), "ориентацию окна диктует стена — X скрыт");
        Assert.IsFalse(panel.FindNode("Vec_Rotation_Z").IsShown(), "и Z скрыт");
        Assert.IsTrue(panel.FindNode("Vec_Rotation_Y").IsShown(), "поворот вокруг Y остался");
    }

    // ── Открывание дверцы (только фасад) ─────────────────────────────

    [Test]
    public void Facade_HasDoorButton_WithOpenLabel()
    {
        var facade = MakeFacade("F1");
        _menu!.Open(facade);
        var panel = _canvas!.transform.FindNode("ContextMenu");

        var door = panel.FindNode("CtxDoor");
        Assert.NotNull(door, "у фасада должна быть кнопка открытия");
        Assert.IsTrue(door.gameObject.activeInHierarchy, "кнопка активна для фасада");
        Assert.AreEqual("Открыть", door.GetComponentInChildren<TMP_Text>(true).text);
    }

    [Test]
    public void Facade_HasModeDropdown_With18Options()
    {
        var facade = MakeFacade("F1");
        _menu!.Open(facade);
        var panel = _canvas!.transform.FindNode("ContextMenu");

        var mode = panel.FindNode("CtxMode");
        Assert.NotNull(mode, "у фасада должен быть список режимов");
        Assert.IsTrue(mode.gameObject.activeInHierarchy, "список активен для фасада");
        var dd = mode.GetComponent<TMP_Dropdown>();
        Assert.NotNull(dd, "CtxMode — это TMP_Dropdown");
        Assert.AreEqual(18, dd.options.Count, "18 режимов (12 рёбер + 6 ящиков)");
    }

    [Test]
    public void Board_DoorControls_Hidden()
    {
        var board = MakeBoard("B1");
        _menu!.Open(board);
        var panel = _canvas!.transform.FindNode("ContextMenu");

        foreach (var name in new[] { "CtxDoor", "CtxMode" })
        {
            var t = panel.FindNode(name);
            Assert.NotNull(t, $"{name} существует");
            Assert.IsFalse(t.gameObject.activeInHierarchy, $"{name} должна быть скрыта для детали");
        }
    }

    [Test]
    public void DoorButton_Click_TogglesFacadeAndLabel()
    {
        var facade = MakeFacade("F1");
        _menu!.Open(facade);
        var panel = _canvas!.transform.FindNode("ContextMenu");
        var door = panel.FindNode("CtxDoor");
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
        var panel = _canvas!.transform.FindNode("ContextMenu");
        var dd = panel.FindNode("CtxMode").GetComponent<TMP_Dropdown>();

        Assert.AreEqual(DoorMode.HingeFrontLeft, facade.Mode);
        dd.value = (int)DoorMode.DrawerOut;
        Assert.AreEqual(DoorMode.DrawerOut, facade.Mode, "выбор в списке ставит режим фасада");
        dd.value = (int)DoorMode.HingeBackTop;
        Assert.AreEqual(DoorMode.HingeBackTop, facade.Mode);
    }
}
