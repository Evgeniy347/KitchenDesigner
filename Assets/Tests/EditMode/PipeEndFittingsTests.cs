using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Деталь, выбранная в списке на конце трубы, обязана появиться уже
/// СОЕДИНЁННОЙ: устье в устье и с доворотом. «Создать рядом» здесь — дефект, а не
/// полумера: несоединённый порт немедленно зажигает PIP-01, и человек получает
/// ошибку в ответ на выбор из списка.
///
/// Посадка идёт через <c>PipeDocking.SeatPort</c> и <c>SnapPortDock.TurnOnto</c> —
/// те же правила, которыми садится перетаскиваемая деталь. Проверяется здесь то,
/// чего быстрый стенд проверить не может: что ось и угол из ядра, собранные в
/// поворот на стороне сцены (<c>Quaternion.AngleAxis</c> — ECall), действительно
/// сводят устья навстречу, и что вся правка — создание, замена, очистка — стоит
/// РОВНО ОДИН шаг отмены.
///
/// Имена элементов латинские: <c>ElementNaming.Rule</c> пропускает в PartName
/// только латиницу.</summary>
public class PipeEndFittingsTests : SnapTestBase
{
    private const int PipeLengthMm = 600;
    private const int LowerEnd = 0;
    private const int UpperEnd = 1;

    [TearDown]
    public void ClearScene()
    {
        CommandStack.Clear();
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

    private PipeEndEdit Choose(PipeElement pipe, int end, PipeNodeKind? kind)
    {
        var before = new List<KitchenElement>(PartRegistry.GetAll());
        var outcome = PipeEndFittings.Set(pipe, end, kind, PartRegistry.GetAll());
        foreach (var element in PartRegistry.GetAll())
            if (!before.Contains(element)) _spawned.Add(element.gameObject);
        return outcome;
    }

    private static PipeFittingElement? FittingOn(PipeElement pipe, int end) =>
        PipeEndFittings.NeighbourAt(pipe, end, PartRegistry.GetAll()) as PipeFittingElement;

    private static int JoinedLinks() =>
        PipeNetwork.Build(new ScenePipeSnapshot(PartRegistry.GetAll()).Ports()).Links.Count;

    private static int OpenEnds() =>
        PipeRules.Collect(new ScenePipeSnapshot(PartRegistry.GetAll()))
            .Count(f => f.Code == PipeIssueCatalog.CodeOpenEnd);

    private static float MouthGapMm(PipeFittingElement fitting, int port, Vector3 target) =>
        Mm(Vector3.Distance(fitting.PortPositionUnits(port), target));

    [Test]
    public void ChoosingACap_PutsItMouthToMouth_AndTheEndStopsBeingOpen()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        Assume.That(OpenEnds(), Is.EqualTo(2), "у голой трубы открыты оба конца");

        var outcome = Choose(pipe, UpperEnd, PipeNodeKind.Cap);

        Assert.AreEqual(PipeEndEdit.Changed, outcome);
        var cap = FittingOn(pipe, UpperEnd);
        Assert.IsNotNull(cap, "выбранная в списке заглушка обязана оказаться НА конце трубы, "
            + "а не где-то рядом: соседа ищут по совпадению устьев и встречным осям");
        Assert.LessOrEqual(MouthGapMm(cap!, 0, pipe.EndBUnits), Tolerance.ContactMm,
            "устье заглушки садится на торец трубы, а не рядом с ним");
        Assert.AreEqual(1, JoinedLinks(), "стык обязан закрыться: мало свести точки — "
            + "устья должны смотреть НАВСТРЕЧУ");
        Assert.AreEqual(1, OpenEnds(),
            "PIP-01 остаётся только на нижнем конце трубы; верхний закрыт. Если бы деталь "
            + "рождалась несоединённой, ошибок стало бы ТРИ, а не одна");
    }

    /// <summary>Единственное плечо заглушки смотрит ВВЕРХ, а верхний торец трубы —
    /// тоже вверх. Без доворота на 180° устья смотрели бы в одну сторону, точки
    /// совпали бы, а стыка бы не было — ровно та тихая половина операции, из-за
    /// которой «создано, но не соединено» выглядит как «создано».</summary>
    [Test]
    public void TheSeatedFitting_IsTurnedRound_NotOnlyMoved()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        Choose(pipe, UpperEnd, PipeNodeKind.Cap);

