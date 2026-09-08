using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Core.UI;

/// <summary>Схема концов трубы — тот же приём, что и схема кромок
/// (<c>ContextMenuEdgeSection</c>): статическая картинка из панелей, собранная
/// <c>UIFactory</c> и вставленная в раскладку одной строкой фасета. Отличие —
/// вместо клика по полосе под каждым торцом свой список деталей.
///
/// Стерегутся три вещи, которые ломаются молча. Имена узлов: по ним схему ищут
/// тесты и снимки панели. Состав списка: он обязан совпадать с реестром видов
/// (<c>PipeFittingNames.Kinds</c>) — таблица возможностей и её двойник в UI
/// расходятся, если их не сверять. И форма, а не только цвет: занятый торец
/// залит сплошь, свободный — пустой контур (docs/UI-GUIDELINES.md §10), иначе в
/// оттенках серого и при дальтонизме схема не читается вовсе.</summary>
public class PipeEndsDiagramTests
{
    private const int PipeLengthMm = 600;

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

    private PipeElement Pipe()
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, PipeLengthMm, "Run",
            Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private Transform Diagram() => Panel().Find("CtxPipeEndsDiagram")!;

    private TMP_Dropdown Choice(int end) =>
        Diagram().Find("CtxPipeEndFitting" + end)!.GetComponent<TMP_Dropdown>();

    private Image Slot(int end) =>
        Diagram().Find("CtxPipeEnd" + end)!.GetComponent<Image>();

    private Image Hole(int end) =>
        Slot(end).transform.Find("CtxPipeEnd" + end + "Hole")!.GetComponent<Image>();

    private static int OptionOf(PipeNodeKind kind) => PipeEndsDiagram.OptionOf(kind);

    /// <summary>Выбирает пункт списка так, как это делает мышь, и запоминает всё
    /// рождённое: снятую деталь <c>DeleteCommand</c> лишь гасит и снимает с реестра,
    /// так что уборка по реестру её уже не увидит и объект утёк бы в следующий тест.</summary>
    private void Pick(int end, int option)
    {
        var before = new List<KitchenElement>(PartRegistry.GetAll());
        Choice(end).onValueChanged.Invoke(option);
        foreach (var element in PartRegistry.GetAll())
            if (!before.Contains(element)) _spawned.Add(element.gameObject);
    }

    [Test]
    public void TheDiagram_KeepsItsWidgetNames()
    {
        Assert.NotNull(Panel().Find("CtxPipeEndsDiagram"),
            "схема концов трубы ищется тестами и снимками панели по имени CtxPipeEndsDiagram");
        for (int end = 0; end < PipeEndsDiagram.EndCount; end++)
        {
            Assert.NotNull(Diagram().Find("CtxPipeEnd" + end), "торец " + end);
            Assert.NotNull(Diagram().Find("CtxPipeEndFitting" + end), "список у торца " + end);
            Assert.NotNull(Diagram().Find("CtxPipeEndLbl" + end), "подпись у торца " + end);
        }
    }

    [Test]
    public void BothEnds_OfferTheSameList_EmptyItemFirstThenEveryFittingKind()
    {
        var expected = new List<string> { PipeEndsDiagram.NoFittingOption };
        foreach (var kind in PipeFittingNames.Kinds) expected.Add(PipeFittingNames.Title(kind));

        for (int end = 0; end < PipeEndsDiagram.EndCount; end++)
        {
            var options = Choice(end).options.ConvertAll(o => o.text);
            CollectionAssert.AreEqual(expected, options,
                "список у торца " + end + " обязан совпадать с реестром видов фитингов. "
                + "Появился новый вид — он появляется и здесь, иначе поставить его на конец "
                + "трубы можно будет только из каталога, а панель промолчит");
        }
    }

    [Test]
    public void AFreeEnd_ShowsTheEmptyItem_AndAnUnfilledOutline()
    {
        _menu!.Open(Pipe());

        Assert.AreEqual(0, Choice(1).value, "на свободном конце список показывает «нет»");
        Assert.AreEqual(UIStyle.EdgeAbsent, Slot(1).color);
        Assert.IsTrue(Hole(1).enabled,
            "свободный торец — ПУСТОЙ контур: цвет не бывает единственным носителем смысла "
            + "(docs/UI-GUIDELINES.md §10)");
    }

    [Test]
    public void ChoosingAKind_CreatesTheFittingAttached_AndTheSchemeShowsItFilled()
    {
        var pipe = Pipe();
        _menu!.Open(pipe);

        Pick(1, OptionOf(PipeNodeKind.Cap));

        var seated = PipeEndFittings.NeighbourAt(pipe, 1, PartRegistry.GetAll());
        Assert.IsNotNull(seated, "выбор в списке ставит деталь на конец трубы соединённой");
        Assert.AreEqual(OptionOf(PipeNodeKind.Cap), Choice(1).value,
            "и список показывает то, что теперь стоит на конце");
        Assert.AreEqual(UIStyle.EdgePresent, Slot(1).color);
        Assert.IsFalse(Hole(1).enabled,
            "занятый торец залит СПЛОШЬ — это и есть вторая, нецветовая половина сигнала");
    }

    [Test]
    public void ReopeningThePanel_ShowsWhatAlreadyStandsOnTheEnd()
    {
        var pipe = Pipe();
        _menu!.Open(pipe);
        Pick(0, OptionOf(PipeNodeKind.Elbow));

        _menu!.Open(pipe);

        Assert.AreEqual(OptionOf(PipeNodeKind.Elbow), Choice(0).value,
            "открытая заново панель читает сцену, а не помнит свой прошлый выбор");
        Assert.AreEqual(0, Choice(1).value, "второй конец так и остался свободным");
    }

    [Test]
    public void ChoosingTheEmptyItem_TakesTheFittingOff()
    {
        var pipe = Pipe();
        _menu!.Open(pipe);
        Pick(1, OptionOf(PipeNodeKind.Cap));

        Pick(1, 0);

        Assert.IsNull(PipeEndFittings.NeighbourAt(pipe, 1, PartRegistry.GetAll()),
            "«нет» снимает деталь с конца");
        Assert.AreEqual(0, Choice(1).value);
        Assert.IsTrue(Hole(1).enabled, "и торец снова читается как пустой контур");
    }
}
