using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Сенсор задачи A/B: печатает ФАКТИЧЕСКИЙ зазор устье-в-устье между трубой
/// и обоими отводами на реальной сцене пользователя (замороженная копия
/// <c>docs/example.save.json</c>, снятая для этой задачи — сам файл этот тест не
/// трогает, agents/TESTS.md → «NEVER TOUCH IT»).
///
/// Тесты на замороженной сцене — СЕНСОР, а не критерий приёмки: они меряют и
/// печатают числа (<c>TestContext.WriteLine</c>), но ничего не утверждают о них.
/// Первая версия ставила сюда <c>Assert.LessOrEqual</c> — стык на сцене пользователя
/// был разомкнут на момент заморозки фикстуры, так что тест был красным вне
/// зависимости от качества починки, и стал бы снова красным при следующей заморозке
/// с любым другим случайным зазором. Критерий приёмки обязан стоять на сцене, которую
/// тест сажает САМ (<c>MouthToMouthGap_OnASelfSeatedScene_IsWithinTheProjectJoinTolerance</c>
/// ниже) — agents/TEST-DESIGN.md → «Snapshot baselines» и «A brute-force sweep...».
///
/// Настоящая причина разомкнутого стыка нашлась не в порядке операций
/// <c>ElementMover.FinishDrag</c> (он уже верный), а в <c>SceneRestorer.Restore</c>:
/// диагноз и починка — <c>ScenePipeJointGridRepairTests</c>.</summary>
public class PipeGapSensorTests
{
    private const string SaveFileName = "Fixtures/pipe-gap-scene.save.json";

    private string _json = "";
    private ProjectLoadStateGuard? _guard;

    /// <summary>Замороженная сцена, общая для трёх сенсоров: они только МЕРЯЮТ устья и
    /// печатают числа, ничего не двигая, а `SaveLoadManager.RestoreScene` на этой фикстуре
    /// стоит около секунды. Четвёртый тест сажает сцену САМ и поэтому общую сбрасывает.
    /// Восстановление ленивое — порядок тестов NUnit не гарантирует, а сбросить и поднять
    /// заново дешевле, чем договариваться о порядке.</summary>
    private List<KitchenElement>? _scene;

    /// <summary>Сколько записей даёт фикстура в <see cref="PartRegistry"/>. Снимается при
    /// первом восстановлении и сверяется на входе в КАЖДЫЙ следующий тест: сенсор, который
    /// что-то в общей сцене сдвинул или удалил, обязан краснеть здесь, а не тихо менять
    /// числа у соседа.</summary>
    private int _registryParts = -1;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "Tests/EditMode", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        _json = File.ReadAllText(fullPath);
        Assert.IsNotEmpty(_json);

