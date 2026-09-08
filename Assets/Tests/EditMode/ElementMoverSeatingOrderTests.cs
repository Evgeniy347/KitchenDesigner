using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Задача A: почему порядок операций в <c>ElementMover.FinishDrag</c> важен.
///
/// Первая версия этого стенда округляла УГОЛОК после посадки и не смогла
/// воспроизвести разрыв (осталась 1 связь вместо ожидаемых 0) — замер на реальной
/// сцене пользователя (<c>PipeGapSensorTests</c>) показал, что настоящий разрыв
/// стыка живёт не в порядке вызовов <c>ElementMover.FinishDrag</c>, а в том, что
/// <c>SceneRestorer.Restore</c> округляет КАЖДУЮ деталь сцены по отдельности при
/// ЗАГРУЗКЕ проекта, ни разу не переспрашивая, остался ли стык закрыт (см.
/// <c>ScenePipeJointGridRepairTests</c> — там и диагноз, и починка).
///
/// Округление именно ТРУБЫ, а не фитинга, воспроизводит разрыв надёжно: сечение
/// dn20-трубы — 27 мм, половина (13.5 мм) ВСЕГДА дробная, так что
/// <c>MmGrid.Snap</c> почти для любой позиции сдвигает минимальную вершину меша на
/// 0.5 мм по каждой поперечной оси — совместно ~0.71 мм, что больше
/// <c>JoinToleranceMm</c> = 0.5 мм. У фитинга дробность зависит от его конкретной
/// геометрии и позиции и не гарантирована — потому первая версия и не увидела
/// разрыва.
///
/// Оба теста двигают ОДИН и тот же уголок-трубу ОДНИМ и тем же путём
/// (<c>IAutoSeated.SeatAfterMove</c>, как <c>PipeFittingDockSceneTests</c>) и
/// отличаются только порядком, в котором зовут <c>MmGrid.Snap</c> — это и есть
/// правило, которое соблюдает <c>ElementMover.FinishDrag</c>: сетка округляет
/// СНАЧАЛА, посадка устье-в-устье выполняется ПОСЛЕДНЕЙ.</summary>
public class ElementMoverSeatingOrderTests : SnapTestBase
{
    private const int PipeLengthMm = 600;

    [TearDown]
    public void ClearRegistry()
    {
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private static float Units(float mm) => mm * AppConstants.MM_TO_UNITS;

    private PipeElement PipeWithItsLowerEndAt(Vector3 end, string name)
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, PipeLengthMm, name,
            end + new Vector3(0f, Units(PipeLengthMm * 0.5f), 0f));
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeFittingElement ElbowBroughtUpTo(PipeElement pipe)
    {
        var go = ElementFactory.CreatePipeElbow("Elb", Vector3.zero);
        _spawned.Add(go);
        var elbow = go.GetComponent<PipeFittingElement>();
        elbow.transform.position += pipe.EndAUnits + new Vector3(0f, Units(-20f), 0f)
            - elbow.PortPositionUnits(0);
        return elbow;
    }

    private static int JoinedLinks(params KitchenElement[] scene) =>
        PipeNetwork.Build(new ScenePipeSnapshot(scene).Ports()).Links.Count;

    [Test]
    public void GridRoundingOfThePipeAfterSeating_BreaksTheJointItJustClosed()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = ElbowBroughtUpTo(pipe);

        elbow.SeatAfterMove(new List<KitchenElement> { pipe, elbow });
        Assume.That(JoinedLinks(pipe, elbow), Is.EqualTo(1),
            "стенд обязан доказать сам себя: посадка закрыла стык");

        bool rounded = MmGrid.Snap(pipe);
        Assume.That(rounded, Is.True,
            "у dn20-трубы сечение 27 мм — половина всегда дробная, сетке обязано "
            + "найтись что округлять, иначе стенд ничего не доказывает");

        Assert.AreEqual(0, JoinedLinks(pipe, elbow),
            "округление ПОСЛЕ посадки откатывает дробную деталь и рвёт только что "
            + "закрытый стык — тот же баг, что при загрузке проекта "
            + "(ScenePipeJointGridRepairTests) рвёт стыки пользователя");
    }

    [Test]
    public void GridRoundingOfThePipeBeforeSeating_LeavesTheJointClosed()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = ElbowBroughtUpTo(pipe);

        MmGrid.Snap(pipe);
        elbow.SeatAfterMove(new List<KitchenElement> { pipe, elbow });

        Assert.AreEqual(1, JoinedLinks(pipe, elbow),
            "та же труба, тот же порядок, что идёт в ElementMover.FinishDrag: "
            + "сетка округляет СНАЧАЛА, посадка — ПОСЛЕДНЕЙ, и ничего дальше её не откатывает");
    }
}
