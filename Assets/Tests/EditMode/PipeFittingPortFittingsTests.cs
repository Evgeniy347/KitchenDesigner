using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Пользователь попросил ту же схему концов, что уже есть у трубы, для ОСТАЛЬНЫХ
/// элементов трассы — то есть выбор детали из списка обязан работать не только когда
/// свободный порт стоит на трубе, но и когда он стоит на фитинге, и при ЛЮБОМ числе портов
/// (1 у заглушки, 2 у отвода, 3 у тройника), а не только у пары концов трубы.
///
/// <see cref="PipeEndFittings"/> раньше принимал именно <c>PipeElement</c> и жёстко проверял
/// границу индекса по <c>PipeNodePorts.CountOf(PipeNodeKind.Pipe)</c> — то есть «2» было
/// вписано текстом. Здесь эта труба выведена наружу как <c>KitchenElement</c>, а граница —
/// как фактическое число портов ДАННОЙ детали (<c>ISnapPorts.SnapPortCount</c>). Тест на
/// заглушке (у неё порт ровно один) — это и есть отрицательный вход, доказывающий, что
/// граница больше не «два»: попытка встать на несуществующий второй порт заглушки обязана
/// отказать так же тихо, как попытка выйти за границу любого другого массива, а не подставить
/// правило трубы по ошибке.</summary>
public class PipeFittingPortFittingsTests : SnapTestBase
{
    private const int PipeLengthMm = 600;
    private const int LowerEnd = 0;

