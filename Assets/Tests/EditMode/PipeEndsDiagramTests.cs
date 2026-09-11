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
    private ProjectLoadStateGuard? _globals;

    /// <summary>Панель строится ОДИН раз на класс: сборка контекстного меню — ~0,31 с,
    /// и одиннадцать сборок это 3,4 с из прогона EditMode. Почему это безопасно — в
    /// сводке <see cref="ContextMenuLayoutTests"/>: боевой сценарий и есть ОДНА панель,
    /// переоткрываемая через <c>Open</c>, и <c>Open</c> же её и сбрасывает.
    ///
    /// Два теста панель не открывают вовсе:
    /// <see cref="TheDiagram_KeepsItsWidgetNames"/> и
    /// <see cref="BothEnds_OfferTheSameList_EmptyItemFirstThenEveryFittingKind"/>
    /// читают то, что собрано в <c>Build</c>, — им общая панель ровно та же самая.
    ///
    /// Своего <c>SelectionManager</c> класс не заводит, и это НЕ упущение: в EditMode
    /// <c>Awake</c> не зовётся, <c>SelectionManager.Instance</c> остаётся пустым, и
    /// панель подписывается на пустоту и в старом виде тоже. Требование «менеджер живёт
    /// со сборки панели до <c>[OneTimeTearDown]</c>» относится к наборам, которые его
    /// создают (<see cref="MaterialPreviewTests"/>): создать его потестово при общей
    /// панели значило бы оставить панель подписанной на разрушенный объект.</summary>
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
    /// <c>ForgetLastApplyFrame</c> — окно склейки правок: в EditMode
    /// <c>Time.frameCount</c> стоит на месте, и окно, взведённое предыдущим тестом,
    /// съело бы первую правку следующего. <c>DisarmAll</c> снимает взвод кнопок
    /// удаления, а фокус — потому что <c>RefreshUnfocused</c> МОЛЧА пропускает
    /// сфокусированное поле, а <c>EventSystem</c> в EditMode один на весь прогон.</summary>
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
    /// обнуляет <c>_target</c> панели, снимает подсветку участков и гасит превью,
    /// иначе живая панель осталась бы с уничтоженной деталью в руках, а красный
    /// участок и призрак уехали бы в следующий тест.</summary>
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

    /// <summary>Наведение приходит через <c>PointerHover</c> — тот же приём, что и в
    /// схеме кромок: EditMode не шлёт указательских событий, и дёргается тот же
    /// делегат, который позовёт мышь.</summary>
    private static void Hover(GameObject go, bool enter)
    {
        var hover = go.GetComponent<PointerHover>();
        Assert.NotNull(hover, "торец схемы обязан реагировать на наведение");
        if (enter) hover!.Enter?.Invoke();
        else hover!.Exit?.Invoke();
    }

    /// <summary>Ставит превью чужими руками: показать призрака из выпадающего списка
    /// в EditMode нечем — список TMP разворачивается только живым указателем, — а
    /// проверить надо ГАШЕНИЕ, и оно не зависит от того, кто превью завёл.</summary>
    private static void SomePreview()
    {
        ScenePreview.Hover("тест", () => new GameObject("Ghost"), null);
        Assume.That(ScenePreview.IsShowing, Is.True);
    }

    [Test]
    public void HoveringAnEnd_PaintsThatEndOfThePipe_AndLeavingHidesIt()
    {
        var pipe = Pipe();
        _menu!.Open(pipe);

        Hover(Slot(1).gameObject, enter: true);
        Assert.IsTrue(PartHighlighter.IsShown(pipe, PartHighlighter.PipeEndRegion(PartEnd.End)),
            "наведение на торец схемы красит ЭТОТ конец трубы в сцене — с любого ракурса "
            + "видно, о каком конце речь");
        Assert.IsFalse(PartHighlighter.IsShown(pipe,
            PartHighlighter.PipeEndRegion(PartEnd.Start)), "и только этот");

        Hover(Slot(1).gameObject, enter: false);
        Assert.IsFalse(PartHighlighter.IsShown(pipe, PartHighlighter.PipeEndRegion(PartEnd.End)),
            "уход курсора снимает подсветку");
    }

    [Test]
    public void HoveringTheDropdownItself_PaintsTheSameEnd()
    {
        var pipe = Pipe();
        _menu!.Open(pipe);

        Hover(Choice(0).gameObject, enter: true);

        Assert.IsTrue(PartHighlighter.IsShown(pipe,
            PartHighlighter.PipeEndRegion(PartEnd.Start)),
            "список у торца и сам торец говорят об одном и том же участке трубы");
    }

    [Test]
    public void ClosingThePanel_TakesTheRedAndTheGhostWithIt()
    {
        var pipe = Pipe();
        _menu!.Open(pipe);
        Hover(Slot(0).gameObject, enter: true);
        SomePreview();

        _menu!.Close();

        Assert.IsFalse(PartHighlighter.IsShown(pipe,
            PartHighlighter.PipeEndRegion(PartEnd.Start)),
            "закрытая панель не оставляет красного участка на детали: PointerExit по "
            + "скрытому виджету уже не придёт");
        Assert.IsFalse(ScenePreview.IsShowing,
            "и не оставляет призрака — иначе в сцене навсегда зелёная деталь, "
            + "которой нет ни в реестре, ни в спецификации");
    }

    [Test]
    public void OpeningAnotherPipe_DropsWhatWasShownForThePreviousOne()
    {
        var first = Pipe();
        var second = Pipe();
        _menu!.Open(first);
        Hover(Slot(0).gameObject, enter: true);
        SomePreview();

        _menu!.Open(second);

        Assert.AreEqual(0, SideHighlighter.QuadCount,
            "подсветка принадлежала ПРЕДЫДУЩЕЙ трубе — смена выделения её снимает");
        Assert.IsFalse(ScenePreview.IsShowing, "и превью тоже");
    }

    [Test]
    public void ChoosingAKind_ClearsThePreview_AndPutsTheRealFittingOn()
    {
        var pipe = Pipe();
        _menu!.Open(pipe);
        Hover(Slot(1).gameObject, enter: true);
        SomePreview();

        Pick(1, OptionOf(PipeNodeKind.Cap));

        Assert.IsFalse(ScenePreview.IsShowing, "выбрал — тонировка исчезла");
        Assert.IsNull(ScenePreview.Ghost, "и призрак уничтожен, а не оставлен рядом с деталью");
        Assert.IsFalse(PartHighlighter.IsShown(pipe, PartHighlighter.PipeEndRegion(PartEnd.End)),
            "красный участок тоже снят: выбор закрыл список, курсору неоткуда его держать");
        Assert.IsNotNull(PipeEndFittings.NeighbourAt(pipe, 1, PartRegistry.GetAll()),
            "а деталь встала на конец по-настоящему");
    }
}
