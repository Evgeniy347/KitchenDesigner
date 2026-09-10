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

    [Test]
    public void APortTakenByAPlainPipe_IsReplaceable_BecauseTheListDescribesAPipe()
    {
        var elbow = Elbow(Vector3.zero, "Corner");
        var pipe = PipeWithItsUpperEndAt(elbow.PortPositionUnits(0), "Run");
        Assume.That(JoinedLinks(), Is.EqualTo(1), "труба состыкована с портом отвода напрямую");
        Assume.That(PipeConnectionRule.ChoicesFor(PipeNodeKind.Elbow),
            Contains.Item(PipeNodeKind.Pipe), "у фитинга труба есть в списке");

        var outcome = Choose(elbow, 0, PipeNodeKind.Cap);

        Assert.AreEqual(PipeEndEdit.Changed, outcome,
            "к порту фитинга труба подключается наравне с фитингами, поэтому её можно "
            + "заменить выбором из того же списка");
        Assert.IsFalse(pipe.gameObject.activeInHierarchy, "прежняя труба снята с порта");
        Assert.IsNotNull(FittingAt(elbow, 0), "на порту теперь заглушка");
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
