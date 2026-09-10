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
        CommandStack.Clear();
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        foreach (var element in PartRegistry.GetAll())
            if (element != null) _spawned.Add(element.gameObject);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
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

    private static int OptionOf(PipeNodeKind kind) => PipeEndsDiagram.OptionOf(kind);

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
}
