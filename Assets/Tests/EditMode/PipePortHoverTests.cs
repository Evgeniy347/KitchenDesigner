using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Core.UI;

/// <summary>Проводка наведения в панели портов: красный отвечает «где это»,
/// зелёный призрак — «что будет». Оба состояния ВИДА, и проверяется здесь ровно
/// то, что легко потерять проводкой.
///
/// Первое — «ушёл с пункта, и сцена вернулась как была». Возврат меряется не
/// глазами, а тремя счётчиками: реестр деталей (призрак родился в песочнице и в
/// него не попал), глубина отмены (наведение мышью не создаёт отменяемой
/// операции) и рендереры заменяемой детали (гасили порендерно — вернули ровно
/// то, что гасили).
///
/// Второе — красный и зелёный не спорят за один объект: красный лежит на
/// ХОЗЯИНЕ порта, гаснет на время превью СОСЕД. Разъедься это — накладка
/// увидела бы свой элемент погасшим и сняла бы подсветку сама
/// (<c>HighlightOverlay.Sync</c>).</summary>
public class PipePortHoverTests
{
    private const int PipeLengthMm = 600;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        ScenePreview.Leave();
        PartHighlighter.Hide();
        CommandStack.Clear();
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

    private PipeFittingElement Elbow()
    {
        var go = ElementFactory.CreatePipeElbow("Corner", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private PipeElement PipeWithItsUpperEndAt(Vector3 end)
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, PipeLengthMm, "Run",
            end - new Vector3(0f, PipeLengthMm * 0.5f * AppConstants.MM_TO_UNITS, 0f));
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private static PipePortHover<PipeElement> HoverOf(PipeElement pipe) =>
        new PipePortHover<PipeElement>(() => pipe, PipeEndsDiagram.Choices,
            (owner, end) => PartHighlighter.ShowPipeEnd(owner, PipeEndsDiagram.EndAt(end)));

    private static PipePortHover<PipeFittingElement> HoverOf(PipeFittingElement fitting) =>
        new PipePortHover<PipeFittingElement>(() => fitting, PipeFittingPortsDiagram.Choices,
            (owner, port) => PartHighlighter.ShowFittingMouth(owner, port));

    private static int OptionOf(PipeNodeKind kind) => PipeEndsDiagram.OptionOf(kind);

    private void Seat(PipeElement pipe, int end, PipeNodeKind kind)
    {
        var before = new List<KitchenElement>(PartRegistry.GetAll());
        PipeEndFittings.Set(pipe, end, kind, PartRegistry.GetAll());
        foreach (var element in PartRegistry.GetAll())
            if (!before.Contains(element)) _spawned.Add(element.gameObject);
    }

    private static int EnabledRenderers(KitchenElement element)
    {
        int enabled = 0;
        foreach (var renderer in ElementRenderers.BodyOf(element))
            if (renderer != null && renderer.enabled) enabled++;
        return enabled;
    }

    [Test]
    public void HoveringAnOption_ShowsTheCandidate_WithoutTouchingTheProject()
    {
        var pipe = Pipe();
        var hover = HoverOf(pipe);
        int parts = PartRegistry.GetAll().Count;
        int undo = CommandStack.UndoCount;

        hover.EnterOption(1, OptionOf(PipeNodeKind.Cap));

        Assert.IsTrue(ScenePreview.IsShowing, "наведение на пункт показывает кандидата");
        Assert.IsNotNull(ScenePreview.Ghost, "и это настоящий объект на месте будущей детали");
        Assert.AreEqual(parts, PartRegistry.GetAll().Count,
            "призрак рождён в песочнице и в реестр не попал — иначе его увидят "
            + "автосохранение, валидация и спецификация");
        Assert.AreEqual(undo, CommandStack.UndoCount,
            "наведение мышью не бывает отменяемой операцией");
    }

    [Test]
    public void LeavingTheOption_PutsTheSceneBack_ExactlyAsItWas()
    {
        var pipe = Pipe();
        Seat(pipe, 1, PipeNodeKind.Cap);
        var seated = PipeEndFittings.NeighbourAt(pipe, 1, PartRegistry.GetAll())!;
        var hover = HoverOf(pipe);
        int parts = PartRegistry.GetAll().Count;
        int undo = CommandStack.UndoCount;
        int lit = EnabledRenderers(seated);
        Assume.That(lit, Is.GreaterThan(0), "заглушка на конце видна до наведения");

        hover.EnterOption(1, OptionOf(PipeNodeKind.Elbow));
        Assume.That(EnabledRenderers(seated), Is.Zero,
            "то, что призрак заменит, на время превью гаснет");

        hover.Clear();

        Assert.IsFalse(ScenePreview.IsShowing, "ушёл с пункта — превью снято");
        Assert.IsNull(ScenePreview.Ghost, "призрак уничтожен, а не оставлен в сцене");
        Assert.AreEqual(lit, EnabledRenderers(seated),
            "погашенное вернулось: гасили порендерно — вернули ровно то, что гасили");
        Assert.AreEqual(parts, PartRegistry.GetAll().Count, "реестр деталей не изменился");
        Assert.AreEqual(undo, CommandStack.UndoCount, "и отменять нечего");
        Assert.IsFalse(PartHighlighter.IsShown(pipe, PartHighlighter.PipeEndRegion(PartEnd.End)),
            "красная подсветка гаснет вместе с превью");
    }

    [Test]
    public void RedPaintsTheOwner_WhileGreenReplacesTheNeighbour()
    {
        var pipe = Pipe();
        Seat(pipe, 1, PipeNodeKind.Cap);
        var hover = HoverOf(pipe);

        hover.EnterOption(1, OptionOf(PipeNodeKind.Elbow));

        Assert.IsTrue(PartHighlighter.IsShown(pipe, PartHighlighter.PipeEndRegion(PartEnd.End)),
            "красный лежит на ХОЗЯИНЕ порта, зелёный подменяет соседа: спорь они за один "
            + "объект, накладка увидела бы его погасшим и сняла бы подсветку сама");
        Assert.IsTrue(ScenePreview.IsShowing);
    }

    [Test]
    public void HoveringTheEmptyOption_ShowsNoGhost()
    {
        var pipe = Pipe();
        var hover = HoverOf(pipe);
        hover.EnterOption(1, OptionOf(PipeNodeKind.Cap));

        hover.EnterOption(1, PipeConnectionRule.NoChoice);

        Assert.IsFalse(ScenePreview.IsShowing,
            "«нет» — это снятие детали, показывать на конце нечего");
        Assert.IsTrue(PartHighlighter.IsShown(pipe, PartHighlighter.PipeEndRegion(PartEnd.End)),
            "но красный остаётся: вопрос «где это» никуда не делся");
    }

    [Test]
    public void HoveringTheControl_PaintsTheEnd_WithoutAnyGhost()
    {
        var pipe = Pipe();
        var hover = HoverOf(pipe);
        hover.EnterOption(0, OptionOf(PipeNodeKind.Cap));

        hover.Enter(0);

        Assert.IsTrue(PartHighlighter.IsShown(pipe, PartHighlighter.PipeEndRegion(PartEnd.Start)),
            "наведение на сам список красит конец трубы");
        Assert.IsFalse(ScenePreview.IsShowing,
            "но пункт не выбран — призраку взяться неоткуда");
    }

    [Test]
    public void NoChoiceOption_WithASeatedNeighbour_PreviewsRemovingIt()
    {
        var pipe = Pipe();
        Seat(pipe, 1, PipeNodeKind.Cap);
        var seated = PipeEndFittings.NeighbourAt(pipe, 1, PartRegistry.GetAll())!;
        var hover = HoverOf(pipe);
        int lit = EnabledRenderers(seated);
        Assume.That(lit, Is.GreaterThan(0), "заглушка на конце видна до наведения на «нет»");

        hover.EnterOption(1, PipeConnectionRule.NoChoice);

        Assert.IsTrue(ScenePreview.IsShowing,
            "«как будет, если выбрать „нет“» — это тоже показ, а не молчание");
        Assert.IsNull(ScenePreview.Ghost, "снятие детали не ставит на её место ничего нового");
        Assert.AreEqual(0, EnabledRenderers(seated),
            "заглушка, которую снимут, гаснет на время превью — иначе нечем "
            + "показать, что будет, если выбрать «нет»");

        hover.Clear();

        Assert.AreEqual(lit, EnabledRenderers(seated),
            "ушли с «нет» — заглушка обязана вернуться, как и любой другой призрак");
    }

    /// <summary>Предпросмотр не имеет права показывать то, чего правка не сделает.
    /// На порту фитинга сидит труба, выбор её не заменяет — значит и наведение не
    /// гасит трубу и не ставит на её место призрак: погашенная труба читалась бы как
    /// «сейчас исчезнет», а клик по пункту отказал бы. Один и тот же вопрос
    /// (<c>PipeEndFittings.HeldByAPipe</c>) задают и правка, и превью.</summary>
    [Test]
    public void HoveringAFittingOnAPortHeldByAPipe_ShowsNothing_AndLeavesThePipeLit()
    {
        var elbow = Elbow();
        var pipe = PipeWithItsUpperEndAt(elbow.PortPositionUnits(0));
        var hover = HoverOf(elbow);
        int lit = EnabledRenderers(pipe);
        Assume.That(lit, Is.GreaterThan(0), "труба на порту видна до наведения");

        hover.EnterOption(0,
            PipeEndsDiagram.OptionOf(PipeFittingPortsDiagram.Choices, PipeNodeKind.Cap));

        Assert.IsFalse(ScenePreview.IsShowing,
            "правка откажет — показывать «как будет» нечего");
        Assert.IsNull(ScenePreview.Ghost, "и призрака нет");
        Assert.AreEqual(lit, EnabledRenderers(pipe),
            "труба НЕ гаснет: превью удаления того, что не удалят, — обман");
        Assert.IsTrue(PartHighlighter.IsShown(elbow, PartHighlighter.FittingMouthRegion(0)),
            "но красный на порту остаётся: вопрос «где это» никуда не делся");
    }

    [Test]
    public void Sync_MovesTheGhost_WhenThePortsOwnerMovesUnderAnOpenPreview()
    {
        var pipe = Pipe();
        var hover = HoverOf(pipe);
        hover.EnterOption(1, OptionOf(PipeNodeKind.Elbow));
        var posBefore = ScenePreview.Ghost!.transform.position;

        pipe.transform.position += new Vector3(1f, 0f, 0f);
        HoverPreviewGate.Sync();

        Assert.AreNotEqual(posBefore, ScenePreview.Ghost!.transform.position,
            "хозяин порта уехал под открытым превью — без Sync призрак остался бы "
            + "стоять на старом месте вместо нового мундштука");
    }

    [Test]
    public void Commit_DropsThePreview_BeforeTheRealEditRuns()
    {
        var pipe = Pipe();
        var hover = HoverOf(pipe);
        hover.EnterOption(1, OptionOf(PipeNodeKind.Cap));

        hover.Commit();
        Seat(pipe, 1, PipeNodeKind.Cap);

        Assert.IsNull(ScenePreview.Ghost, "призрака больше нет");
        Assert.IsFalse(ScenePreview.IsShowing);
        Assert.IsNotNull(PipeEndFittings.NeighbourAt(pipe, 1, PartRegistry.GetAll()),
            "а настоящая деталь стоит на конце — её поставил CommandStack, не превью");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "и отменяется она одним Ctrl+Z, а не двумя: превью в историю не попало");
    }
}
