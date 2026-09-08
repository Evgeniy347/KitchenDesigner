using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>Правило посадки новой детали на конец трубы, когда рядом есть ещё один
/// свободный порт (задача C): обязательный стык — с трубой, всё остальное — бонус.
/// Перебор идёт по (номер СВОЕГО порта × доворот на 90° вокруг оси стыка), и выбор
/// детерминирован: больше связей побеждает, при равенстве — младший порт, затем
/// младший доворот.
///
/// У отвода (Elbow) ноги стоят под прямым углом друг к другу (PipeFittingSpec.Legs
/// = Down, Right). Из этого следует геометрический факт, который и делает тест
/// падающим при поломке правила: какой бы порт ни сел на ВЕРТИКАЛЬНЫЙ стык с трубой,
/// вторая нога, доворачиваясь вокруг той же вертикали, остаётся ГОРИЗОНТАЛЬНОЙ —
/// вертикального соседа она не достанет НИ ПРИ ОДНОМ повороте. Оба теста опираются
/// на этот факт с противоположных сторон: один соседа кладёт туда, куда нога придёт,
/// другой — туда, куда она прийти не может.</summary>
public class PipeFittingSeatChoiceTests
{
    private const string Size = PipeSpec.DEFAULT_SIZE;
    private static readonly PointMm MandatoryMouth = new PointMm(0f, 0f, 0f);
    private static readonly PipeAxis MandatoryAxis = PipeAxis.Down;

    private static float Leg => PipeFittingSpec.LegLengthMm(Size);

    [Test]
    public void BestForNewFitting_Elbow_PicksThePortAndTwist_ThatReachesTheSideNeighbour()
    {
        var sideCandidate = new PipePort("Podacha", PipeNodeKind.Supply, 0,
            new PointMm(0f, -Leg, -Leg), PipeAxis.Forward);

        var choice = PipeFittingSeatChoice.BestForNewFitting(PipeNodeKind.Elbow, Size,
            MandatoryAxis, MandatoryMouth, new[] { sideCandidate });

        Assert.AreEqual(2, choice.LinkedPortCount,
            "у отвода два порта: обязательный стык с трубой и бонусный — со свободным "
            + "портом соседа, который стоит именно там, куда доворот на 90° приводит вторую ногу");
        Assert.AreEqual(0, choice.PortIndex);
        Assert.AreEqual(1, choice.TwistSteps);
    }

    [Test]
    public void BestForNewFitting_Elbow_CannotReachANeighbourOnTheSameVerticalAxis()
    {
        var verticalCandidate = new PipePort("Podacha", PipeNodeKind.Supply, 0,
            new PointMm(0f, -2f * Leg, 0f), PipeAxis.Up);

        var choice = PipeFittingSeatChoice.BestForNewFitting(PipeNodeKind.Elbow, Size,
            MandatoryAxis, MandatoryMouth, new[] { verticalCandidate });

        Assert.AreEqual(1, choice.LinkedPortCount,
            "бонуса нет ни при одном повороте: вторая нога отвода не бывает вертикальной, "
            + "пока вертикаль занята обязательным стыком, — остаётся только связь с трубой");
    }

    [Test]
    public void BestForNewFitting_NoBonusCandidates_StillSeatsOnPortZero_WithNoTwist()
    {
        var choice = PipeFittingSeatChoice.BestForNewFitting(PipeNodeKind.Elbow, Size,
            MandatoryAxis, MandatoryMouth, new PipePort[0]);

        Assert.AreEqual(1, choice.LinkedPortCount);
        Assert.AreEqual(0, choice.PortIndex,
            "при равенстве (везде только обязательная связь) побеждает МЛАДШИЙ порт");
        Assert.AreEqual(0, choice.TwistSteps,
            "и младший доворот — 0°, а не какой-то другой равнозначный вариант");
    }

    [Test]
    public void BestForNewFitting_Coupling_HasNoSecondLeg_SoNeverEarnsABonus()
    {
        var anyCandidate = new PipePort("Podacha", PipeNodeKind.Supply, 0,
            new PointMm(500f, 500f, 500f), PipeAxis.Left);

        var choice = PipeFittingSeatChoice.BestForNewFitting(PipeNodeKind.Coupling, Size,
            MandatoryAxis, MandatoryMouth, new[] { anyCandidate });

        Assert.AreEqual(1, choice.LinkedPortCount,
            "у муфты оба порта соосны: второй уходит на противоположный конец трубы и "
            + "бонусного соседа сбоку не достаёт никогда");
    }
}
