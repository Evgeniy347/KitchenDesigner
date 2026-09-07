using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож переноса старого «ручного» бита в три состояния присутствия
/// кромки — и, прежде всего, сторож РАСКРОЯ.
///
/// До передела жёлтая сторона значила «не выдавать EDG-01 по этому торцу» и на
/// спецификацию не влияла вовсе: колонки кромки считались из одного расчёта по
/// сцене. Значит перенос обязан быть таким, чтобы метраж кромки по проекту не
/// сдвинулся ни на миллиметр:
///
///   было «ручная» + расчёт говорит «кромка есть»  → «принудительно есть»
///   было «ручная» + расчёт говорит «кромки нет»   → «убрать»
///
/// Старый жёлтый на СЕРОМ торце ровно и означал «кромки тут нет, не ругайся» —
/// это и есть новый красный. Ниже это доказано числом, а не рассуждением:
/// суммарный метраж кромки по замороженной копии проекта пользователя считается
/// дважды — по прежнему правилу (только расчёт) и по нынешнему
/// (EdgeBanding.HasEdgeEffective) — и обязан совпасть.
///
/// Сцена берётся из ЗАМОРОЖЕННОЙ копии, а не из docs/example.save.json: тот
/// файл живой, его переписывает автосохранение десктопа (та же причина, что у
/// ValidationInvariantTests).</summary>
public class EdgeStateMigrationTests
{
    private const string SaveFileName = "Fixtures/validation-scene.save.json";
    private const string ReportFileName = "edge-migration.log";

    private string _json = "";
    private string _reportPath = "";
    private ProjectLoadStateGuard? _guard;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "Tests/EditMode", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        _json = File.ReadAllText(fullPath);
        Assert.IsNotEmpty(_json);