        var cap = FittingOn(pipe, UpperEnd);
        Assert.IsNotNull(cap);
        Assert.LessOrEqual(Vector3.Dot(cap!.PortDirection(0), pipe.RunAxis),
            -Tolerance.ParallelDot,
            "устье заглушки обязано смотреть НАВСТРЕЧУ торцу: без доворота оно смотрит "
            + "туда же, куда и торец");
    }

    [Test]
    public void TheElbowOnTheLowerEnd_ClosesTheJoint_WithItsFirstArm()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");

        Choose(pipe, LowerEnd, PipeNodeKind.Elbow);

        var elbow = FittingOn(pipe, LowerEnd);
        Assert.IsNotNull(elbow, "отвод садится нулевым плечом на нижний торец");
        Assert.LessOrEqual(MouthGapMm(elbow!, 0, pipe.EndAUnits), Tolerance.ContactMm);
        Assert.AreEqual(1, JoinedLinks());
        Assert.AreEqual(2, OpenEnds(),
            "свободны верхний конец трубы и второе плечо отвода — их закрывает "
            + "пользователь, а не этот выбор");
    }

    /// <summary>Привязка выключена в проекте — а деталь из списка всё равно обязана
    /// сесть. Выбор в списке это не перетаскивание: он не «подносит» деталь к
    /// порогу срабатывания, он НАЗНАЧАЕТ стык.</summary>
    [Test]
    public void TheFittingIsSeated_EvenWhenSnappingIsSwitchedOff()
    {
        KitchenSettings.Instance!.SnapEnabled = false;
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");

        Choose(pipe, UpperEnd, PipeNodeKind.Coupling);

        Assert.AreEqual(1, JoinedLinks(),
            "посадка по выбору не спрашивает KitchenSettings.SnapEnabled: иначе человек "
            + "с выключенным снэпом получал бы PIP-01 сразу после выбора из списка");
    }

    [Test]
    public void CreatingAFitting_IsOneUndoStep()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        int undoBefore = CommandStack.UndoCount;

        Choose(pipe, UpperEnd, PipeNodeKind.Cap);

        Assert.AreEqual(undoBefore + 1, CommandStack.UndoCount,
            "одна правка — один шаг отмены (docs/UI-GUIDELINES.md §2)");

        CommandStack.Undo();

        Assert.IsNull(FittingOn(pipe, UpperEnd), "отмена убирает созданную деталь");
        Assert.AreEqual(2, OpenEnds(), "и труба возвращается к двум открытым концам");
    }

    [Test]
    public void ClearingAnEnd_RemovesTheFitting_AndUndoBringsItBack()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        Choose(pipe, UpperEnd, PipeNodeKind.Cap);
        int undoAfterCreate = CommandStack.UndoCount;

        var cleared = Choose(pipe, UpperEnd, null);

        Assert.AreEqual(PipeEndEdit.Changed, cleared);
        Assert.IsNull(FittingOn(pipe, UpperEnd), "«нет» снимает деталь с конца");
        Assert.AreEqual(undoAfterCreate + 1, CommandStack.UndoCount,
            "удаление — тоже ровно один шаг");

        CommandStack.Undo();

        Assert.IsNotNull(FittingOn(pipe, UpperEnd),
            "отмена возвращает деталь на место — и соединённой");
        Assert.AreEqual(1, JoinedLinks());
    }

    [Test]
    public void ReplacingAFitting_SwapsItInOneUndoStep()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        Choose(pipe, UpperEnd, PipeNodeKind.Cap);
        int undoAfterCreate = CommandStack.UndoCount;

        Choose(pipe, UpperEnd, PipeNodeKind.Coupling);

        Assert.AreEqual(PipeNodeKind.Coupling, FittingOn(pipe, UpperEnd)!.NodeKind,
            "смена значения в списке ЗАМЕНЯЕТ деталь");
        Assert.AreEqual(1, JoinedLinks(), "новая деталь тоже соединена");
        Assert.AreEqual(undoAfterCreate + 1, CommandStack.UndoCount,
            "снять старую и поставить новую — одна правка, а не две");

        CommandStack.Undo();

        Assert.AreEqual(PipeNodeKind.Cap, FittingOn(pipe, UpperEnd)!.NodeKind,
            "одна отмена возвращает прежнюю деталь целиком");
    }

    [Test]
    public void ChoosingWhatAlreadyStandsThere_ChangesNothing()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        Choose(pipe, UpperEnd, PipeNodeKind.Cap);
        int undoAfterCreate = CommandStack.UndoCount;

        var again = Choose(pipe, UpperEnd, PipeNodeKind.Cap);

        Assert.AreEqual(PipeEndEdit.Unchanged, again);
        Assert.AreEqual(undoAfterCreate, CommandStack.UndoCount,
            "повторный выбор того же вида не обязан плодить шаги отмены: иначе Ctrl+Z "
            + "начнёт отменять то, чего пользователь не делал");
    }

    /// <summary>За фитингом на конце висит целая ветка — труба и заглушка на её дальнем
    /// конце. Правка больше не отказывает: она заменяет деталь ВСЕГДА, ветку не удаляет
    /// и не двигает, а связь, для которой у нового вида не нашлось порта, просто рвётся.
    /// Разбирать или воссоединять ветку — дело пользователя, а не этой правки.</summary>
    [Test]
    public void AnEndCarryingAWholeBranch_SwapsAnyway_AndTheBranchStaysWholeAndInPlace()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        Choose(pipe, UpperEnd, PipeNodeKind.Coupling);
        var coupling = FittingOn(pipe, UpperEnd)!;

        var branch = PipeWithItsLowerEndAt(coupling.PortPositionUnits(1), "Branch");
        Choose(branch, UpperEnd, PipeNodeKind.Cap);
        var branchCap = FittingOn(branch, UpperEnd)!;
        Assume.That(JoinedLinks(), Is.EqualTo(3),
            "стенд обязан доказать сам себя: за муфтой висит труба, а на её дальнем конце — заглушка");
        Vector3 branchPositionBefore = branch.transform.position;
        Vector3 branchCapPositionBefore = branchCap.transform.position;
        int undoBefore = CommandStack.UndoCount;

        var outcome = Choose(pipe, UpperEnd, PipeNodeKind.Cap);

        Assert.AreEqual(PipeEndEdit.Changed, outcome,
            "занятый веткой конец больше не блокирует замену");
        Assert.AreEqual(PipeNodeKind.Cap, FittingOn(pipe, UpperEnd)!.NodeKind,
            "деталь на редактируемом конце заменена");
        Assert.IsTrue(branch.gameObject.activeInHierarchy, "труба ветки не удалена");
        Assert.IsTrue(branchCap.gameObject.activeInHierarchy, "и заглушка на её дальнем конце тоже");
        Assert.AreEqual(branchPositionBefore, branch.transform.position,
            "труба ветки не сдвинута — рвётся только связь с муфтой, а не её положение");
        Assert.AreEqual(branchCapPositionBefore, branchCap.transform.position,
            "дальний конец ветки тем более не тронут");
        Assert.IsNull(PipeEndFittings.NeighbourAt(branch, LowerEnd, PartRegistry.GetAll()),
            "у заглушки один порт — второй порт муфты она не воспроизводит, связь ветки с "
            + "трубой рвётся: нижний конец ветки становится свободным");
        Assert.AreEqual(branchCap, FittingOn(branch, UpperEnd),
            "а дальняя связь ветки с её собственной заглушкой этой правки не касается");
        Assert.AreEqual(2, JoinedLinks(),
            "две связи на всю сцену: труба-заглушка (новая) и ветка-её собственная заглушка "
            + "(нетронутая) — связь муфты с веткой пропала, а не заменилась третьей");
        Assert.AreEqual(undoBefore + 1, CommandStack.UndoCount, "замена — один шаг отмены");

        CommandStack.Undo();

        Assert.AreEqual(PipeNodeKind.Coupling, FittingOn(pipe, UpperEnd)!.NodeKind,
            "одна отмена возвращает муфту");
        Assert.AreEqual(3, JoinedLinks(), "...и связь с веткой вместе с ней");
    }

    /// <summary>Муфта соединяет две обычные трубы. У заглушки один порт — она
    /// воспроизводит только связь с трубой, чей конец редактировали, вторая рвётся, а
    /// сосед остаётся на месте: ничего не удаляется и не двигается.</summary>
    [Test]
    public void CouplingToCap_SeversTheOtherLink_AndLeavesTheNeighbourInPlace()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        Choose(pipe, UpperEnd, PipeNodeKind.Coupling);
        var coupling = FittingOn(pipe, UpperEnd)!;

        var other = PipeWithItsLowerEndAt(coupling.PortPositionUnits(1), "Other");
        Assume.That(JoinedLinks(), Is.EqualTo(2));
        Vector3 otherPositionBefore = other.transform.position;

        var outcome = Choose(pipe, UpperEnd, PipeNodeKind.Cap);

        Assert.AreEqual(PipeEndEdit.Changed, outcome);
        Assert.AreEqual(PipeNodeKind.Cap, FittingOn(pipe, UpperEnd)!.NodeKind);
        Assert.AreEqual(1, JoinedLinks(), "муфта→заглушка: ровно одна связь рвётся");
        Assert.IsTrue(other.gameObject.activeInHierarchy);
        Assert.AreEqual(otherPositionBefore, other.transform.position,
            "сосед не двигается — он просто теряет соединение");
    }

    /// <summary>Муфта соединяет две обычные трубы. У тройника три порта — оба прежних
    /// воспроизводятся, третий остаётся свободным и загорается PIP-01.</summary>
    [Test]
    public void CouplingToTee_KeepsBothLinks_AndOpensTheSideBranch()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        Choose(pipe, UpperEnd, PipeNodeKind.Coupling);
        var coupling = FittingOn(pipe, UpperEnd)!;

        var above = PipeWithItsLowerEndAt(coupling.PortPositionUnits(1), "Above");
        Assume.That(JoinedLinks(), Is.EqualTo(2));
        int openBefore = OpenEnds();

        var outcome = Choose(pipe, UpperEnd, PipeNodeKind.Tee);

        Assert.AreEqual(PipeEndEdit.Changed, outcome);
        Assert.AreEqual(PipeNodeKind.Tee, FittingOn(pipe, UpperEnd)!.NodeKind);
        Assert.AreEqual(2, JoinedLinks(), "у тройника остаются ОБЕ прежние связи");
        Assert.IsTrue(above.gameObject.activeInHierarchy);
        Assert.AreEqual(openBefore + 1, OpenEnds(),
            "третий, боковой порт тройника свободен — это новый открытый конец");
    }

    /// <summary>Муфта соединяет две обычные трубы. У отвода второе плечо смотрит вбок,
    /// а не туда, откуда шла прежняя связь — сохранить обе можно было бы только сдвинув
    /// соседа, а замена соседей не двигает.</summary>
    [Test]
    public void CouplingToElbow_SeversTheTurnedLeg_AndLeavesTheNeighbourInPlace()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        Choose(pipe, UpperEnd, PipeNodeKind.Coupling);
        var coupling = FittingOn(pipe, UpperEnd)!;

        var above = PipeWithItsLowerEndAt(coupling.PortPositionUnits(1), "Above");
        Assume.That(JoinedLinks(), Is.EqualTo(2));
        Vector3 abovePositionBefore = above.transform.position;

        var outcome = Choose(pipe, UpperEnd, PipeNodeKind.Elbow);

        Assert.AreEqual(PipeEndEdit.Changed, outcome);
        Assert.AreEqual(PipeNodeKind.Elbow, FittingOn(pipe, UpperEnd)!.NodeKind);
        Assert.AreEqual(1, JoinedLinks(),
            "у отвода второе плечо смотрит не туда, где стояла труба Above — связь рвётся");
        Assert.IsTrue(above.gameObject.activeInHierarchy);
        Assert.AreEqual(abovePositionBefore, above.transform.position,
            "сосед остаётся ровно там, где стоял");
    }

    [Test]
    public void AnEndTakenByAnotherPipe_IsRefused_WithItsOwnAnswer()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var second = PipeWithItsLowerEndAt(pipe.EndBUnits, "Next");
        Assume.That(JoinedLinks(), Is.EqualTo(1), "две трубы состыкованы напрямую");

        var outcome = Choose(pipe, UpperEnd, PipeNodeKind.Cap);

        Assert.AreEqual(PipeEndEdit.OccupiedByOther, outcome,
            "на конце не фитинг, а другая труба — список её не описывает и подменять её "
            + "выбором из списка нельзя");
        Assert.IsTrue(second.gameObject.activeInHierarchy);
    }
}
