using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Настоящая причина разомкнутых стыков трубопровода на сцене пользователя
/// (замер — <c>PipeGapSensorTests</c>, зазоры 1.9689 мм и 1.3698 мм): не порядок
/// вызовов в <c>ElementMover.FinishDrag</c> (он уже верный, см.
/// <c>ElementMoverSeatingOrderTests</c>), а <c>SceneRestorer.Restore</c>, который
/// при ЗАГРУЗКЕ проекта прогоняет <c>MmGrid.Snap</c> по каждой детали НЕЗАВИСИМО и
/// ни разу не переспрашивает стыки труб — ровно тот же баг задачи A, но на другом
/// вызывающем пути. У dn20-трубы сечение 27 мм — половина (13.5 мм) всегда дробная,
/// так что округление минимальной вершины меша почти для любой позиции сдвигает
/// трубу на 0.5 мм по каждой поперечной оси; для двух деталей это ~0.71-1.73 мм —
/// больше <c>PipeJoint.JoinToleranceMm</c> = 0.5 мм.
///
/// Починка (<c>PipeDocking.RepairAfterGridSnap</c>, вызывается из
/// <c>SceneRestorer.RepairAutoSeatedJointsAfterGridSnap</c> для каждого
/// <c>IAutoSeated</c> сразу после <c>SnapElementEdgesToMillimetreGrid</c>) — тот же
/// «максимум связей» через <c>SnapPortDock.Best</c>, что и посадка при перетаскивании,
/// но с фиксированным малым допуском (<c>GridRepairMaxDistMm</c> = 2 мм), не связанным
/// с пользовательским <c>SnapThreshold</c>: это восстановление целостности данных, а
/// не интерактивный магнит, и обязано работать независимо от <c>SnapEnabled</c>.</summary>
public class ScenePipeJointGridRepairTests : SnapTestBase
{
    private const int PipeLengthMm = 600;

    [TearDown]
    public void ClearRegistry()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();
        ProjectInstructions.Reset();
        ProjectRooms.Reset();
        ProjectFloorplans.Reset();
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
    public void RepairJointAfterGridSnap_RestoresTheJointThatIndependentRoundingBroke()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = ElbowBroughtUpTo(pipe);
        var scene = new List<KitchenElement> { pipe, elbow };

        elbow.SeatAfterMove(scene);
        Assume.That(JoinedLinks(pipe, elbow), Is.EqualTo(1),
            "стенд обязан доказать сам себя: посадка закрыла стык");

        Assume.That(MmGrid.Snap(pipe), Is.True,
            "у dn20-трубы сечение всегда дробное — сетке обязано найтись что округлять");
        Assume.That(JoinedLinks(pipe, elbow), Is.EqualTo(0),
            "стенд обязан доказать сам себя: независимое округление рвёт стык — это и "
            + "есть баг, который чинит RepairJointAfterGridSnap");

        pipe.RepairJointAfterGridSnap(scene);

        Assert.AreEqual(1, JoinedLinks(pipe, elbow),
            "RepairJointAfterGridSnap обязан вернуть максимум связей после того, как "
            + "независимое округление их разорвало");
    }

    [Test]
    public void RepairJointAfterGridSnap_DoesNothingWhenNoJointWasEverClosed()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Isolated");
        var farAwayElbow = ElementFactory.CreatePipeElbow("FarElb",
            new Vector3(5f, 5f, 5f)).GetComponent<PipeFittingElement>();
        _spawned.Add(farAwayElbow.gameObject);
        var scene = new List<KitchenElement> { pipe, farAwayElbow };

        Assume.That(JoinedLinks(pipe, farAwayElbow), Is.EqualTo(0),
            "деталь за пределами GridRepairMaxDistMm не обязана быть соединена");

        pipe.RepairJointAfterGridSnap(scene);

        Assert.AreEqual(0, JoinedLinks(pipe, farAwayElbow),
            "RepairJointAfterGridSnap не обязан выдумывать связь там, где её никогда не было "
            + "— допуск 2 мм рассчитан на округление сетки, а не на магнит через всю сцену");
    }

    [Test]
    public void SceneRestorer_ReconnectsAJointThatItsOwnGridSnapWouldOtherwiseBreak()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = ElbowBroughtUpTo(pipe);
        var built = new List<KitchenElement> { pipe, elbow };
        elbow.SeatAfterMove(built);
        Assume.That(JoinedLinks(pipe, elbow), Is.EqualTo(1),
            "стенд обязан доказать сам себя: посадка закрыла стык до сохранения");

        var data = SaveLoadManager.CaptureScene(built);
        var json = SaveLoadManager.Serialize(data);

        ClearRegistry();
        _spawned.Clear();

        var loaded = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(loaded);
        var objs = SaveLoadManager.RestoreScene(loaded!);
        var restored = objs.Select(g => g.GetComponent<KitchenElement>())
            .Where(e => e != null).ToList()!;
        _spawned.AddRange(objs);

        var restoredPipe = restored.FirstOrDefault(e => e.PartName == "Run");
        var restoredElbow = restored.FirstOrDefault(e => e.PartName == "Elb");
        Assert.IsNotNull(restoredPipe, "труба обязана вернуться после load");
        Assert.IsNotNull(restoredElbow, "уголок обязан вернуться после load");

        Assert.AreEqual(1, JoinedLinks(restoredPipe!, restoredElbow!),
            "SceneRestorer.Restore округляет каждую деталь независимо "
            + "(SnapElementEdgesToMillimetreGrid) и обязан тут же переспросить стыки труб "
            + "(RepairAutoSeatedJointsAfterGridSnap) — иначе загрузка проекта САМА рвёт то, "
            + "что было закрыто на момент сохранения");
    }
}