    [TearDown]
    public void ClearScene()
    {
        CommandStack.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private static float Units(float mm) => mm * AppConstants.MM_TO_UNITS;

    private static float Mm(float units) => units / AppConstants.MM_TO_UNITS;

    private PipeFittingElement Elbow(Vector3 hubPosition, string name)
    {
        var go = ElementFactory.CreatePipeElbow(name, hubPosition);
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private PipeFittingElement Tee(Vector3 hubPosition, string name)
    {
        var go = ElementFactory.CreatePipeTee(name, hubPosition);
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private PipeFittingElement Cap(Vector3 hubPosition, string name)
    {
        var go = ElementFactory.CreatePipeCap(name, hubPosition);
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private PipeElement PipeWithItsUpperEndAt(Vector3 end, string name)
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, PipeLengthMm, name,
            end - new Vector3(0f, Units(PipeLengthMm * 0.5f), 0f));
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeEndEdit Choose(KitchenElement owner, int port, PipeNodeKind? kind)
    {
        var before = new List<KitchenElement>(PartRegistry.GetAll());
        var outcome = PipeEndFittings.Set(owner, port, kind, PartRegistry.GetAll());
        foreach (var element in PartRegistry.GetAll())
            if (!before.Contains(element)) _spawned.Add(element.gameObject);
        return outcome;
    }

    private static PipeFittingElement? FittingAt(KitchenElement owner, int port) =>
        PipeEndFittings.NeighbourAt(owner, port, PartRegistry.GetAll()) as PipeFittingElement;

    private static int JoinedLinks() =>
        PipeNetwork.Build(new ScenePipeSnapshot(PartRegistry.GetAll()).Ports()).Links.Count;

    private static int OpenEnds() =>
        PipeRules.Collect(new ScenePipeSnapshot(PartRegistry.GetAll()))
            .Count(f => f.Code == PipeIssueCatalog.CodeOpenEnd);

    private static float MouthGapMm(PipeFittingElement fitting, int port, Vector3 target) =>
        Mm(Vector3.Distance(fitting.PortPositionUnits(port), target));

    [Test]
    public void AnElbowsFreePort_AcceptsAFitting_SeatedMouthToMouth()
    {
        var elbow = Elbow(Vector3.zero, "Corner");
        Assume.That(OpenEnds(), Is.EqualTo(2), "у голого отвода открыты оба порта");

        var outcome = Choose(elbow, 0, PipeNodeKind.Cap);

        Assert.AreEqual(PipeEndEdit.Changed, outcome);
        var cap = FittingAt(elbow, 0);
        Assert.IsNotNull(cap, "выбор из списка обязан посадить деталь именно на порт "
            + "фитинга — тот же приём, что и на конце трубы");
        Assert.LessOrEqual(MouthGapMm(cap!, 0, elbow.PortPositionUnits(0)), Tolerance.ContactMm);
        Assert.AreEqual(1, JoinedLinks());
        Assert.AreEqual(1, OpenEnds(), "второй порт отвода остаётся открытым — эту правку не касается");
    }

    [Test]
    public void ATeesThirdPort_AlsoAcceptsAFitting_TheBoundIsNotHardcodedToTwo()
    {
        var tee = Tee(Vector3.zero, "Branch");
        Assume.That(PipeEndFittings.PortCountOf(tee), Is.EqualTo(3),
            "у тройника три порта — стенд обязан доказать это сам");

        var outcome = Choose(tee, 2, PipeNodeKind.Supply);

        Assert.AreEqual(PipeEndEdit.Changed, outcome);
        var supply = FittingAt(tee, 2);
        Assert.IsNotNull(supply, "третий порт тройника — за пределами прежней границы «два» "
            + "трубы — обязан принимать деталь так же, как первые два");
        Assert.LessOrEqual(MouthGapMm(supply!, 0, tee.PortPositionUnits(2)), Tolerance.ContactMm);
        Assert.AreEqual(1, JoinedLinks());
    }

    [Test]
    public void AnElbowsFreePort_AcceptsAPipe_SeatedMouthToMouth()
    {
        var elbow = Elbow(Vector3.zero, "Corner");
        Assume.That(OpenEnds(), Is.EqualTo(2), "у голого отвода открыты оба порта");

        var outcome = Choose(elbow, 1, PipeNodeKind.Pipe);

        Assert.AreEqual(PipeEndEdit.Changed, outcome,
            "трубу можно подключить к любому фитингу — правило в PipeConnectionRule");
        var pipe = PipeEndFittings.NeighbourAt(elbow, 1, PartRegistry.GetAll()) as PipeElement;
        Assert.IsNotNull(pipe, "на порт отвода обязана сесть именно труба");
        Assert.AreEqual(1, JoinedLinks());
        Assert.AreEqual(PipeNodeKind.Pipe,
            PipeEndFittings.StateAt(elbow, 1, PartRegistry.GetAll()).Fitting,
            "схема портов обязана НАЗВАТЬ трубу, а не показывать порт пустым");
    }

    /// <summary>ФИТИНГ на порту заменяется выбором из списка — это и есть обратный
    /// вход к запрету ниже. Починка «трубу не сносить» не имеет права превратиться в
    /// «на занятом порту нельзя ничего»: замена детали на детали — нормальная правка
    /// и остаётся одним шагом отмены.</summary>
    [Test]
    public void APortHeldByAFitting_IsReplacedByTheChoice()
    {
        var elbow = Elbow(Vector3.zero, "Corner");
        Choose(elbow, 0, PipeNodeKind.Cap);
        var cap = FittingAt(elbow, 0)!;
        Assume.That(JoinedLinks(), Is.EqualTo(1), "заглушка сидит на нулевом порту отвода");
        int undoBefore = CommandStack.UndoCount;

        var outcome = Choose(elbow, 0, PipeNodeKind.Coupling);

        Assert.AreEqual(PipeEndEdit.Changed, outcome,
            "фитинг на порту заменяется — запрет касается ТОЛЬКО трубы");
        Assert.AreEqual(PipeNodeKind.Coupling, FittingAt(elbow, 0)!.NodeKind,
            "на порту стоит выбранная деталь");
        Assert.IsFalse(cap.gameObject.activeInHierarchy, "прежняя заглушка снята");
        Assert.AreEqual(1, JoinedLinks(), "новая деталь тоже соединена");
        Assert.AreEqual(undoBefore + 1, CommandStack.UndoCount,
            "снять старую и поставить новую — одна правка");
    }

    /// <summary>СТОРОЖ ДЕФЕКТА. На порту фитинга сидит труба, у трубы свой дальний
    /// стык — и выбор любой другой детали в списке этого порта выполнял
    /// <c>DeleteCommand</c> НА ВСЮ ТРУБУ: в панели это выглядело заменой фитинга, а в
    /// сцене двухметровая труба исчезала и её второй конец осиротевал.
    ///
    /// Отказ достижим именно здесь, и вот чем: <c>ScenePipeSurvey</c> выпускает порты
    /// и у труб, <c>PipeJoint.Connects</c> стык «труба—фитинг» ПРИНИМАЕТ (запрещена
    /// только пара труба—труба), поэтому соседом порта фитинга труба быть может — и
    /// бывает почти всегда. На конце самой трубы состояние недостижимо, и ветки там
    /// нет: <c>PipeConnectionRule.CanConnect(Pipe, Pipe)</c> ложно, соседом конца
    /// трубы труба не станет. Тот, кто сочтёт этот отказ мёртвым, обязан сначала
    /// уронить настоящий тест, а не свою <c>Assume</c>: здесь нет ни одной.</summary>
    [Test]
    public void APortHeldByAPipe_IsRefused_AndTheWholePipeStaysWhereItWas()
    {
        var elbow = Elbow(Vector3.zero, "Corner");
        var pipe = PipeWithItsUpperEndAt(elbow.PortPositionUnits(0), "Run");
        Choose(pipe, LowerEnd, PipeNodeKind.Cap);
        var farCap = FittingAt(pipe, LowerEnd)!;
        Assume.That(JoinedLinks(), Is.EqualTo(2),
            "труба сидит на порту отвода, а на её дальнем конце — своя заглушка");
        Vector3 pipeBefore = pipe.transform.position;
        int parts = PartRegistry.GetAll().Count;
        int undoBefore = CommandStack.UndoCount;

        var outcome = Choose(elbow, 0, PipeNodeKind.Cap);

        Assert.AreEqual(PipeEndEdit.OccupiedByPipe, outcome,
            "трубу выбор из списка не заменяет: её убирает пользователь сам");
        Assert.IsTrue(pipe.gameObject.activeInHierarchy, "труба НЕ удалена");
        Assert.IsTrue(farCap.gameObject.activeInHierarchy,
            "и её дальний стык не осиротел — он и был ценой этого дефекта");
        Assert.AreEqual(pipeBefore, pipe.transform.position, "труба не сдвинута");
        Assert.AreEqual(parts, PartRegistry.GetAll().Count,
            "сцена осталась как была: отказ не создаёт деталь и не удаляет её");
        Assert.AreEqual(2, JoinedLinks(), "оба стыка на месте");
        Assert.AreEqual(undoBefore, CommandStack.UndoCount,
            "отказ не пишется в историю — отменять нечего");
        Assert.AreEqual(PipeNodeKind.Pipe,
            PipeEndFittings.StateAt(elbow, 0, PartRegistry.GetAll()).Fitting,
            "и схема по-прежнему называет на этом порту трубу");
    }

    /// <summary>«Нет» — тот же выбор из того же списка, и трубу он тоже не снимает:
    /// иначе запрет обходился бы одним пунктом выше остальных.</summary>
    [Test]
    public void TheNoneOptionOnAPortHeldByAPipe_IsRefusedTheSameWay()
    {
        var elbow = Elbow(Vector3.zero, "Corner");
        var pipe = PipeWithItsUpperEndAt(elbow.PortPositionUnits(0), "Run");
        Assume.That(JoinedLinks(), Is.EqualTo(1), "труба состыкована с портом отвода напрямую");
        int undoBefore = CommandStack.UndoCount;

        var outcome = Choose(elbow, 0, null);

        Assert.AreEqual(PipeEndEdit.OccupiedByPipe, outcome,
            "«нет» на занятом трубой порту отказывает так же, как выбор детали");
        Assert.IsTrue(pipe.gameObject.activeInHierarchy, "труба НЕ удалена");
        Assert.AreEqual(1, JoinedLinks(), "стык не разорван");
        Assert.AreEqual(undoBefore, CommandStack.UndoCount, "и в историю ничего не легло");
    }

    /// <summary>Труба на порту, и в списке выбрана ТРУБА — менять нечего. Это не
    /// отказ: пользователь просит то, что уже стоит.</summary>
    [Test]
    public void ChoosingAPipeOnAPortAlreadyHoldingOne_ChangesNothing()
    {
        var elbow = Elbow(Vector3.zero, "Corner");
        PipeWithItsUpperEndAt(elbow.PortPositionUnits(0), "Run");
        Assume.That(JoinedLinks(), Is.EqualTo(1));

        var outcome = Choose(elbow, 0, PipeNodeKind.Pipe);

        Assert.AreEqual(PipeEndEdit.Unchanged, outcome,
            "повторный выбор того, что уже стоит, не отказ и не правка");
    }

    [Test]
    public void APortIndexBeyondTheCapsOwnCount_IsRejected_NotTreatedAsAPipesTwo()
    {
        var cap = Cap(Vector3.zero, "Deadend");
        Assume.That(PipeEndFittings.PortCountOf(cap), Is.EqualTo(1),
            "у заглушки ровно один порт — стенд обязан доказать это сам");

        var outcome = Choose(cap, 1, PipeNodeKind.Elbow);

        Assert.AreEqual(PipeEndEdit.Unchanged, outcome,
            "второго порта у заглушки не существует: если бы граница осталась «меньше "
            + "PipeNodePorts.CountOf(Pipe) == 2», этот вызов молча считался бы допустимым");
        Assert.IsNull(FittingAt(cap, 0), "и первый порт заглушки остался нетронутым");
    }

    [Test]
    public void ChoosingAFittingOnAFittingsPort_IsOneUndoStep()
    {
        var elbow = Elbow(Vector3.zero, "Corner");
        int undoBefore = CommandStack.UndoCount;

        Choose(elbow, 1, PipeNodeKind.Coupling);

        Assert.AreEqual(undoBefore + 1, CommandStack.UndoCount,
            "одна правка — один шаг отмены, как и на конце трубы");

        CommandStack.Undo();

        Assert.IsNull(FittingAt(elbow, 1), "отмена убирает деталь, посаженную на порт фитинга");
    }
}
