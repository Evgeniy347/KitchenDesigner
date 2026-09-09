using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Задачи 4 и 6 из пользовательской жалобы: обе "уже сделаны" по коду
/// (PipeRunFollow, PipeDocking.ReseatAfterRotation) и обе зелены на синтетической
/// паре, построенной тестом в коде (PipeRunFollowSceneTests, PipeFittingRotationLinksTests).
/// В приложении не работает НИ ОДНА. Раз тесты зелёные, а приложение — нет, они проверяют
/// не тот вход: этот класс идёт путём ПОЛЬЗОВАТЕЛЯ — грузит его сцену через настоящий
/// SaveLoadManager.RestoreScene (не строит пару фитинг+труба вручную) и повторяет
/// драг/поворот НА НЕЙ.
///
/// Сцена — замороженная копия docs/example.save.json, снятая для этой задачи
/// (Fixtures/pipe-gap-scene.save.json, сам docs/example.save.json тест не трогает).
/// В ней: Truba (dn20, длина 372, ось X), Otvod_92 и Otvod_91 на её концах, Podacha за
/// 92-м, Obratka за 91-м — то есть у КАЖДОГО отвода два реальных соседа: труба (фитинг
/// вправе тянуть только её — задача 4 не про фитинги) и ещё один фитинг (подача/обратка).
///
/// PipeUserPathSensorTests ниже печатает то, что видит сетка портов, Hold и
/// ConnectedMouths, и ничего не утверждает — это чистый сенсор (тот же приём, что и в
/// PipeGapSensorTests, и по той же причине: разомкнутый стык в файле ПОЛЬЗОВАТЕЛЯ не
/// обязан совпадать с зазором на следующей заморозке фикстуры).
///
/// PipeUserPathReproTests ниже уже УТВЕРЖДАЕТ — это критерий приёмки, и он идёт по
/// маршруту пользователя (load -> drag/поворот -> проверка стыка), а не по синтетической
/// паре.</summary>
public class PipeUserPathSensorTests
{
    private const string SaveFileName = "Fixtures/pipe-gap-scene.save.json";

    private string _json = "";
    private ProjectLoadStateGuard? _guard;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "Tests/EditMode", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        _json = File.ReadAllText(fullPath);
        Assert.IsNotEmpty(_json);
    }

    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);
        _guard = ProjectLoadStateGuard.Capture();
        s.AutoSave = false;
        s.SpatialGrid = false;
        s.NormalView.edgeOutline = false;
        ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        ClearScene();
        _guard?.Restore();
        FaceCache.Clear();
    }

    private static void ClearScene()
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

    private List<KitchenElement> RestoreScene()
    {
        var data = SaveLoadManager.Deserialize(_json);
        Assert.IsNotNull(data);
        var objs = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(objs);
        return objs.Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList()!;
    }

    private static KitchenElement Named(List<KitchenElement> elements, string name)
    {
        var found = elements.FirstOrDefault(e => e.PartName == name);
        Assert.IsNotNull(found, $"в сцене обязан быть элемент {name}");
        return found!;
    }

    /// <summary>Печатает, опознала ли сеть портов стык между A и B: если да — по какой
    /// паре портов и с каким зазором в мм (обязан быть внутри PipeJoint.JoinToleranceMm,
    /// сеть иначе не признала бы связь); если нет — ближайший зазор устье-в-устье, чтобы
    /// было видно, НАСКОЛЬКО он разомкнут.</summary>
    private static void ReportJoint(PipeSurvey survey, List<KitchenElement> elements,
        string aName, string bName)
    {
        var a = Named(elements, aName);
        var b = Named(elements, bName);
        var ports = survey.Ports;

        for (int i = 0; i < ports.Count; i++)
        {
            if (ports[i].ElementId != aName) continue;
            int partner = survey.Network.PartnerOf(i);
            if (partner == PipeNetwork.NoPartner) continue;
            if (ports[partner].ElementId != bName) continue;

            float gapMm = ports[i].PositionMm.DistanceMmTo(ports[partner].PositionMm);
            TestContext.WriteLine($"{aName}[{ports[i].PortIndex}] <-> {bName}"
                + $"[{ports[partner].PortIndex}]: СВЯЗАН сетью портов, зазор = {gapMm:F4} мм");
            return;
        }

        float bestMm = float.MaxValue;
        var pa = (ISnapPorts)a;
        var pb = (ISnapPorts)b;
        for (int i = 0; i < pa.SnapPortCount; i++)
        {
            var mouthA = pa.SnapPortAt(i, a.transform.position);
            for (int j = 0; j < pb.SnapPortCount; j++)
            {
                var mouthB = pb.SnapPortAt(j, b.transform.position);
                float gapMm = Vector3.Distance(mouthA.Position, mouthB.Position)
                    / AppConstants.MM_TO_UNITS;
                if (gapMm < bestMm) bestMm = gapMm;
            }
        }
        TestContext.WriteLine($"{aName} <-> {bName}: НЕ СВЯЗАН сетью портов, ближайший "
            + $"зазор устье-в-устье = {bestMm:F4} мм (допуск PipeJoint.JoinToleranceMm = "
            + $"{PipeJoint.JoinToleranceMm} мм)");
    }

    [Test]
    public void JointsSeenByThePortNetwork_AfterARealSceneLoad()
    {
        var elements = RestoreScene();
        var survey = ScenePipeSurvey.Of(elements);

        TestContext.WriteLine("--- Сеть портов после SaveLoadManager.RestoreScene ---");
        ReportJoint(survey, elements, "Truba", "Otvod_92");
        ReportJoint(survey, elements, "Truba", "Otvod_91");
        ReportJoint(survey, elements, "Otvod_92", "Podacha");
        ReportJoint(survey, elements, "Otvod_91", "Obratka");
        TestContext.WriteLine($"Всего связей в сети: {survey.Network.Links.Count} "
            + $"(портов в сети: {survey.Ports.Count})");
    }

    [Test]
    public void PipeRunFollowHold_ForOtvod92_AfterARealSceneLoad()
    {
        var elements = RestoreScene();
        var fitting = (PipeFittingElement)Named(elements, "Otvod_92");

        var holds = new List<PipeRunHold>();
        PipeRunFollow.Hold(fitting, elements, holds);

        TestContext.WriteLine($"PipeRunFollow.Hold(Otvod_92): {holds.Count} удержаний");
        foreach (var h in holds)
            TestContext.WriteLine($"  труба={h.Pipe?.PartName}, порт фитинга={h.FittingPort}, "
                + $"свободный конец трубы={h.FreeEnd}");

        if (holds.Count == 0)
            TestContext.WriteLine("НОЛЬ удержаний — это и есть объяснение пункта 4: "
                + "перетаскивание Otvod_92 вдоль трубы не найдёт, что удлинять.");
    }

    [Test]
    public void PipeDockingConnectedMouths_ForOtvod92_AfterARealSceneLoad()
    {
        var elements = RestoreScene();
        var fitting = (PipeFittingElement)Named(elements, "Otvod_92");

        var mouths = PipeDocking.ConnectedMouths(fitting, elements);

        TestContext.WriteLine($"PipeDocking.ConnectedMouths(Otvod_92) ДО поворота: "
            + $"{mouths.Count} устьев соседей");
        foreach (var m in mouths)
            TestContext.WriteLine($"  сосед={m.ElementId}, порт={m.PortIndex}, "
                + $"kind={m.OwnerKind}");

        if (mouths.Count == 0)
            TestContext.WriteLine("НОЛЬ запомненных устьев — это и есть объяснение пункта "
                + "6: ReseatAfterRotation получит пустой список и никогда не сдвинет "
                + "деталь после поворота.");
    }
}

