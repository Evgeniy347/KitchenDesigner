using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Прикреплённый фитинг, который тащат ВДОЛЬ трубы, меняет её длину, а не рвёт стык.
///
/// Кто ведущий, кто ведомый. Тащит пользователь фитинг — значит ведущий фитинг, а длина
/// трубы ведомая. Опасность здесь ровно та, о которой предупреждает CONVENTIONS.md →
/// «A derived value must never move the thing it was derived FROM»: длина трубы выводится
/// из позиции фитинга, а фитинг садится на устье трубы — замкни это неверно, и труба
/// поедет или задрожит. Поэтому вывод опирается не на живую трубу, а на ДВЕ величины,
/// замороженные в начале жеста: дальний торец (<c>PipeRunHold.FreeEndUnits</c>) и ось
/// (<c>AxisUnits</c>). Ни одну из них проход не переписывает, цикла нет — и это доказывает
/// не рассуждение, а <c>SecondPassOverAnUnchangedScene_ChangesNothing</c>.
///
/// Вторая половина той же развязки живёт в <c>ElementMover.FinishDrag</c>: трубам, которые
/// пошли за фитингом, запрещено участвовать в его посадке
/// (<c>SceneWithoutTheFollowingPipes</c>). Иначе отпускание кнопки утащило бы фитинг обратно
/// на торец трубы — то есть ведомое двинуло бы ведущего, и перемещение вдоль трубы было бы
/// невозможно в принципе. Здесь этот путь воспроизведён руками: сначала
/// <c>PipeRunFollow.Hold</c> в начале жеста, потом <c>FollowAll</c>, потом команда.
///
/// Инвариант для случая с двумя трубами — СУММА ДЛИН. Тело фитинга жёсткое, расстояние
/// между его устьями постоянно, дальние торцы обеих труб заморожены: сколько прибавилось
/// одной, столько отнялось у другой. Сумма сильнее пары равенств — она красная и тогда,
/// когда обе трубы поехали «правильно» в одну сторону.
///
/// Имена элементов ЛАТИНСКИЕ: <c>ElementNaming.Rule</c> пропускает в PartName только
/// латиницу.</summary>
public class PipeRunFollowSceneTests : SnapTestBase
{
    private const int PipeLengthMm = 600;

