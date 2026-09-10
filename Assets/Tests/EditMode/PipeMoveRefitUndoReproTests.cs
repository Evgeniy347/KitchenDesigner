using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Два дефекта на пути ПЕРЕМЕЩЕНИЯ трубы, оба — про порядок операций в
/// <c>ElementMover.FinishDrag</c>, и оба видны пользователю.
///
/// Первый (отмена). Подгонка пролёта <c>PipeDocking.RefitRunAfterResize</c>
/// вызывалась ДО того, как снималось «было» для записи отмены, поэтому изменённая
/// длина попадала в запись как исходная, а <c>BuildMoveCommand</c> пишет только
/// позицию и поворот. Пользователь тянул трубу так, что оба конца попадали в
/// пределы досягаемости устьев, подгонка молча переписывала 118 → 119 мм, а Ctrl+Z
/// возвращал позицию и ОСТАВЛЯЛ длину: труба навсегда на миллиметр длиннее записи,
/// ведомость считала неверную длину реза, файл расходился с тем, что пользователь
/// считает отменённым. <c>ResizeHandleManager.FinishDrag</c> то же самое делает
/// правильно — читает размеры ПОСЛЕ подгонки.
///
/// Второй (порядок). Сразу после подгонки шли <c>SeatAfterMove</c> →
/// <c>PipeDocking.Seat</c> → <c>SnapPortDock.Best</c> с полным
/// <c>SnapThreshold</c> — много больше 2 мм досягаемости подгонки. Посадка двигает
/// ВСЮ трубу, чтобы прижать ОДНО устье, и тем самым сваливает весь остаток
/// округления на второй стык — ровно то, что
/// conventions/UNITS-AND-FILES.md → «Целые миллиметры не стыкуются с дробной
/// геометрией» запрещает: остаток делится между ОБОИМИ стыками. Подгонка обязана
/// быть последним геометрическим словом о трубе в жесте.</summary>
public class PipeMoveRefitUndoReproTests : SnapTestBase
{
    private const int PipeLengthMm = 118;
    private const float SpanMm = 118.6f;
    private const int RefittedLengthMm = 119;
    private const float ResiduePerJointMm = 0.2f;

    private GameObject? _moverGo;
    private ElementMover? _mover;

    protected override void OnSetup()
    {
        _moverGo = new GameObject("ElementMover сторожа");
        _mover = _moverGo.AddComponent<ElementMover>();
    }

    [TearDown]
    public void ClearRegistry()
    {
        if (_moverGo != null) Object.DestroyImmediate(_moverGo);
        _moverGo = null;
        _mover = null;
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
    }

    private static float Units(float mm) => mm * AppConstants.MM_TO_UNITS;

    private static float Mm(float units) => units / AppConstants.MM_TO_UNITS;

    private PipeFittingElement CouplingWithItsPortAt(string name, int portIndex, Vector3 world)
    {
        var go = ElementFactory.CreatePipeCoupling(name, Vector3.zero);
        _spawned.Add(go);
        var coupling = go.GetComponent<PipeFittingElement>();
        coupling.transform.position += world - coupling.PortPositionUnits(portIndex);
        return coupling;
    }

