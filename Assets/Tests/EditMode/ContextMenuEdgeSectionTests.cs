using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ContextMenuEdgeSectionTests
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
        SideHighlighter.Hide();
        CommandStack.Clear();
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
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
