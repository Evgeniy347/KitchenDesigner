using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Секция кромок в панели свойств: схема сторон, цикл состояний по клику, подсветка
/// и поле толщины.
///
/// `BlockOnViolation` фикстура ГАСИТ, и это не удобство: одинокая деталь в пустой сцене сама
/// по себе нарушение (опереться не на что), а `ContextMenuUI.Apply` при нарушении с версии
/// «заблокированное применение откатывает всё» откатывает ВЕСЬ ввод, включая толщину кромки.
/// С включённой блокировкой тесты толщины проверяли бы не разбор числа, а факт отката —
/// `Thickness_WithComma_IsApplied` до этого зеленел лишь потому, что откат тогда возвращал
/// три поля из дюжины и кромку в сцене оставлял.</summary>
public class ContextMenuEdgeSectionTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _globals;

    /// <summary>Панель строится ОДИН раз на класс: сборка контекстного меню — 0,31 с,
    /// и десять сборок это 3,1 с из прогона EditMode при бюджете 170 с. Почему это
    /// безопасно — в сводке <see cref="ContextMenuLayoutTests"/>: боевой сценарий и есть
    /// ОДНА панель, переоткрываемая через <c>Open</c>, а <c>Open</c> и есть её сброс —
    /// в том числе <c>SideHighlighter.Hide()</c> и <c>_edges.Refresh()</c>, на которых
    /// стоит <see cref="OpeningAnotherElement_DropsHoverHighlightOfThePreviousOne"/>.
    ///
    /// Три теста панель не открывают: <see cref="EdgeDiagram_KeepsItsWidgetNames"/>
    /// читает имена узлов, собранные в <c>Build</c>, а оба теста про троттлинг —
    /// чистая арифметика <c>FrameThrottle</c> и панель не трогают вовсе.</summary>
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

    /// <summary>Панель переживает тест — значит потестовое состояние сбрасывается здесь.
    /// <c>ForgetLastApplyFrame</c> — окно склейки правок: <c>ApplyOncePerFrame</c>
    /// пропускает один Apply за кадр, а в EditMode <c>Time.frameCount</c> стоит на
    /// месте, поэтому окно, взведённое предыдущим тестом, съело бы первую правку
    /// следующего — то есть ровно оба теста про толщину. Фокус снимается по той же
    /// причине: <c>RefreshUnfocused</c> МОЛЧА пропускает сфокусированное поле, а
    /// <c>EventSystem</c> в EditMode один на весь прогон. <c>BlockOnViolation</c>
    /// (зачем — в сводке класса) возвращается не руками, а через
    /// <c>ProjectLoadStateGuard</c>: ручная пара сохраняет ровно то поле, о котором
    /// вспомнили.</summary>
    [SetUp]
    public void Setup()
    {
        _globals = ProjectLoadStateGuard.Capture();
        KitchenSettings.Instance.BlockOnViolation = false;
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
        ConfirmDeleteButton.DisarmAll();
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null) es.SetSelectedGameObject(null);
    }

    /// <summary><c>Close()</c> обязан идти ДО <c>DestroyImmediate</c> спавнов: он
    /// обнуляет <c>_target</c> панели, иначе живая панель осталась бы с уничтоженной
    /// деталью в руках, и он же снимает подсветку сторон и взвод кнопок удаления.</summary>
    [TearDown]
    public void Teardown()
    {
        SideHighlighter.Hide();
        CommandStack.Clear();
        if (_menu != null) _menu!.Close();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        _globals!.Restore();
    }

    private KitchenElement Board(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreatePart(dims, name, pos);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private Image Strip(string node) =>
        Panel().Find("CtxEdgeDiagram")!.Find(node)!.GetComponent<Image>();

    private TMP_InputField Thickness() =>
        Panel().Find("F_EdgeThickness")!.GetComponent<TMP_InputField>();

    private static void Hover(GameObject go, bool enter)
    {
        var hover = go.GetComponent<PointerHover>();
        Assert.NotNull(hover, "полоса схемы обязана реагировать на наведение");
        if (enter) hover!.Enter?.Invoke();
        else hover!.Exit?.Invoke();
    }

    [Test]
    public void EdgeDiagram_KeepsItsWidgetNames()
    {
        var diagram = Panel().Find("CtxEdgeDiagram");
        Assert.NotNull(diagram, "схема кромок ищется тестами и снапшотами по имени CtxEdgeDiagram");
        foreach (var node in new[] { "CtxEdgeL1", "CtxEdgeL2", "CtxEdgeW1", "CtxEdgeW2" })
            Assert.NotNull(diagram!.Find(node), $"полоса-торец {node} должна остаться на месте");
        Assert.NotNull(Panel().Find("F_EdgeThickness"),
            "поле толщины кромки ищется по имени F_EdgeThickness");
    }

    [Test]
    public void StripClick_CyclesThatSideThroughTheThreeStates()
    {
        var board = Board("Полка", new Vector3Int(600, 300, 18), Vector3.zero);
        _menu!.Open(board);
        Assume.That(board.SupportsEdges, Is.True, "лист 600x300x18 кромкуется");

        Strip("CtxEdgeW1").GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(EdgeSideState.Forced, board.EdgeStateOf(EdgeSide.W1),
            "порядок цикла: авто → есть → убрать → авто. Первый клик добавляет кромку — "
            + "именно это делал жёлтый бит до передела, и мышечная память у человека на него");
        Assert.AreEqual(EdgeSideState.Auto, board.EdgeStateOf(EdgeSide.W2),
            "соседние стороны клик не задевает");

        Strip("CtxEdgeW1").GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(EdgeSideState.Suppressed, board.EdgeStateOf(EdgeSide.W1),
            "второй клик — ЯВНОЕ «кромки нет»: красную сторону видит спецификация, 3D и "
            + "get_element, а EDG-01 по ней молчит");

        Strip("CtxEdgeW1").GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(EdgeSideState.Auto, board.EdgeStateOf(EdgeSide.W1),
            "три клика возвращают сторону туда, откуда начали");
    }

    [Test]
    public void StripClick_IsUndoable()
    {
        var board = Board("Полка", new Vector3Int(600, 300, 18), Vector3.zero);
        _menu!.Open(board);

        Strip("CtxEdgeL1").GetComponent<Button>().onClick.Invoke();
        Assume.That(board.EdgeStateOf(EdgeSide.L1), Is.Not.EqualTo(EdgeSideState.Auto));

        CommandStack.Undo();
        Assert.AreEqual(EdgeSideState.Auto, board.EdgeStateOf(EdgeSide.L1),
            "любая мутация кромки идёт через CommandStack (правило 2 UI-GUIDELINES)");
    }

    [Test]
    public void StripHover_ShowsThatSideOnTheBoard_AndExitHidesIt()
    {
        var board = Board("Полка", new Vector3Int(600, 300, 18), Vector3.zero);
        _menu!.Open(board);

        Hover(Strip("CtxEdgeW2").gameObject, enter: true);
        Assert.IsTrue(SideHighlighter.IsShown(board, EdgeSide.W2),
            "наведение на полосу подсвечивает сторону на самой детали — с любого ракурса "
            + "видно, о какой стороне речь");

        Hover(Strip("CtxEdgeW2").gameObject, enter: false);
        Assert.IsFalse(SideHighlighter.IsShown(board, EdgeSide.W2),
            "уход курсора снимает подсветку");
    }

    [Test]
    public void OpeningAnotherElement_DropsHoverHighlightOfThePreviousOne()
    {
        var first = Board("Первая", new Vector3Int(600, 300, 18), Vector3.zero);
        var second = Board("Вторая", new Vector3Int(800, 400, 18), new Vector3(2f, 0f, 0f));
        _menu!.Open(first);
        Hover(Strip("CtxEdgeL1").gameObject, enter: true);
        Assume.That(SideHighlighter.IsShown(first, EdgeSide.L1), Is.True);

        _menu!.Open(second);

        Assert.AreEqual(0, SideHighlighter.QuadCount,
            "подсветка принадлежала ПРЕДЫДУЩЕЙ детали: схема кромок под курсором "
            + "пересобирается, и PointerExit по старой полосе уже не придёт — снять её "
            + "обязан сам Open");
    }

    [Test]
    public void Close_DropsHoverHighlight()
    {
        var board = Board("Полка", new Vector3Int(600, 300, 18), Vector3.zero);
        _menu!.Open(board);
        Hover(Strip("CtxEdgeL2").gameObject, enter: true);
        Assume.That(SideHighlighter.QuadCount, Is.GreaterThan(0));

        _menu!.Close();

        Assert.AreEqual(0, SideHighlighter.QuadCount,
            "панель гаснет без PointerExit по полосе — иначе накладки остались бы висеть на детали");
    }

    [Test]
    public void Thickness_OutOfRange_IsRejectedAndBoardKeepsOldValue()
    {
        var board = Board("Полка", new Vector3Int(600, 300, 18), Vector3.zero);
        _menu!.Open(board);
        float before = board.EdgeThicknessMM;

        Thickness().text = "99";
        Thickness().onEndEdit.Invoke("99");

        Assert.AreEqual(before, board.EdgeThicknessMM, 1e-4f,
            "толщина вне EDGE_THICKNESS_MIN/MAX не клампится молча — она не принимается");
    }

    [Test]
    public void Thickness_WithComma_IsApplied()
    {
        var board = Board("Полка", new Vector3Int(600, 300, 18), Vector3.zero);
        _menu!.Open(board);

        Thickness().text = "0,8";
        Thickness().onEndEdit.Invoke("0,8");

        Assert.AreEqual(0.8f, board.EdgeThicknessMM, 1e-4f,
            "на русской раскладке толщина набирается через запятую, а формат вывода — через точку");
    }

    [Test]
    public void EdgeRecompute_IsThrottled_NotRunOnEveryFrame()
    {
        int period = ContextMenuEdgeSection.RecomputeEveryNFrames;
        var throttle = new FrameThrottle(period);

        int due = 0;
        for (int frame = 0; frame < period * 3; frame++)
            if (throttle.Due()) due++;

        Assert.AreEqual(3, due,
            "пересчёт кромок опрашивает ВСЮ сцену (EdgeBanding.Coverage по PartRegistry.GetAll), "
            + "поэтому в Update он идёт раз в RecomputeEveryNFrames кадров, а не каждый кадр: "
            + "соседи двигаются заметно медленнее 60 Гц");
    }

    [Test]
    public void EdgeRecompute_AfterExplicitRefresh_StartsTheWaitOver()
    {
        var throttle = new FrameThrottle(ContextMenuEdgeSection.RecomputeEveryNFrames);
        throttle.Due();
        throttle.Reset();

        Assert.IsFalse(throttle.Due(),
            "явный Refresh (открытие панели, правка кромки) уже пересчитал схему — "
            + "следующий кадр не обязан делать это снова");
    }
}
