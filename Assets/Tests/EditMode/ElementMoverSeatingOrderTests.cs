using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Задача A: почему порядок операций в <c>ElementMover.FinishDrag</c> важен.
///
/// <c>MmGrid.Snap</c> округляет координату МИНИМАЛЬНОЙ вершины меша до целого
/// миллиметра — разумно для мебели, но фитинги dn20 дробные по построению (Ø26.8,
/// корпус Ø33.5, нога 40.2 мм), так что округление почти всегда находит, что
/// сдвинуть. <c>JoinToleranceMm</c> = 0.5 мм: сдвиг на округление легко превышает
/// допуск и рвёт только что закрытый стык.
///
/// Оба теста двигают ОДИН и тот же уголок ОДНИМ и тем же путём
/// (<c>IAutoSeated.SeatAfterMove</c>, как <c>PipeFittingDockSceneTests</c>) и
/// отличаются только порядком, в котором зовут <c>MmGrid.Snap</c> — это и есть
/// правило, которое теперь соблюдает <c>ElementMover.FinishDrag</c>: сетка округляет
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
    public void GridRoundingAfterSeating_BreaksTheJointItJustClosed()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = ElbowBroughtUpTo(pipe);

        elbow.SeatAfterMove(new List<KitchenElement> { pipe, elbow });
        Assume.That(JoinedLinks(pipe, elbow), Is.EqualTo(1),
            "стенд обязан доказать сам себя: посадка закрыла стык");

        bool rounded = MmGrid.Snap(elbow);
        Assume.That(rounded, Is.True,
            "у dn20 дробная геометрия — сетке обязано найтись что округлять, иначе стенд "
            + "ничего не доказывает");

        Assert.AreEqual(0, JoinedLinks(pipe, elbow),
            "это и есть баг задачи A: округление ПОСЛЕ посадки откатывает дробную деталь "
            + "и рвёт только что закрытый стык");
    }

    [Test]
    public void GridRoundingBeforeSeating_LeavesTheJointClosed()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = ElbowBroughtUpTo(pipe);

        MmGrid.Snap(elbow);
        elbow.SeatAfterMove(new List<KitchenElement> { pipe, elbow });

        Assert.AreEqual(1, JoinedLinks(pipe, elbow),
            "тот же уголок, тот же порядок, что теперь идёт в ElementMover.FinishDrag: "
            + "сетка округляет СНАЧАЛА, посадка — ПОСЛЕДНЕЙ, и ничего дальше её не откатывает");
    }
}
