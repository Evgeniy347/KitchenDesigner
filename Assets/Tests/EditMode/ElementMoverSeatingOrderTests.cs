using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Задача A: почему порядок операций в <c>ElementMover.FinishDrag</c> важен —
/// и что из этого правила осталось верным после того, как труба перестала быть
/// подопечной мм-сетки.
///
/// Раньше здесь стоял тест, доказывавший, что округление ТРУБЫ ПОСЛЕ посадки рвёт
/// только что закрытый стык, а округление ДО посадки — нет. Первая версия стенда
/// округляла УГОЛОК после посадки и не смогла воспроизвести разрыв (осталась 1 связь
/// вместо ожидаемых 0) — замер на реальной сцене пользователя (<c>PipeGapSensorTests</c>)
/// показал, что настоящий разрыв стыка жил не в порядке вызовов
/// <c>ElementMover.FinishDrag</c>, а в том, что <c>SceneRestorer.Restore</c> округляла
/// КАЖДУЮ деталь сцены по отдельности при ЗАГРУЗКЕ проекта, ни разу не переспрашивая,
/// остался ли стык закрыт (см. <c>ScenePipeJointGridRepairTests</c> — там и диагноз, и
/// починка). Починкой стало то, что <c>MmGrid.OffsetToGrid</c> вовсе перестал трогать
/// детали с <c>ISnapPorts</c> (трубы и фитинги) — «округление ПОСЛЕ посадки» больше не
/// существует как операция, так что доказывать про её порядок больше нечего:
/// <see cref="PipeWithPorts_IsLeftAloneByTheGrid_UnlikeAnOrdinaryPart"/> утверждает это
/// напрямую, противоположным входом к обычной детали без устьев.
///
/// <see cref="GridRoundingOfThePipeBeforeSeating_LeavesTheJointClosed"/> остаётся:
/// он доказывает, что фактический порядок <c>ElementMover.FinishDrag</c> (сетка —
/// СНАЧАЛА, посадка устье-в-устье — ПОСЛЕДНЕЙ) не рвёт стык, даже когда для трубы
/// сетка — no-op.</summary>
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

    /// <summary>Действующее правило, с противоположным входом (agents/TEST-DESIGN.md →
    /// «Two questions need OPPOSITE inputs»): труба несёт <c>ISnapPorts</c> и мм-сетка обязана
    /// оставить её позицию нетронутой, а обычная деталь без устьев — как и раньше,
    /// сеткой двигается. Без второй половины пары «сетка ничего не делает» было бы
    /// неотличимо от «сетка сломана и не делает ничего вообще».</summary>
    [Test]
    public void PipeWithPorts_IsLeftAloneByTheGrid_UnlikeAnOrdinaryPart()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var before = pipe.transform.position;

        Assert.IsFalse(MmGrid.Snap(pipe),
            "у dn20-трубы сечение 27 мм — половина всегда дробная, так что мимо мм-сетки "
            + "она гарантированно стоит; MmGrid обязан её не заметить именно потому, что "
            + "несёт ISnapPorts, а не потому, что случайно уже на сетке");
        Assert.AreEqual(before, pipe.transform.position, "позицию трубы сетка не тронула");

        var ordinary = OrdinaryPartOffGrid();
        Assert.IsTrue(MmGrid.Snap(ordinary),
            "деталь без устьев мимо мм-сетки обязана сдвинуться — иначе OffsetToGrid "
            + "перестал бы округлять вообще всё, а не только детали с устьями");
    }

    private KitchenElement OrdinaryPartOffGrid()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = new Vector3(0f, 1.35f, 1.2005f);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = "Боковина";
        e.DimensionsMM = new Vector3Int(600, 2700, 16);
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
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
