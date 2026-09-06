using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Инвариант валидации — то же, чем <see cref="SnapMutationTests"/> страхует снэп,
/// только для правил валидации. Реальная сцена <c>docs/example.save.json</c>
/// прогоняется через все три валидатора, и числа нарушений по типам обязаны
/// совпасть до единицы. Расхождение хоть на одно означает, что поведение
/// валидации поехало.
///
/// Зачем: вынос ядра в <c>KitchenDesigner.Geometry</c> увёл
/// <see cref="ConstraintValidator"/> в ядро, а связь у него СЕМАНТИЧЕСКАЯ
/// («это пол», «это проём», «это мойка — пропустить»), не геометрическая.
/// Снимок геометрии её не ловит, шесть точечных наборов тестов — сеть заметно
/// реже сети снэпа. Этот файл закрывал дыру на время переноса и стережёт её до сих пор.
///
/// Полный список нарушений пишется в
/// <c>test-results/validation-invariant.log</c>; в консоль идёт только таблица
/// расхождений.
///
/// **Как перезакрепить baseline.** Только осознанно: сначала объяснить, почему
/// число изменилось. Актуальные значения тест печатает в отчёт секцией
/// «Счётчики»; их и переносить в <see cref="Baseline"/>.
/// </summary>
public class ValidationInvariantTests
{
    /// <summary>Сцена берётся из ЗАМОРОЖЕННОЙ копии, а не из
    /// <c>docs/example.save.json</c>: тот файл живой — его перезаписывает
    /// автосохранение десктопа и PlayMode-прогон (см. комментарий в
    /// <c>PlayModeTestConfig</c>). Один такой прогон менял в нём режим окна, и
    /// инвариант краснел на −7 контактов, хотя код валидации никто не трогал.
    /// Baseline обязан стоять на неподвижном входе.</summary>
    private const string SaveFileName = "Fixtures/validation-scene.save.json";
    private const string ReportFileName = "validation-invariant.log";