    private PipeElement PipeAt(string name, Vector3 centre)
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, PipeLengthMm, name, centre);
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeElement DragThePipeBetweenTwoMouths(out Vector3 startPosition,
        out PipeFittingElement low, out PipeFittingElement high)
    {
        Vector3 lowMouth = new Vector3(0f, Units(500f), 0f);
        low = CouplingWithItsPortAt("Nizhnyaya", 1, lowMouth);
        high = CouplingWithItsPortAt("Verhnyaya", 0,
            lowMouth + new Vector3(0f, Units(SpanMm), 0f));

        var pipe = PipeAt("Truba", new Vector3(Units(2000f), Units(500f), 0f));
        startPosition = pipe.transform.position;

        _mover!.BeginDragOn(pipe);
        pipe.transform.position += lowMouth + new Vector3(0f, Units(SpanMm * 0.5f), 0f)
            - 0.5f * (pipe.EndAUnits + pipe.EndBUnits);
        _mover!.FinishDragNow();

        return pipe;
    }

    private static float GapMm(PipeElement pipe, int end, PipeFittingElement fitting,
        int portIndex) =>
        Mm((pipe.SnapPortAt(end, pipe.transform.position).Position
            - fitting.PortPositionUnits(portIndex)).magnitude);

    /// <summary>Тест на возврат дефекта отмены: Ctrl+Z обязан вернуть И позицию,
    /// И длину. Первая половина — контроль, что подгонка вообще случилась: без неё
    /// «длина не изменилась» было бы неотличимо от «отмена работает».</summary>
    [Test]
    public void UndoingAPipeMove_RestoresTheLength_NotOnlyThePosition()
    {
        var pipe = DragThePipeBetweenTwoMouths(out Vector3 startPosition,
            out PipeFittingElement _, out PipeFittingElement _);

        Assert.AreEqual(RefittedLengthMm, pipe.LengthMM,
            "контроль стенда: подгонка пролёта обязана была переписать 118 → 119 мм, иначе "
            + "проверять отмену нечего");

        CommandStack.Undo();

        Assert.AreEqual(PipeLengthMm, pipe.LengthMM,
            "Ctrl+Z вернул позицию и ОСТАВИЛ длину: труба навсегда на миллиметр длиннее "
            + "записи, ведомость считает неверную длину реза, а файл расходится с тем, что "
            + "пользователь считает отменённым");
        Assert.AreEqual(startPosition.x, pipe.transform.position.x, Tol, "X вернулся");
        Assert.AreEqual(startPosition.y, pipe.transform.position.y, Tol, "Y вернулся");
        Assert.AreEqual(startPosition.z, pipe.transform.position.z, Tol, "Z вернулся");
    }

    /// <summary>Тест на возврат дефекта порядка: остаток округления делится между
    /// ОБОИМИ стыками. Когда посадка идёт ПОСЛЕ подгонки, она прижимает одно устье
    /// вплотную и сваливает весь остаток на второе.</summary>
    [Test]
    public void AfterMovingAPipeBetweenTwoMouths_TheRoundingResidue_IsSplitBetweenBothJoints()
    {
        var pipe = DragThePipeBetweenTwoMouths(out Vector3 _,
            out PipeFittingElement low, out PipeFittingElement high);

        float lowGapMm = GapMm(pipe, 0, low, 1);
        float highGapMm = GapMm(pipe, 1, high, 0);

        Assert.AreEqual(ResiduePerJointMm, lowGapMm, 0.05f,
            "нижний стык обязан получить ПОЛОВИНУ остатка округления (0,2 мм из 0,4), а не "
            + "ноль: ноль значит, что посадка прижала это устье вплотную ПОСЛЕ подгонки и "
            + "унесла весь остаток на второй стык — conventions/UNITS-AND-FILES.md → «Целые "
            + "миллиметры не стыкуются с дробной геометрией»");
        Assert.AreEqual(ResiduePerJointMm, highGapMm, 0.05f,
            "и верхний стык — ровно столько же; подгонка обязана быть последним "
            + "геометрическим словом о трубе в жесте");
    }

    /// <summary>Обе половины жеста вместе: пользователь видит закрытые стыки.</summary>
    [Test]
    public void AfterMovingAPipeBetweenTwoMouths_BothJointsAreClosed()
    {
        var pipe = DragThePipeBetweenTwoMouths(out Vector3 _,
            out PipeFittingElement low, out PipeFittingElement high);

        var scene = new List<KitchenElement> { low, high, pipe };
        Assert.AreEqual(2,
            PipeNetwork.Build(new ScenePipeSnapshot(scene).Ports()).Links.Count,
            "два стыка — и ни одного PIP-01 «открытый конец» на трассе, которую "
            + "пользователь только что собрал");
    }
}
