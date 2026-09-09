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
        // MmGrid.OffsetToGrid больше не трогает элементы с устьями (ISnapPorts) вовсе —
        // это и есть настоящее лечение задачи A, так что MmGrid.Snap(pipe) сейчас всегда
        // no-op и разорвать стык этим путём уже нельзя. Но RepairJointAfterGridSnap
        // остаётся страховкой для ФАЙЛОВ, сохранённых ДО этого исправления — они несут
        // унаследованное independent-rounding смещение на полмиллиметра по каждой
        // поперечной оси. Имитируем ровно это смещение напрямую, не через MmGrid.
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = ElbowBroughtUpTo(pipe);
        var scene = new List<KitchenElement> { pipe, elbow };

        elbow.SeatAfterMove(scene);
        Assume.That(JoinedLinks(pipe, elbow), Is.EqualTo(1),
            "стенд обязан доказать сам себя: посадка закрыла стык");

        pipe.transform.position += new Vector3(Units(0.475f), Units(0.275f), Units(-0.5f));
        Assume.That(JoinedLinks(pipe, elbow), Is.EqualTo(0),
            "стенд обязан доказать сам себя: унаследованное смещение рвёт стык — это и "
            + "есть легаси-состояние, которое чинит RepairJointAfterGridSnap");

        pipe.RepairJointAfterGridSnap(scene);

        Assert.AreEqual(1, JoinedLinks(pipe, elbow),
            "RepairJointAfterGridSnap обязан вернуть максимум связей после того, как "
            + "унаследованное округление их разорвало");
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

    private static int FreeLegIndex(PipeFittingElement fitting, IReadOnlyList<KitchenElement> scene)
    {
        var survey = ScenePipeSurvey.Of(scene);
        for (int p = 0; p < survey.Ports.Count; p++)
        {
            if (!string.Equals(survey.Ports[p].ElementId, fitting.PartName,
                    System.StringComparison.Ordinal)) continue;
            if (survey.Network.IsFree(p)) return survey.Ports[p].PortIndex;
        }
        Assert.Fail($"{fitting.PartName}: свободная нога не найдена — стенд не может продолжать");
        return -1;
    }

    /// <summary>Отвод_91/92 задачи A из «Otvod_91»/«Otvod_92»: двуногий фитинг, у которого
    /// ОДНА нога уже сомкнута (с Podacha/Obratka), а ДРУГАЯ разомкнута унаследованным
    /// смещением на трубу. <c>SnapPortDock.Best</c> — единственное описание правила
    /// стыка (<c>SnapPortRuleSingleSourceTests</c>) — всегда предлагает элементу
    /// ГЛОБАЛЬНО ближайшую пару портов across всей сцены; уже сомкнутая нога всегда
    /// меряет ближе, чем просто открытая, так что <c>RepairJointAfterGridSnap</c> для
    /// такого фитинга — молчаливый no-op: он бесконечно перевыбирает уже закрытую ногу
    /// и никогда не пробует открытую.</summary>
    [Test]
    public void RepairAfterLoad_ClosesAnOpenJoint_EvenWhenTheSameFittingHasAnotherCloserClosedJoint()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = ElbowBroughtUpTo(pipe);
        var built = new List<KitchenElement> { pipe, elbow };
        elbow.SeatAfterMove(built);
        Assume.That(JoinedLinks(pipe, elbow), Is.EqualTo(1),
            "стенд обязан доказать сам себя: элбоу сел на трубу");

        int freeIdx = FreeLegIndex(elbow, built);

        elbow.transform.position += new Vector3(Units(0.6f), 0f, 0f);
        Assume.That(JoinedLinks(pipe, elbow), Is.EqualTo(0),
            "стенд обязан доказать сам себя: сдвиг элбоу на 0.6 мм рвёт стык труба-фитинг "
            + "(допуск PipeJoint.JoinToleranceMm = 0.5 мм)");

        var capGo = ElementFactory.CreatePipeCap("Cap", Vector3.zero);
        _spawned.Add(capGo);
        var cap = capGo.GetComponent<PipeFittingElement>();
        cap.transform.position += elbow.PortPositionUnits(freeIdx)
            + elbow.PortDirection(freeIdx) * Units(20f) - cap.PortPositionUnits(0);

        var scene = new List<KitchenElement> { pipe, elbow, cap };
        cap.SeatAfterMove(scene);
        Assume.That(JoinedLinks(elbow, cap), Is.EqualTo(1),
            "стенд обязан доказать сам себя: заглушка села ровно на свободную ногу элбоу");
        Assume.That(JoinedLinks(pipe, elbow), Is.EqualTo(0),
            "стенд обязан доказать сам себя: труба-элбоу стык остаётся разомкнут после "
            + "посадки заглушки на другую ногу");

        var data = SaveLoadManager.CaptureScene(scene);
        var json = SaveLoadManager.Serialize(data);

        ClearRegistry();
        _spawned.Clear();

        var loaded = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(loaded);
        var objs = SaveLoadManager.RestoreScene(loaded!);
        _spawned.AddRange(objs);
        var restored = objs.Select(g => g.GetComponent<KitchenElement>())
            .Where(e => e != null).ToList()!;

        var restoredPipe = restored.FirstOrDefault(e => e.PartName == "Run");
        var restoredElbow = restored.FirstOrDefault(e => e.PartName == "Elb");
        var restoredCap = restored.FirstOrDefault(e => e.PartName == "Cap");
        Assert.IsNotNull(restoredPipe, "труба обязана вернуться после load");
        Assert.IsNotNull(restoredElbow, "уголок обязан вернуться после load");
        Assert.IsNotNull(restoredCap, "заглушка обязана вернуться после load");

        Assert.AreEqual(1, JoinedLinks(restoredPipe!, restoredElbow!),
            "SceneRestorer обязан закрыть открытый стык труба-фитинг ДАЖЕ когда у того же "
            + "фитинга уже есть более близкий сомкнутый стык на другой ноге — иначе фитинг "
            + "никогда не выберет открытую ногу, потому что SnapPortDock.Best всегда "
            + "предлагает глобально ближайшую пару портов");
        Assert.AreEqual(1, JoinedLinks(restoredElbow!, restoredCap!),
            "починка открытого стыка на одной ноге фитинга не обязана рвать уже сомкнутый "
            + "стык на другой");

        var elbowSizes = ScenePipeSurvey.SizesOf(restoredElbow);
        Assert.AreNotEqual(PipeSpec.NoValue, ScenePipeSurvey.DesignationAt(elbowSizes, 0),
            "«Диаметр 1» отвода читается тем же путём, что и редактор свойств "
            + "(PipeFittingFieldsEditor → ScenePipeSurvey.SizesOf), и обязан быть заполнен, "
            + "как только обе ноги фитинга сомкнуты сетью");
        Assert.AreNotEqual(PipeSpec.NoValue, ScenePipeSurvey.DesignationAt(elbowSizes, 1),
            "«Диаметр 2» отвода обязан быть заполнен по той же причине");

        var capSizes = ScenePipeSurvey.SizesOf(restoredCap);
        Assert.AreNotEqual(PipeSpec.NoValue, ScenePipeSurvey.DesignationAt(capSizes, 0),
            "«Диаметр 1» одноногого фитинга (та же форма, что у Podacha/Obratka) обязан "
            + "быть заполнен, когда цепочка портов доходит до трубы с известным ДУ");
    }
}