    /// <summary>Пять с лишним чисел, которые обязаны совпасть. Порядок — как в
    /// отчёте.</summary>
    private static readonly (string Key, int Expected)[] Baseline =
    {
        ("elements",                    274),
        ("contacts",                   1336),
        ("violations",                    0),
        ("isolatedGroups",                0),
        ("kind.Overlap",                  0),
        ("kind.Unsupported",              0),
        ("kind.OutOfWallBounds",          0),
        // Сцена ЧИСТА по ConstraintValidator, но не по остальным двум: 30 ящиков
        // с зазором 40 мм на сторону (допуск 12,5) и 26 столкновений траектории
        // открывания фасадов. Это baseline реального сейва, а не эталон качества —
        // здесь важно, что число не меняется само по себе.
        //
        // Было 63, пока траектория считалась осевым габаритом, объединяющим
        // закрытую и текущую позу: у двери 670 мм такая коробка накрывает весь
        // квадрант, и 37 «столкновений» приходились на пустое место между её
        // противоположными углами. Теперь поза меряется своей ориентированной
        // коробкой (BoxOverlap), а сосед, которого фасад касается закрытым,
        // гасится только в своей ТЕНИ (ContactShadow), а не целиком.
        ("drawer.errors",                30),
        ("facade.faceObstructions",       0),
        ("facade.openingViolations",     26),
    };

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
        File.WriteAllText(_reportPath, $"# Инвариант валидации: {SaveFileName}\n");
    }

    /// <summary>Загрузка сейва переписывает глобальное состояние целиком
    /// (KitchenSettings, режим ручек, статики подсветки) — снимаем и возвращаем
    /// его так же, как это делает SaveValidationTests.</summary>
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

    // ── Тест 1: числа нарушений ─────────────────────────────────────────

    [Test]
    public void Validation_ExampleSave_MatchesBaseline()
    {
        var elements = RestoreScene();
        var snapshot = Collect(elements);

        var lines = new List<string> { "", "## Счётчики" };
        foreach (var kv in snapshot.Counters)
            lines.Add($"{kv.Key,-28} {kv.Value}");
        lines.Add("");
        lines.Add("## Нарушения");
        lines.AddRange(snapshot.Details.Count > 0 ? snapshot.Details : new List<string> { "чисто" });
        File.AppendAllLines(_reportPath, lines);

        var mismatches = new List<string>();
        foreach (var (key, expected) in Baseline)
        {
            Assert.IsTrue(snapshot.Counters.ContainsKey(key), $"Счётчик '{key}' не собран");
            int actual = snapshot.Counters[key];
            if (actual != expected)
                mismatches.Add($"{key,-28} baseline {expected,6}   факт {actual,6}   "
                    + $"({(actual - expected > 0 ? "+" : "")}{actual - expected})");
        }

        if (mismatches.Count == 0) return;

        Assert.Fail($"Поведение валидации изменилось ({mismatches.Count} из {Baseline.Length} "
            + "счётчиков разошлись):\n" + string.Join("\n", mismatches)
            + $"\nПодробности: {_reportPath}");
    }

    // ── Тест 2: детерминизм ─────────────────────────────────────────────

    /// <summary>Тот же результат на той же сцене. Валидатор держит статические
    /// scratch-буферы и словарь broad-phase сетки; если их чистка сломается,
    /// baseline первого теста будет «плавать» между прогонами, а мутационный
    /// прогон — давать ложные убийства.</summary>
    [Test]
    public void Validation_ExampleSave_IsDeterministic()
    {
        var elements = RestoreScene();
        var first = Collect(elements);
        var second = Collect(elements);

        CollectionAssert.AreEqual(first.Counters.Values.ToList(), second.Counters.Values.ToList(),
            "Счётчики валидации разошлись между двумя прогонами на одной сцене");
        CollectionAssert.AreEqual(first.Details, second.Details,
            "Список нарушений разошёлся между двумя прогонами на одной сцене");
    }

    // ── сбор ────────────────────────────────────────────────────────────

    private readonly struct Snapshot
    {
        public readonly Dictionary<string, int> Counters;
        public readonly List<string> Details;

        public Snapshot(Dictionary<string, int> counters, List<string> details)
        {
            Counters = counters;
            Details = details;
        }
    }

    /// <summary>Один прогон всех трёх валидаторов. Детали сортируются, чтобы
    /// отчёт не зависел от порядка обхода сцены.</summary>
    private static Snapshot Collect(List<KitchenElement> elements)
    {
        var alive = elements.Where(e => e != null).ToList();
        var result = ConstraintValidator.Validate(alive);

        var counters = new Dictionary<string, int>
        {
            ["elements"] = alive.Count,
            ["contacts"] = result.contacts.Count,
            ["violations"] = result.violations.Count,
            ["isolatedGroups"] = result.isolatedGroups.Count,
        };

        var diagnostics = result.diagnostics ?? new List<ContactViolation>();
        foreach (ViolationKind kind in System.Enum.GetValues(typeof(ViolationKind)))
            counters[$"kind.{kind}"] = diagnostics.Count(d => d.kind == kind);

        var details = diagnostics
            .Select(d => $"{d.kind,-16} {Name(d.element),-32} {Name(d.other)}")
            .ToList();

        // Ящики: обе проверки сразу (боковины + посадка в корпус).
        int drawerErrors = 0;
        foreach (var drawer in alive.OfType<DrawerElement>().OrderBy(d => d.PartName, System.StringComparer.Ordinal))
        {
            var r = DrawerValidator.ValidateAll(drawer, alive);
            if (r.Errors == null) continue;
            drawerErrors += r.Errors.Count;
            foreach (var err in r.Errors)
                details.Add($"{"Drawer",-16} {Name(drawer),-32} {err}");
        }
        counters["drawer.errors"] = drawerErrors;

        // Фасады: что стоит перед лицевой гранью и во что упирается открывание.
        int obstructions = 0;
        int openings = 0;
        foreach (var facade in alive.OfType<FacadeElement>().OrderBy(f => f.PartName, System.StringComparer.Ordinal))
        {
            foreach (var o in FacadeValidator.FindFaceObstructions(facade, alive))
            {
                obstructions++;
                details.Add($"{"FaceObstruction",-16} {Name(facade),-32} {o.neighbor}");
            }
            foreach (var v in FacadeValidator.FindOpeningViolations(facade, alive))
            {
                openings++;
                details.Add($"{"OpeningViolation",-16} {Name(facade),-32} {v.neighbor}");
            }
        }
        counters["facade.faceObstructions"] = obstructions;
        counters["facade.openingViolations"] = openings;

        details.Sort(System.StringComparer.Ordinal);
        return new Snapshot(counters, details);
    }

    private static string Name(KitchenElement? e) => e == null ? "—" : e.PartName;
}
