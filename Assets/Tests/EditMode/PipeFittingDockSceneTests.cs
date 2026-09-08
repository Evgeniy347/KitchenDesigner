using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Доворот на НАСТОЯЩИХ элементах: деталь, поднесённую любым устьем, отпускание
/// кнопки разворачивает нужной стороной и сажает на трубу.
///
/// Соседний <c>SnapPortDockTests</c> проверяет правило на голых устьях и без Unity — там
/// оно и живёт. Здесь проверяется то, чего быстрый стенд проверить не может: что ось и
/// угол из ядра, собранные в поворот на стороне сцены, действительно сводят устье с
/// устьем (<c>Quaternion.AngleAxis</c> — ECall, во второй сборке его нет), и что после
/// доворота отмена возвращает И позицию, И поворот.
///
/// Путь тот же, которым идёт <c>ElementMover.FinishDrag</c>: сначала
/// <c>IAutoSeated.SeatAfterMove</c> с курсором, потом <c>MoveCommand</c> с позой ДО и
/// ПОСЛЕ. Поворот без записи в команду — половина операции: отмена вернула бы деталь на
/// место развёрнутой.
///
/// Имена элементов ЛАТИНСКИЕ: <c>ElementNaming.Rule</c> пропускает в PartName только
/// латиницу.</summary>
public class PipeFittingDockSceneTests : SnapTestBase
{
    private const int PipeLengthMm = 600;

    [TearDown]
    public void ClearRegistry()
    {
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private static float Units(float mm) => mm * AppConstants.MM_TO_UNITS;

    private static float Mm(float units) => units / AppConstants.MM_TO_UNITS;

    private PipeElement PipeWithItsLowerEndAt(Vector3 end, string name)
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, PipeLengthMm, name,
            end + new Vector3(0f, Units(PipeLengthMm * 0.5f), 0f));
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeFittingElement Fitting(GameObject go, Quaternion rotation)
    {
        _spawned.Add(go);
        var fitting = go.GetComponent<PipeFittingElement>();
        fitting.transform.rotation = rotation;
        return fitting;
    }

    private static void PutMouthAt(PipeFittingElement fitting, int port, Vector3 where) =>
        fitting.transform.position += where - fitting.PortPositionUnits(port);

    private static int NearestMouthTo(PipeFittingElement fitting, Vector3 target)
    {
        int best = -1;
        float bestGap = float.MaxValue;
        for (int i = 0; i < fitting.PortCount; i++)
        {
            float gap = Vector3.Distance(fitting.PortPositionUnits(i), target);
            if (gap >= bestGap) continue;
            bestGap = gap;
            best = i;
        }
        return best;
    }

    private static float NearestMouthGapMm(PipeFittingElement fitting, Vector3 target) =>
        Mm(Vector3.Distance(fitting.PortPositionUnits(NearestMouthTo(fitting, target)), target));

    private static int JoinedLinks(params KitchenElement[] scene) =>
        PipeNetwork.Build(new ScenePipeSnapshot(scene).Ports()).Links.Count;

    private static int OpenEnds(params KitchenElement[] scene) =>
        PipeRules.Collect(new ScenePipeSnapshot(scene))
            .Count(f => f.Code == PipeIssueCatalog.CodeOpenEnd);

    private static SnapCursor LookingStraightDownAt(Vector3 point) =>
        SnapCursor.AlongRay(point + new Vector3(0f, 1f, 0f), Vector3.down);

    [Test]
    public void Elbow_BroughtUpWithAMouthPointingAway_TurnsItselfAndClosesTheJoint()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = Fitting(ElementFactory.CreatePipeElbow("Elb", Vector3.zero),
            Quaternion.identity);
        PutMouthAt(elbow, 0, pipe.EndAUnits + new Vector3(0f, Units(-20f), 0f));

        Assert.AreEqual(0, JoinedLinks(pipe, elbow),
            "исходно стыка нет: нулевое плечо уголка смотрит ВНИЗ, прочь от торца трубы. "
            + "Это тот самый случай, который снэп не закрывал никогда — он умеет сводить "
            + "точки, но не разворачивать деталь");

        elbow.SeatAfterMove(new List<KitchenElement> { pipe, elbow });