        var dir = Path.Combine(Application.dataPath, "../test-results");
        Directory.CreateDirectory(dir);
        _reportPath = Path.Combine(dir, ReportFileName);
        File.WriteAllText(_reportPath, $"# Перенос состояний кромки: {SaveFileName}\n");
    }

    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);

        _guard = ProjectLoadStateGuard.Capture();
        s.AutoSave = false;
        s.SpatialGrid = false;

        ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        ClearScene();
        _guard?.Restore();
        FaceCache.Clear();
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

    private List<KitchenElement> RestoreFrozenProject()
    {
        var data = SaveLoadManager.Deserialize(_json);
        Assert.IsNotNull(data);
        var objs = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(objs);
        return objs.Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList()!;
    }

    /// <summary>Метраж кромки: сумма длин тех торцов, на которых кромка есть.
    /// <c>respectExplicitStates = false</c> воспроизводит правило, действовавшее
    /// ДО передела, — тогда спецификация смотрела только на расчёт по сцене, а
    /// «ручной» бит не читала вовсе.</summary>
    private static long EdgeMetreageMM(IReadOnlyList<KitchenElement> all, bool respectExplicitStates)
    {
        long total = 0;
        foreach (var el in all)
        {
            if (el == null || !el.EdgeBandingEnabled) continue;
            var layout = EdgeBanding.LayoutOf(el.DimensionsMM);
            if (!layout.IsValid) continue;

            var coverage = EdgeBanding.Coverage(el, all);
            foreach (EdgeSide side in EdgeStates.All)
            {
                bool hasEdge = respectExplicitStates
                    ? EdgeBanding.HasEdgeEffective(el, coverage, side)
                    : coverage.HasEdge(side);
                if (hasEdge) total += layout.SideLengthMM(side);
            }
        }
        return total;
    }

    [Test]
    public void Migration_OfTheUsersProject_LeavesTheCutListUntouchedToTheMillimetre()
    {
        var all = RestoreFrozenProject();

        long asBefore = EdgeMetreageMM(all, respectExplicitStates: false);
        long asNow = EdgeMetreageMM(all, respectExplicitStates: true);

        int forced = 0, suppressed = 0, parts = 0;
        foreach (var el in all)
        {
            if (el == null || !el.SupportsEdges) continue;
            int f = CountBits(el.EdgeForcedMask), s = CountBits(el.EdgeSuppressedMask);
            if (f + s == 0) continue;
            parts++;
            forced += f;
            suppressed += s;
        }

        File.AppendAllText(_reportPath,
            $"деталей с явными сторонами: {parts}\n"
            + $"сторон «принудительно есть»: {forced}\n"
            + $"сторон «убрать»: {suppressed}\n"
            + $"метраж кромки ДО переноса (только расчёт), мм: {asBefore}\n"
            + $"метраж кромки ПОСЛЕ переноса (HasEdgeEffective), мм: {asNow}\n"
            + $"расхождение, мм: {asNow - asBefore}\n");

        Assert.Greater(parts, 0,
            "в замороженной копии обязаны быть детали со старым ручным битом — иначе тест "
            + "ничего не доказывает, он просто проходит по пустому множеству");
        Assert.AreEqual(asBefore, asNow,
            $"перенос сдвинул раскрой на {asNow - asBefore} мм. Он обязан быть тождественным: "
            + "старый жёлтый бит на ЗЕЛЁНОМ торце становится «принудительно есть» (кромка была "
            + "и осталась), на СЕРОМ — «убрать» (кромки не было и нет). Числа — в "
            + $"test-results/{ReportFileName}");
    }

    /// <summary>Перенос обязан произойти там, где сцена УЖЕ СОБРАНА: он считает
    /// перекрытие торца соседями, а перекрытия в момент разбора JSON ещё не
    /// существует. Отсюда и место — <c>SceneRestorer</c>, после расстановки всех
    /// деталей и выравнивания их по миллиметровой сетке.</summary>
    [Test]
    public void Migration_SplitsTheOldManualBit_ByWhatTheSceneSays()
    {
        var shelf = ElementFactory.CreatePart(new Vector3Int(800, 18, 400), "Полка", Vector3.zero)
            .GetComponent<KitchenElement>();
        ElementFactory.CreatePart(new Vector3Int(18, 720, 400), "Стойка",
            new Vector3(0.409f, 0f, 0f));

        var coverage = EdgeBanding.Coverage(shelf, PartRegistry.GetAll());
        Assume.That(coverage.HasEdge(EdgeSide.W1), Is.False, "торец W1 упирается в стойку");
        Assume.That(coverage.HasEdge(EdgeSide.W2), Is.True, "торец W2 открыт");

        var project = SaveLoadManager.CaptureScene(PartRegistry.GetAll());
        foreach (var ed in project.elements)
        {
            ed.edgeManualMask = EdgeManual.AllMask;
            ed.edgeSuppressedMask = EdgeStates.Unmigrated;
        }
        var json = SaveLoadManager.Serialize(project);

        ClearScene();
        var restored = SaveLoadManager.RestoreScene(SaveLoadManager.Deserialize(json)!)
            .Select(g => g.GetComponent<KitchenElement>())
            .First(e => e != null && e.PartName == "Полка")!;

        Assert.AreEqual(EdgeSideState.Suppressed, restored.EdgeStateOf(EdgeSide.W1),
            "жёлтый на СЕРОМ торце значил «кромки тут нет, не ругайся» — это и есть новый красный");
        Assert.AreEqual(EdgeSideState.Forced, restored.EdgeStateOf(EdgeSide.W2),
            "жёлтый на ЗЕЛЁНОМ торце значил «кромка есть, проверку не проводи»");
    }

    [Test]
    public void Migration_DoesNotTouchAFileThatAlreadyCarriesTheSecondMask()
    {
        var shelf = ElementFactory.CreatePart(new Vector3Int(800, 18, 400), "Полка", Vector3.zero)
            .GetComponent<KitchenElement>();
        ElementFactory.CreatePart(new Vector3Int(18, 720, 400), "Стойка",
            new Vector3(0.409f, 0f, 0f));
        shelf.SetEdgeState(EdgeSide.W1, EdgeSideState.Forced);

        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(PartRegistry.GetAll()));
        ClearScene();
        var restored = SaveLoadManager.RestoreScene(SaveLoadManager.Deserialize(json)!)
            .Select(g => g.GetComponent<KitchenElement>())
            .First(e => e != null && e.PartName == "Полка")!;

        Assert.AreEqual(EdgeSideState.Forced, restored.EdgeStateOf(EdgeSide.W1),
            "у СВОЕГО файла вторая маска записана (пусть и нулём), и перенос его не трогает: "
            + "иначе «принудительно есть» на закрытом торце — законный выбор человека — "
            + "переворачивалось бы в «убрать» на каждой загрузке");
    }

    private static int CountBits(int mask)
    {
        int n = 0;
        for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) n++;
        return n;
    }
}
