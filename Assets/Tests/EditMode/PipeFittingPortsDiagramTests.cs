using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Core.UI;

/// <summary>Панель свойств фитинга обязана предложить ровно столько выпадающих списков,
/// сколько у выбранного фитинга портов — один у заглушки, два у отвода, три у тройника — и
/// число это читается из <c>PipeFittingSpec</c>, а не перечислено по видам вручную
/// (<c>PipeFittingPortsDiagram.PortVisibility</c> опирается на те же грани
/// <c>ElementFacet.PipeFitting(Second|Third)Port</c>, которыми уже размечены поля диаметров).
///
/// Проверяется здесь, как и в <c>PipeEndsDiagramTests</c>, не рисунок, а результат выбора:
/// что деталь появилась и СЕЛА на нужный порт устье в устье.</summary>
public class PipeFittingPortsDiagramTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _globals;

    /// <summary>Панель строится ОДИН раз на класс: сборка контекстного меню — ~0,31 с,
    /// и десять сборок это 3,1 с из прогона EditMode. Почему это безопасно — в сводке
    /// <see cref="ContextMenuLayoutTests"/>: боевой сценарий и есть ОДНА панель,
    /// переоткрываемая через <c>Open</c>, и через <c>Open</c> здесь проходит КАЖДЫЙ
    /// тест без исключений. Больше того, именно переоткрытие общей панели на другом
    /// виде фитинга и есть предмет
    /// <see cref="ReopeningThePanel_OnADifferentFittingKind_ShowsThatKindsOwnPortCount"/>:
    /// потестовая сборка проверяла там свежую панель, то есть половину сценария.
    ///
    /// Своего <c>SelectionManager</c> класс не заводит: в EditMode <c>Awake</c> не
    /// зовётся и <c>SelectionManager.Instance</c> пуст, так что подписка панели ни на
    /// что не указывает ни при общей панели, ни при потестовой.</summary>
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

    /// <summary>Панель переживает тест — значит потестовое состояние сбрасывается здесь:
    /// окно склейки правок (<c>ForgetLastApplyFrame</c>, в EditMode <c>Time.frameCount</c>
    /// стоит на месте), взвод кнопок удаления и фокус, который <c>RefreshUnfocused</c>
    /// МОЛЧА пропускает, а <c>EventSystem</c> в EditMode один на весь прогон.</summary>
    [SetUp]
    public void Setup()
    {
        _globals = ProjectLoadStateGuard.Capture();
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
        ConfirmDeleteButton.DisarmAll();
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null) es.SetSelectedGameObject(null);
    }

    /// <summary><c>Close()</c> обязан идти ДО <c>DestroyImmediate</c> спавнов: он
    /// обнуляет <c>_target</c> панели, снимает красное устье и гасит призрака —
    /// иначе всё это уехало бы в следующий тест на живой панели.</summary>
    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        if (_menu != null) _menu!.Close();
        foreach (var element in PartRegistry.GetAll())
            if (element != null) _spawned.Add(element.gameObject);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
        _globals!.Restore();
    }

    private PipeFittingElement Elbow()
    {
        var go = ElementFactory.CreatePipeElbow("Corner", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private PipeFittingElement Tee()
    {
        var go = ElementFactory.CreatePipeTee("Branch", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private PipeFittingElement Cap()
    {
        var go = ElementFactory.CreatePipeCap("Deadend", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private Transform Diagram() => Panel().Find("CtxFittingPortsDiagram")!;

    private TMP_Dropdown Choice(int port) =>
        Panel().Find("CtxFittingPortFitting" + port)!.GetComponent<TMP_Dropdown>();

    private bool ChoiceRowVisible(int port) => Choice(port).gameObject.activeInHierarchy;

    private Image Slot(int port) => Diagram().Find("CtxFittingPort" + port)!.GetComponent<Image>();

    private static int OptionOf(PipeNodeKind kind) =>
        PipeEndsDiagram.OptionOf(PipeFittingPortsDiagram.Choices, kind);

    private void Pick(int port, int option)
    {
        var before = new List<KitchenElement>(PartRegistry.GetAll());
        Choice(port).onValueChanged.Invoke(option);
        foreach (var element in PartRegistry.GetAll())
            if (!before.Contains(element)) _spawned.Add(element.gameObject);
    }

    [Test]
    public void ACapsPanel_ShowsExactlyOnePortRow()
    {
        _menu!.Open(Cap());

        Assert.IsTrue(ChoiceRowVisible(0), "у заглушки один порт — первая строка обязана быть видна");
        Assert.IsFalse(ChoiceRowVisible(1), "второго порта у заглушки нет — вторая строка обязана быть скрыта");
        Assert.IsFalse(ChoiceRowVisible(2));
    }

    [Test]
    public void AnElbowsPanel_ShowsExactlyTwoPortRows()
    {
        _menu!.Open(Elbow());

        Assert.IsTrue(ChoiceRowVisible(0));
        Assert.IsTrue(ChoiceRowVisible(1));
        Assert.IsFalse(ChoiceRowVisible(2), "у отвода нет третьего порта");
    }

    [Test]
    public void ATeesPanel_ShowsAllThreePortRows()
    {
        _menu!.Open(Tee());

        Assert.IsTrue(ChoiceRowVisible(0));
        Assert.IsTrue(ChoiceRowVisible(1));
        Assert.IsTrue(ChoiceRowVisible(2), "у тройника есть третий порт — его строка обязана быть видна");
    }

    [Test]
    public void AFreePortOnAFitting_ShowsTheEmptyItem_AndAnUnfilledOutline()
    {
        _menu!.Open(Elbow());

        Assert.AreEqual(0, Choice(1).value, "свободный порт отвода показывает «нет»");
        Assert.AreEqual(UIStyle.EdgeAbsent, Slot(1).color);
    }

    [Test]
    public void ChoosingAKind_OnAFittingsPort_CreatesTheFittingAttached()
    {
        var elbow = Elbow();
        _menu!.Open(elbow);

        Pick(1, OptionOf(PipeNodeKind.Cap));

        var seated = PipeEndFittings.NeighbourAt(elbow, 1, PartRegistry.GetAll());
        Assert.IsNotNull(seated, "выбор в списке ставит деталь на порт фитинга соединённой, "
            + "как и на конце трубы");
        Assert.AreEqual(OptionOf(PipeNodeKind.Cap), Choice(1).value);
        Assert.AreEqual(UIStyle.EdgePresent, Slot(1).color);
    }

    [Test]
    public void ChoosingAKind_OnATeesThirdPort_CreatesTheFittingAttached()
    {
        var tee = Tee();
        _menu!.Open(tee);

        Pick(2, OptionOf(PipeNodeKind.Supply));

        var seated = PipeEndFittings.NeighbourAt(tee, 2, PartRegistry.GetAll());
        Assert.IsNotNull(seated, "третий порт тройника обязан принимать выбор точно так же, "
            + "как первые два — граница не «два», она читается из данных фитинга");
    }

    [Test]
    public void ReopeningThePanel_OnADifferentFittingKind_ShowsThatKindsOwnPortCount()
    {
        var elbow = Elbow();
        _menu!.Open(elbow);
        Assume.That(ChoiceRowVisible(2), Is.False, "у отвода нет третьего порта");

        _menu!.Open(Tee());

        Assert.IsTrue(ChoiceRowVisible(2),
            "панель одна и та же на все виды фитингов — переоткрытие на тройнике обязано "
            + "показать его третий порт, а не унаследовать форму отвода");
    }

    private static void Hover(GameObject go, bool enter)
    {
        var hover = go.GetComponent<PointerHover>();
        Assert.NotNull(hover, "порт схемы обязан реагировать на наведение");
        if (enter) hover!.Enter?.Invoke();
        else hover!.Exit?.Invoke();
    }

    [Test]
    public void HoveringAnOccupiedPort_PaintsThatMouth_AndOnlyThatOne()
    {
        var tee = Tee();
        _menu!.Open(tee);
        Pick(1, OptionOf(PipeNodeKind.Cap));
        Assume.That(PipeEndFittings.NeighbourAt(tee, 1, PartRegistry.GetAll()), Is.Not.Null);

        Hover(Slot(1).gameObject, enter: true);

        Assert.IsTrue(PartHighlighter.IsShown(tee, PartHighlighter.FittingMouthRegion(1)),
            "занятый порт красится там же, где свободный, — на устье СВОЕЙ ноги: "
            + "красный отвечает «где это», а не «свободно ли это»");
        Assert.IsFalse(PartHighlighter.IsShown(tee, PartHighlighter.FittingMouthRegion(0)),
            "и соседнюю ногу не трогает");

        Hover(Slot(1).gameObject, enter: false);
        Assert.IsFalse(PartHighlighter.IsShown(tee, PartHighlighter.FittingMouthRegion(1)),
            "уход курсора снимает подсветку");
    }

    [Test]
    public void HoveringThePortDropdown_PaintsTheSameMouth()
    {
        var elbow = Elbow();
        _menu!.Open(elbow);

        Hover(Choice(0).gameObject, enter: true);

        Assert.IsTrue(PartHighlighter.IsShown(elbow, PartHighlighter.FittingMouthRegion(0)),
            "список порта и порт на схеме говорят об одной и той же ноге фитинга");
    }

    [Test]
    public void ClosingThePanel_TakesTheRedAndTheGhostWithIt()
    {
        var elbow = Elbow();
        _menu!.Open(elbow);
        Hover(Slot(0).gameObject, enter: true);
        ScenePreview.Hover("тест", () => new GameObject("Ghost"), null);
        Assume.That(ScenePreview.IsShowing, Is.True);

        _menu!.Close();

        Assert.IsFalse(PartHighlighter.IsShown(elbow, PartHighlighter.FittingMouthRegion(0)),
            "закрытая панель не оставляет красного устья на фитинге");
        Assert.IsFalse(ScenePreview.IsShowing, "и не оставляет призрака в сцене");
    }
}