/// <summary>Критерий приёмки для пунктов 4 и 6 — на сцене пользователя, не на
/// синтетической паре. Ожидание, зафиксированное ПЕРЕД починкой: КРАСНЫЙ (см. отчёт
/// агента — сенсор выше печатает, на каком именно шаге рвётся связь); после починки —
/// ЗЕЛЁНЫЙ.</summary>
public class PipeUserPathReproTests
{
    private const string SaveFileName = "Fixtures/pipe-gap-scene.save.json";

    private string _json = "";
    private ProjectLoadStateGuard? _guard;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "Tests/EditMode", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        _json = File.ReadAllText(fullPath);
        Assert.IsNotEmpty(_json);
    }

    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);
        _guard = ProjectLoadStateGuard.Capture();
        s.AutoSave = false;
        s.SpatialGrid = false;
        s.NormalView.edgeOutline = false;
        s.SnapEnabled = true;
        s.SnapThreshold = 50f;
        ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        ClearScene();
        _guard?.Restore();
        FaceCache.Clear();
    }

    private static void ClearScene()
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

    private List<KitchenElement> RestoreScene()
    {
        var data = SaveLoadManager.Deserialize(_json);
        Assert.IsNotNull(data);
        var objs = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(objs);
        return objs.Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList()!;
    }

    private static KitchenElement Named(List<KitchenElement> elements, string name)
    {
        var found = elements.FirstOrDefault(e => e.PartName == name);
        Assert.IsNotNull(found, $"в сцене обязан быть элемент {name}");
        return found!;
    }

    private static float MinMouthGapMm(KitchenElement a, KitchenElement b)
    {
        var pa = (ISnapPorts)a;
        var pb = (ISnapPorts)b;
        float best = float.MaxValue;
        for (int i = 0; i < pa.SnapPortCount; i++)
        {
            var mouthA = pa.SnapPortAt(i, a.transform.position);
            for (int j = 0; j < pb.SnapPortCount; j++)
            {
                var mouthB = pb.SnapPortAt(j, b.transform.position);
                float gapMm = Vector3.Distance(mouthA.Position, mouthB.Position)
                    / AppConstants.MM_TO_UNITS;
                if (gapMm < best) best = gapMm;
            }
        }
        return best;
    }

    /// <summary>Пункт 4, путь пользователя целиком: загрузить сцену, взять реальный
    /// Otvod_92 из неё (не построенный тестом заново), сделать то же, что делает
    /// ElementMover при перетаскивании вдоль трубы (Hold в начале жеста, потом
    /// FollowAll), и проверить, что труба удлинилась, а стык остался сомкнут.</summary>
    [Test]
    public void DraggingOtvod92AlongTheRun_OnTheLoadedScene_LengthensTrubaAndKeepsTheJoint()
    {
        var elements = RestoreScene();
        var fitting = (PipeFittingElement)Named(elements, "Otvod_92");
        var truba = (PipeElement)Named(elements, "Truba");
        int lengthBefore = truba.LengthMM;

        var holds = new List<PipeRunHold>();
        PipeRunFollow.Hold(fitting, elements, holds);
        Assert.Greater(holds.Count, 0,
            "Otvod_92 обязан удержать Truba в начале перетаскивания — на сцене "
            + "пользователя, не на паре, построенной тестом. Ноль удержаний здесь и есть "
            + "пункт 4: 'просто отвод слетает'");

        Vector3 axisUnits = truba.RunAxis;
        fitting.transform.position += axisUnits * (50f * AppConstants.MM_TO_UNITS);
        int following = PipeRunFollow.FollowAll(fitting, holds);

        Assert.Greater(following, 0, "перетаскивание обязано было потянуть трубу за собой");
        Assert.AreNotEqual(lengthBefore, truba.LengthMM,
            "длина трубы обязана измениться при перетаскивании подключённого фитинга "
            + "вдоль её оси");
        Assert.LessOrEqual(MinMouthGapMm(truba, fitting), PipeJoint.JoinToleranceMm,
            "стык обязан остаться сомкнут (устье в устье) после перетаскивания — фитинг "
            + "не должен слететь");
    }

    /// <summary>Пункт 6, путь пользователя целиком: загрузить сцену, взять реальный
    /// Otvod_92, повторить то, что делает ContextMenuUI.RotateAxis (запомнить связи,
    /// повернуть на 90°, пересадить) и проверить, что стык с трубой пережил поворот.
    /// Поворачивает вокруг ТОЙ оси из трёх, которая реально рвёт стык у этой детали —
    /// так тест не зависит от конкретной геометрии фикстуры и остаётся верен и после
    /// следующей заморозки.</summary>
    [Test]
    public void RotatingOtvod92_OnTheLoadedScene_ReseatsItOntoTheSameMouth()
    {
        var elements = RestoreScene();
        var fitting = (PipeFittingElement)Named(elements, "Otvod_92");

        var remembered = PipeDocking.ConnectedMouths(fitting, elements);
        Assert.Greater(remembered.Count, 0,
            "Otvod_92 обязан числить хотя бы одну связь на загруженной сцене — иначе "
            + "нечего сохранять при повороте, и это уже пункт 4/6 общей причины, а не "
            + "дефект поворота");

        Vector3 positionBeforeRotation = fitting.transform.position;
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        bool rotationBrokeTheJoint = false;

        foreach (var axis in axes)
        {
            fitting.transform.position = positionBeforeRotation;
            fitting.transform.rotation =
                Quaternion.AngleAxis(90f, axis) * fitting.transform.rotation;

            var survey = ScenePipeSurvey.Of(elements);
            int linksAfterRotationAlone = survey.Network.Links.Count(l =>
                survey.Ports[l.APortIndex].ElementId == fitting.PartName
                || survey.Ports[l.BPortIndex].ElementId == fitting.PartName);

            if (linksAfterRotationAlone < remembered.Count)
            {
                rotationBrokeTheJoint = true;
                break;
            }

            fitting.transform.rotation =
                Quaternion.AngleAxis(-90f, axis) * fitting.transform.rotation;
        }

        Assert.IsTrue(rotationBrokeTheJoint,
            "стенд обязан доказать сам себя: хотя бы один из трёх поворотов на 90° обязан "
            + "разомкнуть стык, иначе пересаживать нечего и тест ничего не отличает");

        bool moved = PipeDocking.ReseatAfterRotation(fitting, remembered);

        Assert.IsTrue(moved,
            "ReseatAfterRotation обязан сдвинуть Otvod_92 так, чтобы порт снова сел "
            + "устье в устье на того же соседа — на сцене пользователя, не на паре, "
            + "построенной тестом");

        var surveyAfter = ScenePipeSurvey.Of(elements);
        int linksAfterReseat = surveyAfter.Network.Links.Count(l =>
            surveyAfter.Ports[l.APortIndex].ElementId == fitting.PartName
            || surveyAfter.Ports[l.BPortIndex].ElementId == fitting.PartName);
        Assert.Greater(linksAfterReseat, 0,
            "после пересадки стык с соседом обязан снова числиться в сети портов");
    }
}