        CaptureGlobals();
    }

    /// <summary>Снимок глобального состояния — ОДИН раз на класс. Сцена общая, а вместе с
    /// ней общее и то, что приехало из файла проекта (<c>SceneRestorer.RestoreProjectState</c>):
    /// потестовый <c>Restore()</c> сдирал бы настройки с живой сцены, и второй сенсор мерял
    /// бы ту же геометрию при других настройках. Соседние классы защищены прежним способом —
    /// состояние возвращается в <c>[OneTimeTearDown]</c>.</summary>
    private void CaptureGlobals()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);

        _guard = ProjectLoadStateGuard.Capture();

        s.AutoSave = false;
        s.SpatialGrid = false;
        s.NormalView.edgeOutline = false;
    }

    [SetUp]
    public void SetUp() => AssertSharedSceneIntact();

    [TearDown]
    public void TearDown() => FaceCache.Clear();

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        DropSharedScene();
        _guard?.Restore();
        _guard = null;
    }

    /// <summary>Сторож общей сцены на границе тестов: если она жива, её состав обязан быть
    /// тем же, что при восстановлении. Проверка стоит именно в <c>[SetUp]</c>, а не только
    /// внутри <see cref="SharedScene"/>, чтобы падение указывало на ПРЕДЫДУЩИЙ тест.</summary>
    private void AssertSharedSceneIntact()
    {
        if (_scene == null) return;
        Assert.AreEqual(_registryParts, PartRegistry.GetAll().Count,
            "общая сцена поехала между тестами: записей в PartRegistry стало другое число");
        Assert.AreEqual(_scene!.Count, _scene!.Count(e => e != null),
            "в общей сцене появились уничтоженные детали");
    }

    /// <summary>Сцена фикстуры, общая для читающих сенсоров: поднимается ЛЕНИВО и
    /// переиспользуется.</summary>
    private List<KitchenElement> SharedScene()
    {
        if (_scene != null)
        {
            AssertSharedSceneIntact();
            return _scene!;
        }

        ClearScene();
        var built = RestoreScene();
        _registryParts = PartRegistry.GetAll().Count;
        _scene = built;
        return built;
    }

    /// <summary>Забыть общую сцену и вычистить её из сцены Unity: зовёт только тест,
    /// которому нужна своя.</summary>
    private void DropSharedScene()
    {
        ClearScene();
        _scene = null;
        _registryParts = -1;
    }

    private void ClearScene()
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

    private static void ReportGap(List<KitchenElement> elements, string pipeName,
        string fittingName)
    {
        var pipe = elements.FirstOrDefault(e => e.PartName == pipeName);
        var fitting = elements.FirstOrDefault(e => e.PartName == fittingName);
        Assert.IsNotNull(pipe, $"в сцене обязана быть труба {pipeName}");
        Assert.IsNotNull(fitting, $"в сцене обязан быть фитинг {fittingName}");
        Assert.IsInstanceOf<ISnapPorts>(pipe, $"{pipeName} обязана нести устья");
        Assert.IsInstanceOf<ISnapPorts>(fitting, $"{fittingName} обязан нести устья");

        float gapMm = MinMouthGapMm(pipe!, fitting!);
        TestContext.WriteLine($"{pipeName} <-> {fittingName}: зазор устье-в-устье = "
            + $"{gapMm:F4} мм (допуск PipeJoint.JoinToleranceMm = "
            + $"{PipeJoint.JoinToleranceMm} мм)");
    }

    private static readonly string[] AllFiveNames =
        { "Truba", "Otvod_92", "Otvod_91", "Podacha", "Obratka" };

    /// <summary>Печатает по КАЖДОМУ устью каждого из пяти элементов задачи: позицию,
    /// ось, найденного партнёра (по сети <c>PipeNetwork</c>, тот же путь, которым
    /// пользуется <c>PipeFittingSizeLink</c>/<c>PipeFittingFieldsEditor</c>) и
    /// ФАКТИЧЕСКИЙ зазор устье-в-устье до БЛИЖАЙШЕГО порта с противоположной осью во
    /// всей сцене — даже когда сеть считает стык разомкнутым. Ничего не утверждает
    /// (agents/TEST-DESIGN.md → «Snapshot baselines»): это сенсор, а не критерий
    /// приёмки.</summary>
    [Test]
    public void PrintEveryPortOfAllFiveElements_OnTheFrozenUserScene()
    {
        var elements = SharedScene();
        var survey = ScenePipeSurvey.Of(elements);

        foreach (var name in AllFiveNames)
        {
            var el = elements.FirstOrDefault(e => e.PartName == name);
            Assert.IsNotNull(el, $"в сцене обязан быть элемент {name}");
            Assert.IsInstanceOf<ISnapPorts>(el, $"{name} обязан нести устья");
            var ported = (ISnapPorts)el!;

            for (int i = 0; i < ported.SnapPortCount; i++)
            {
                var mouth = ported.SnapPortAt(i, el!.transform.position);

                int myPortIndex = -1;
                for (int p = 0; p < survey.Ports.Count; p++)
                    if (string.Equals(survey.Ports[p].ElementId, name, System.StringComparison.Ordinal)
                        && survey.Ports[p].PortIndex == i) { myPortIndex = p; break; }

                string partnerLine = "нет кандидата с противоположной осью";
                if (myPortIndex >= 0)
                {
                    int networkPartner = survey.Network.PartnerOf(myPortIndex);
                    var mine = survey.Ports[myPortIndex];

                    int nearestOpposite = -1;
                    float nearestGapMm = float.MaxValue;
                    for (int p = 0; p < survey.Ports.Count; p++)
                    {
                        if (p == myPortIndex) continue;
                        if (string.Equals(survey.Ports[p].ElementId, name,
                                System.StringComparison.Ordinal)) continue;
                        if (!PipeAxis.AreOpposite(mine.OutwardAxis, survey.Ports[p].OutwardAxis))
                            continue;
                        float g = mine.PositionMm.DistanceMmTo(survey.Ports[p].PositionMm);
                        if (g < nearestGapMm) { nearestGapMm = g; nearestOpposite = p; }
                    }

                    if (nearestOpposite >= 0)
                    {
                        var found = survey.Ports[nearestOpposite];
                        bool network = networkPartner == nearestOpposite;
                        partnerLine = $"ближайший противоположный = {found.ElementId}"
                            + $"[устье {found.PortIndex}], зазор = {nearestGapMm:F4} мм"
                            + $" (сеть считает стык {(network ? "СОМКНУТЫМ" : "РАЗОМКНУТЫМ")}"
                            + $", допуск {PipeJoint.JoinToleranceMm} мм)";
                    }
                }

                TestContext.WriteLine($"{name}[устье {i}]: позиция = "
                    + $"({mouth.Position.x / AppConstants.MM_TO_UNITS:F4}, "
                    + $"{mouth.Position.y / AppConstants.MM_TO_UNITS:F4}, "
                    + $"{mouth.Position.z / AppConstants.MM_TO_UNITS:F4}) мм, ось = "
                    + $"{mouth.Outward}, {partnerLine}");
            }
        }
    }

    [Test]
    public void MouthToMouthGap_TrubaToOtvod92_OnTheFrozenUserScene()
    {
        var elements = SharedScene();
        ReportGap(elements, "Truba", "Otvod_92");
    }

    [Test]
    public void MouthToMouthGap_TrubaToOtvod91_OnTheFrozenUserScene()
    {
        var elements = SharedScene();
        ReportGap(elements, "Truba", "Otvod_91");
    }

    [Test]
    public void MouthToMouthGap_OnASelfSeatedScene_IsWithinTheProjectJoinTolerance()
    {
        DropSharedScene();

        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);
        s.SnapEnabled = true;
        s.SnapThreshold = 50f;

        float toU = AppConstants.MM_TO_UNITS;
        var pipeGo = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, 600, "SensorPipe",
            new Vector3(0f, 300f * toU, 0f));
        var pipe = pipeGo.GetComponent<PipeElement>();
        var elbowGo = ElementFactory.CreatePipeElbow("SensorElbow", Vector3.zero);
        var elbow = elbowGo.GetComponent<PipeFittingElement>();
        elbow.transform.position += pipe.EndAUnits + new Vector3(0f, -20f * toU, 0f)
            - elbow.PortPositionUnits(0);

        var scene = new List<KitchenElement> { pipe, elbow };
        elbow.SeatAfterMove(scene);

        float gapMm = MinMouthGapMm(pipe, elbow);
        TestContext.WriteLine($"SensorPipe <-> SensorElbow (сцена, посаженная тестом): "
            + $"зазор устье-в-устье = {gapMm:F4} мм (допуск PipeJoint.JoinToleranceMm = "
            + $"{PipeJoint.JoinToleranceMm} мм)");

        Assert.LessOrEqual(gapMm, PipeJoint.JoinToleranceMm,
            $"IAutoSeated.SeatAfterMove обязан закрыть стык устье-в-устье в допуск "
            + $"({PipeJoint.JoinToleranceMm} мм) на детерминированной сцене, которую сажает "
            + "сам тест — это критерий приёмки, который не зависит от конкретного "
            + "разомкнутого файла пользователя");
    }
}