        Assert.LessOrEqual(NearestMouthGapMm(elbow, pipe.EndAUnits), Tolerance.ContactMm,
            "после отпускания кнопки устье обязано сесть НА торец, а не рядом с ним: ось "
            + "и угол считает ядро, а собирает из них поворот сторона сцены, и разойтись "
            + "они могут молча");
        Assert.AreEqual(1, JoinedLinks(pipe, elbow),
            "и стык обязан закрыться: мало свести точки — устья должны смотреть "
            + "НАВСТРЕЧУ. Ровно этого не хватало: раньше пользователю приходилось "
            + "доворачивать уголок руками");
        Assert.AreEqual(2, OpenEnds(pipe, elbow),
            "свободны только верхний конец трубы и второе плечо уголка");
    }

    [Test]
    public void Elbow_BroughtUpWithItsOtherArmSideways_TurnsTheQuarterTurnAndClosesTheJoint()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = Fitting(ElementFactory.CreatePipeElbow("Elb", Vector3.zero),
            Quaternion.identity);
        PutMouthAt(elbow, 1, pipe.EndAUnits + new Vector3(0f, Units(-20f), 0f));

        Assert.AreEqual(1, NearestMouthTo(elbow, pipe.EndAUnits),
            "стенд обязан доказать сам себя: подносим ВТОРОЕ плечо, и оно же ближайшее");

        elbow.SeatAfterMove(new List<KitchenElement> { pipe, elbow });

        Assert.AreEqual(1, JoinedLinks(pipe, elbow),
            "второе плечо смотрит вбок — до торца ему четверть оборота, а не половина. "
            + "Приёмка звучит «поднесённый ЛЮБЫМ портом», а не «поднесённый тем портом и "
            + "под тем углом, для которых написан тест»");
        Assert.LessOrEqual(Mm(Vector3.Distance(elbow.PortPositionUnits(1), pipe.EndAUnits)),
            Tolerance.ContactMm,
            "и на торце оказалось именно второе плечо");
    }

    [Test]
    public void Tee_BroughtUpWithOneOfItsThreeMouths_DocksWithThatOne_NotWithAnother()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var tee = Fitting(ElementFactory.CreatePipeTee("Tee", Vector3.zero),
            Quaternion.identity);
        PutMouthAt(tee, 2, pipe.EndAUnits + new Vector3(0f, Units(-20f), 0f));

        int brought = NearestMouthTo(tee, pipe.EndAUnits);
        Assert.AreEqual(2, brought,
            "стенд обязан доказать сам себя раньше, чем что-то измерять: подносим "
            + "БОКОВОЕ устье тройника, и оно же ближайшее к торцу");

        tee.SeatAfterMove(new List<KitchenElement> { pipe, tee });

        Assert.LessOrEqual(Mm(Vector3.Distance(tee.PortPositionUnits(brought), pipe.EndAUnits)),
            Tolerance.ContactMm,
            "на торце обязано оказаться ИМЕННО то устье, которым тройник подносили. Любое "
            + "из трёх село бы «правильно» с точки зрения трассы, но развернуло бы деталь "
            + "не той ножкой — а ножки тройника не равноправны, ими и задают направление "
            + "разводки");
        Assert.AreEqual(1, JoinedLinks(pipe, tee));
    }

    [Test]
    public void TwoPipesInRange_TheElbowJoinsTheOneUnderTheCursor_NotTheOneWithTheSmallerGap()
    {
        var byGap = PipeWithItsLowerEndAt(Vector3.zero, "ByGap");
        var elbow = Fitting(ElementFactory.CreatePipeElbow("Elb", Vector3.zero),
            Quaternion.identity);
        PutMouthAt(elbow, 0, byGap.EndAUnits + new Vector3(0f, Units(-20f), 0f));

        var byCursor = PipeWithItsLowerEndAt(
            elbow.PortPositionUnits(0) + new Vector3(0f, 0f, Units(30f)), "ByCursor");

        var scene = new List<KitchenElement> { byGap, byCursor, elbow };
        elbow.SeatAfterMove(scene, LookingStraightDownAt(byCursor.EndAUnits));

        Assert.AreEqual(1, JoinedLinks(byCursor, elbow),
            "устье трубы ByGap в 20 мм, устье трубы ByCursor в 30 мм — по зазору "
            + "выигрывает первая. Луч курсора проходит через устье ВТОРОЙ, и садиться "
            + "деталь обязана на неё: указатель мыши — единственное, чем пользователь "
            + "говорит, к какой трубе он несёт фитинг");
        Assert.AreEqual(0, JoinedLinks(byGap, elbow),
            "и с ближайшей по зазору стыка при этом нет — иначе тест был бы зелен при "
            + "любом из двух правил и не различал бы их");
    }

    [Test]
    public void TheSameTwoPipesWithoutACursor_TheElbowFallsBackToTheSmallerGap()
    {
        var byGap = PipeWithItsLowerEndAt(Vector3.zero, "ByGap");
        var elbow = Fitting(ElementFactory.CreatePipeElbow("Elb", Vector3.zero),
            Quaternion.identity);
        PutMouthAt(elbow, 0, byGap.EndAUnits + new Vector3(0f, Units(-20f), 0f));

        var byCursor = PipeWithItsLowerEndAt(
            elbow.PortPositionUnits(0) + new Vector3(0f, 0f, Units(30f)), "ByCursor");

        elbow.SeatAfterMove(new List<KitchenElement> { byGap, byCursor, elbow });

        Assert.AreEqual(1, JoinedLinks(byGap, elbow),
            "сцена та же, что в тесте выше, а победитель ДРУГОЙ — этим два правила и "
            + "различаются. Без курсора (MCP, восстановление сцены) правило обязано "
            + "отвечать по зазору, а не отказываться работать");
    }

    [Test]
    public void Undo_AfterASeatThatTurnedThePart_GivesBackBothThePositionAndTheRotation()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = Fitting(ElementFactory.CreatePipeElbow("Elb", Vector3.zero),
            Quaternion.identity);
        PutMouthAt(elbow, 0, pipe.EndAUnits + new Vector3(0f, Units(-20f), 0f));

        var positionBefore = elbow.transform.position;
        var rotationBefore = elbow.transform.rotation;

        elbow.SeatAfterMove(new List<KitchenElement> { pipe, elbow });

        Assert.Greater(Quaternion.Angle(rotationBefore, elbow.transform.rotation), 1f,
            "положительный контроль: посадка действительно ПОВЕРНУЛА деталь. Без него "
            + "тест про отмену зелен и на детали, которая никуда не поворачивалась");

        CommandStack.Execute(new MoveCommand(elbow, positionBefore, elbow.transform.position,
            rotationBefore, elbow.transform.rotation));
        CommandStack.Undo();

        Assert.AreEqual(0f, Vector3.Distance(positionBefore, elbow.transform.position), 1e-4f,
            "отмена возвращает позицию — это работало и раньше");
        Assert.AreEqual(0f, Quaternion.Angle(rotationBefore, elbow.transform.rotation), 0.05f,
            "и ПОВОРОТ: доворот, не доехавший до команды перемещения, — половина "
            + "операции. Отмена вернула бы деталь на старое место уже развёрнутой, и "
            + "восстановить исходную позу было бы нечем");
    }

    [Test]
    public void SnapDiagnose_WithoutACursor_SaysSoInsteadOfPassingOffTheGapAnswerAsTheCursorOne()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = Fitting(ElementFactory.CreatePipeElbow("Elb", Vector3.zero),
            Quaternion.identity);
        PutMouthAt(elbow, 0, pipe.EndAUnits + new Vector3(0f, Units(-20f), 0f));

        var blind = SnapSystem.Diagnose(elbow, new List<KitchenElement> { pipe },
            elbow.transform.position);

        Assert.IsTrue(blind.dockWins,
            "оракул обязан знать про доворот: без этого snap_diagnose скажет «устья не "
            + "встречные — деталь повёрнута?» про пару, которую посадка развернёт сама. "
            + "AGENTS.md → «A rule added to candidate SELECTION must reach Diagnose in "
            + "the same commit»");
        Assert.AreEqual("Run", blind.dockTarget);
        Assert.Greater(blind.dockRotationDegrees, 1f,
            "и называет УГОЛ, а не только цель: агенту важно, что деталь довернётся");
        Assert.IsFalse(blind.cursorKnown,
            "курсора у MCP нет — и отчёт обязан это сказать, а не молчать");
        StringAssert.Contains("курсор не передан", blind.neighbors[0].verdict,
            "правило отбора теперь зависит от указателя мыши, которого у агента нет. "
            + "Оракул, отвечающий по зазору и выдающий это за ответ по курсору, — ровно "
            + "тот «уверенный неправильный ответ», который однажды уже стоил 78 ложных "
            + "находок");

        var sighted = SnapSystem.Diagnose(elbow, new List<KitchenElement> { pipe },
            elbow.transform.position, null, 5, LookingStraightDownAt(pipe.EndAUnits));

        Assert.IsTrue(sighted.cursorKnown,
            "а с курсором — отвечает по курсору и говорит об этом");
        Assert.GreaterOrEqual(sighted.dockCursorDistanceMM, 0f,
            "и несёт расстояние от устья до луча: по нему видно, ПОЧЕМУ выбрана эта цель");
        StringAssert.Contains("от луча курсора", sighted.neighbors[0].verdict,
            "тот же вердикт, но уже без оговорки про отсутствующий курсор");
    }
}