    [TearDown]
    public void ClearRegistry()
    {
        CommandStack.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private static float Units(float mm) => mm * AppConstants.MM_TO_UNITS;

    private static float Mm(float units) => units / AppConstants.MM_TO_UNITS;

    private PipeElement PipeWithItsLowerEndAt(Vector3 end, string name, int lengthMM)
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, lengthMM, name,
            end + new Vector3(0f, Units(lengthMM * 0.5f), 0f));
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeFittingElement Fitting(GameObject go)
    {
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private static void PutMouthAt(PipeFittingElement fitting, int port, Vector3 where) =>
        fitting.transform.position += where - fitting.PortPositionUnits(port);

    private static int JoinedLinks(params KitchenElement[] scene) =>
        PipeNetwork.Build(new ScenePipeSnapshot(scene).Ports()).Links.Count;

    private static int OpenEnds(params KitchenElement[] scene) =>
        PipeRules.Collect(new ScenePipeSnapshot(scene))
            .Count(f => f.Code == PipeIssueCatalog.CodeOpenEnd);

    private sealed class Run
    {
        public PipeElement Lower = null!;
        public PipeElement? Upper;
        public PipeFittingElement Fitting = null!;
        public List<KitchenElement> Scene = null!;
        public readonly List<PipeRunHold> Holds = new List<PipeRunHold>();

        public void Drag(Vector3 by)
        {
            Fitting.transform.position += by;
            PipeRunFollow.FollowAll(Fitting, Holds);
        }
    }

    private Run SeatedOnOnePipe(PipeNodeKind kind)
    {
        var run = new Run();
        run.Lower = PipeWithItsLowerEndAt(Vector3.zero, "Lower", PipeLengthMm);
        run.Fitting = Fitting(kind == PipeNodeKind.Tee
            ? ElementFactory.CreatePipeTee("Node", Vector3.zero)
            : ElementFactory.CreatePipeCoupling("Node", Vector3.zero));
        PutMouthAt(run.Fitting, 0, run.Lower.EndBUnits + new Vector3(0f, Units(20f), 0f));
        run.Scene = new List<KitchenElement> { run.Lower, run.Fitting };
        run.Fitting.SeatAfterMove(run.Scene);

        Assume.That(JoinedLinks(run.Lower, run.Fitting), Is.EqualTo(1),
            "стенд обязан доказать себя раньше, чем что-то измерять: фитинг действительно "
            + "сидит на верхнем торце трубы, иначе тест меряет пустоту");

        PipeRunFollow.Hold(run.Fitting, run.Scene, run.Holds);
        return run;
    }

    private Run SeatedBetweenTwoPipes(PipeNodeKind kind)
    {
        var run = SeatedOnOnePipe(kind);
        var upper = PipeWithItsLowerEndAt(run.Fitting.PortPositionUnits(1), "Upper",
            PipeLengthMm);
        run.Upper = upper;
        run.Scene.Add(upper);

        Assume.That(JoinedLinks(run.Lower, run.Fitting, upper), Is.EqualTo(2),
            "обе трубы обязаны быть подведены — инвариант про СУММУ длин на одной трубе "
            + "зелен всегда и ничего не различает");

        PipeRunFollow.Hold(run.Fitting, run.Scene, run.Holds);
        return run;
    }

    [Test]
    public void Coupling_DraggedUpAlongTheRun_LengthensThePipeAndLeavesItsFarEndWhereItWas()
    {
        var run = SeatedOnOnePipe(PipeNodeKind.Coupling);
        Assert.AreEqual(1, run.Holds.Count, "у муфты на одной трубе ровно один захват");

        run.Drag(new Vector3(0f, Units(100f), 0f));

        Assert.AreEqual(PipeLengthMm + 100, run.Lower.LengthMM,
            "муфту увели на 100 мм вдоль оси — труба обязана дорасти ровно на столько же");
        Assert.AreEqual(0f, Mm(Vector3.Distance(Vector3.zero, run.Lower.EndAUnits)), 0.5f,
            "и ВТОРОЙ конец трубы обязан остаться на месте: это и есть та замороженная "
            + "величина, из которой длина выведена. Поедь она — вывод питал бы сам себя");
    }

    [Test]
    public void Coupling_DraggedDownAlongTheRun_ShortensThePipe()
    {
        var run = SeatedOnOnePipe(PipeNodeKind.Coupling);

        run.Drag(new Vector3(0f, Units(-150f), 0f));

        Assert.AreEqual(PipeLengthMm - 150, run.Lower.LengthMM,
            "противоположный вход обязан дать противоположный ответ: тест, который умеет "
            + "только удлинять, зелен и на коде, который всегда прибавляет модуль");
        Assert.AreEqual(0f, Mm(Vector3.Distance(Vector3.zero, run.Lower.EndAUnits)), 0.5f);
    }

    [Test]
    public void Coupling_DraggedAlongTheRun_DoesNotOpenAJoint()
    {
        var run = SeatedOnOnePipe(PipeNodeKind.Coupling);
        int before = OpenEnds(run.Lower, run.Fitting);

        run.Drag(new Vector3(0f, Units(100f), 0f));

        Assert.AreEqual(1, JoinedLinks(run.Lower, run.Fitting),
            "стык обязан пережить перетаскивание: труба тянется за муфтой, а не остаётся "
            + "стоять с открытым торцом");
        Assert.AreEqual(before, OpenEnds(run.Lower, run.Fitting),
            "и PIP-01 не должен загораться по дороге — открытых концов не прибавилось");
    }

    [Test]
    public void CouplingBetweenTwoPipes_DraggedAlongTheRun_KeepsTheSumOfTheirLengths()
    {
        var run = SeatedBetweenTwoPipes(PipeNodeKind.Coupling);
        Assert.AreEqual(2, run.Holds.Count, "муфта держит обе трубы");
        int sumBefore = run.Lower.LengthMM + run.Upper!.LengthMM;

        run.Drag(new Vector3(0f, Units(120f), 0f));

        Assert.AreEqual(PipeLengthMm + 120, run.Lower.LengthMM,
            "нижняя удлиняется на пройденное муфтой расстояние");
        Assert.AreEqual(PipeLengthMm - 120, run.Upper!.LengthMM,
            "а верхняя ровно на столько же укорачивается");
        Assert.AreEqual(sumBefore, run.Lower.LengthMM + run.Upper!.LengthMM,
            "СУММА длин — вот несущий инвариант этого случая. Тело муфты жёсткое, оба "
            + "дальних торца заморожены, значит сумма не может измениться ни на "
            + "миллиметр. Два отдельных равенства зелены и тогда, когда обе трубы поехали "
            + "в одну сторону; сумма — нет");
        Assert.AreEqual(2, JoinedLinks(run.Lower, run.Fitting, run.Upper!),
            "и оба стыка целы");
    }

    [Test]
    public void TeeBetweenTwoPipes_DraggedAlongTheRun_KeepsTheSumOfTheirLengths()
    {
        var run = SeatedBetweenTwoPipes(PipeNodeKind.Tee);
        int sumBefore = run.Lower.LengthMM + run.Upper!.LengthMM;

        run.Drag(new Vector3(0f, Units(-90f), 0f));

        Assert.AreEqual(sumBefore, run.Lower.LengthMM + run.Upper!.LengthMM,
            "у тройника проходных устьев тоже два, и правило обязано быть одно на оба "
            + "типа: правило, написанное про муфту, молча не сработает на тройнике");
        Assert.AreEqual(PipeLengthMm - 90, run.Lower.LengthMM);
        Assert.AreEqual(PipeLengthMm + 90, run.Upper!.LengthMM);
    }

    [Test]
    public void Coupling_DraggedAcrossTheRunWithinTheBreakAway_SlidesAlongTheAxisInstead()
    {
        var run = SeatedOnOnePipe(PipeNodeKind.Coupling);
        float sidewaysMm = KitchenSettings.Instance.SnapThreshold - 20f;
        Assume.That(sidewaysMm, Is.GreaterThan(Tolerance.ContactMm),
            "поперечный увод обязан быть заметно больше допуска касания, иначе тест не "
            + "различает «фитинг вернули на ось» и «он и не уходил»");

        run.Drag(new Vector3(Units(sidewaysMm), Units(100f), 0f));

        Assert.AreEqual(PipeLengthMm + 100, run.Lower.LengthMM,
            "поперёк оси длины нет: в длину идёт только ПРОЕКЦИЯ смещения на ось трубы");
        Assert.AreEqual(0f, Mm(run.Fitting.PortPositionUnits(0).x), Tolerance.ContactMm,
            "а сам фитинг возвращён на ось — стык может существовать только на ней. Ось "
            + "и дальний торец заморожены в начале жеста, поэтому это НЕ труба двигает "
            + "фитинг: труба к этому моменту ещё ничего о себе не сообщила");
        Assert.AreEqual(1, JoinedLinks(run.Lower, run.Fitting), "стык цел");
    }

    [Test]
    public void Coupling_DraggedAcrossTheRunPastTheBreakAway_LetsGoAndLeavesThePipeAlone()
    {
        var run = SeatedOnOnePipe(PipeNodeKind.Coupling);
        float sidewaysMm = KitchenSettings.Instance.SnapThreshold + 20f;
        var pipeWas = run.Lower.transform.position;

        run.Fitting.transform.position += new Vector3(Units(sidewaysMm), Units(100f), 0f);
        int following = PipeRunFollow.FollowAll(run.Fitting, run.Holds);

        Assert.AreEqual(0, following,
            "за порогом прилипания захват отпускается: тащить трубу боком нельзя, не "
            + "сдвинув её дальний торец — а он держит следующий стык, и правка поехала бы "
            + "по всей разводке");
        Assert.AreEqual(PipeLengthMm, run.Lower.LengthMM, "труба не тронута");
        Assert.AreEqual(0f, Vector3.Distance(pipeWas, run.Lower.transform.position), 1e-4f);
        Assert.AreEqual(sidewaysMm, Mm(run.Fitting.PortPositionUnits(0).x), 0.5f,
            "и фитинг остался там, куда его увели: дальше им распоряжается обычный снэп");
    }

    [Test]
    public void Coupling_DraggedPastTheFarEndOfTheRun_StopsAtTheShortestPipeThereIs()
    {
        var run = SeatedOnOnePipe(PipeNodeKind.Coupling);

        run.Drag(new Vector3(0f, Units(-(PipeLengthMm + 200)), 0f));

        Assert.AreEqual(PipeElementSpec.MIN_LENGTH_MM, run.Lower.LengthMM,
            "длина не бывает отрицательной и не бывает меньше минимума. Число НЕ новое: "
            + "его держит PipeElementSpec.ClampLengthMM — тот же предел, что и у поля "
            + "длины в панели, иначе у одной величины стало бы два минимума");
    }

    [Test]
    public void SecondPassOverAnUnchangedScene_ChangesNothing()
    {
        var run = SeatedBetweenTwoPipes(PipeNodeKind.Coupling);
        run.Drag(new Vector3(0f, Units(120f), 0f));

        int lowerMm = run.Lower.LengthMM;
        int upperMm = run.Upper!.LengthMM;
        var lowerAt = run.Lower.transform.position;
        var upperAt = run.Upper!.transform.position;
        var fittingAt = run.Fitting.transform.position;
        var fittingTurn = run.Fitting.transform.rotation;

        PipeRunFollow.FollowAll(run.Fitting, run.Holds);
        run.Fitting.SeatAfterMove(run.Scene);
        PipeRunFollow.FollowAll(run.Fitting, run.Holds);

        Assert.AreEqual(lowerMm, run.Lower.LengthMM,
            "ВТОРОЙ ПРОХОД ПО НЕИЗМЕНЁННОЙ СЦЕНЕ ОБЯЗАН МЕНЯТЬ РОВНО НОЛЬ. Это дешёвый "
            + "датчик на автоколебание, и он здесь несущий: длина трубы выведена из позы "
            + "фитинга, а поза фитинга садится на устье трубы. Замкни это через живую "
            + "трубу вместо замороженного торца — и каждый проход будет отъедать по "
            + "полмиллиметра, а фитинг поползёт");
        Assert.AreEqual(upperMm, run.Upper!.LengthMM);
        Assert.AreEqual(0f, Vector3.Distance(lowerAt, run.Lower.transform.position), 1e-5f);
        Assert.AreEqual(0f, Vector3.Distance(upperAt, run.Upper!.transform.position), 1e-5f);
        Assert.AreEqual(0f, Vector3.Distance(fittingAt, run.Fitting.transform.position), 1e-5f,
            "и посадка не имеет права подвинуть ведущего: труба выведена из фитинга, "
            + "значит фитинг из трубы выводиться уже не может");
        Assert.AreEqual(0f, Quaternion.Angle(fittingTurn, run.Fitting.transform.rotation),
            0.01f);
    }

    [Test]
    public void Undo_AfterADragAlongTheRun_GivesBackTheFittingAndBothPipeLengthsAtOnce()
    {
        var run = SeatedBetweenTwoPipes(PipeNodeKind.Coupling);
        var fittingWas = run.Fitting.transform.position;
        var fittingTurnWas = run.Fitting.transform.rotation;
        var lowerWas = run.Holds[0];
        var upperWas = run.Holds[1];
        Assume.That(lowerWas.Pipe, Is.SameAs(run.Lower),
            "стенд обязан знать, какой захват чей: перепутанные местами трубы сделали бы "
            + "оба утверждения об отмене зелёными по симметрии сцены");
        Assume.That(upperWas.Pipe, Is.SameAs(run.Upper));

        run.Drag(new Vector3(0f, Units(120f), 0f));

        Assert.AreNotEqual(lowerWas.DimensionsBeforeMM, lowerWas.Pipe.DimensionsMM,
            "положительный контроль: перетаскивание действительно изменило длину. Без "
            + "него тест про отмену зелен и на коде, который ничего не менял");

        var cmds = new List<IUndoCommand>
        {
            new MoveCommand(run.Fitting, fittingWas, run.Fitting.transform.position,
                fittingTurnWas, run.Fitting.transform.rotation),
            Resize(lowerWas),
            Resize(upperWas),
        };
        CommandStack.Execute(new CompositeCommand("Move group", cmds));
        CommandStack.Undo();

        Assert.AreEqual(0f, Vector3.Distance(fittingWas, run.Fitting.transform.position), 1e-4f,
            "одна отмена возвращает и позицию фитинга…");
        Assert.AreEqual(PipeLengthMm, run.Lower.LengthMM, "…и длину первой трубы…");
        Assert.AreEqual(PipeLengthMm, run.Upper!.LengthMM, "…и длину второй");
        Assert.AreEqual(0f, Vector3.Distance(lowerWas.PositionBefore,
            run.Lower.transform.position), 1e-4f,
            "длины мало: труба при этом ещё и переезжала, чтобы дальний торец стоял на "
            + "месте. Отмена без позиции вернула бы длину и оставила трубу сдвинутой");
        Assert.AreEqual(0f, Vector3.Distance(upperWas.PositionBefore,
            run.Upper!.transform.position), 1e-4f);
    }

    private static IUndoCommand Resize(in PipeRunHold hold) => new ResizeCommand(hold.Pipe,
        hold.DimensionsBeforeMM, hold.Pipe.DimensionsMM, hold.PositionBefore,
        hold.Pipe.transform.position, hold.Pipe.transform.rotation,
        hold.Pipe.transform.rotation);
}
