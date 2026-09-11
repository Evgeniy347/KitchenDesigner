using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>
/// Валидация сохранённой сцены — «стоп-кран перед пилорамой».
/// Парный к <see cref="SnapMutationTests"/>: тот двигает и растягивает детали и
/// ловит баги прилипания, этот ничего не мутирует и проверяет, годится ли сцена
/// к раскрою:
///   1) все правила <see cref="SceneAnalyzer"/> (COL-01..03, EDG-01, GAP-01,
///      SEAT-01, FAC-01, DRW-01);
///   2) инвариант мм-сетки: координаты ГРАНЕЙ кратны 1 мм;
///   3) подпороговые стыки — щели и врезания в полосе (0.01, 0.5] мм, которую
///      валидатор по построению не видит (Tolerance.ContactMm — «касание»);
///   4) round-trip save→load: какие типы теряют живую позу.
///
/// Сцена берётся из ЗАМОРОЖЕННОЙ копии <c>docs/example.save.json</c>
/// (<c>Fixtures/pipe-gap-scene.save.json</c>), а не из живого файла: тот
/// переписывается автосохранением десктопа и правится пользователем каждый
/// день (agents/TESTS.md → «docs/example.save.json — NEVER TOUCH IT»), так что
/// приёмочные числа на нём плавают вместе с чужой работой, а не с кодом. Тесты
/// 1 и 3 когда-то стояли на живом файле и красили сборку каждый раз, когда
/// пользователь передвигал мебель — тот же приём, каким уже разведён
/// <see cref="PipeGapSensorTests"/>: детерминированная фикстура несёт критерий
/// приёмки, живой файл остался только сенсором в <see cref="SaveValidationSensorTests"/>.
///
/// Фикстура — не эталон чистоты: на момент заморозки в ней было 2 реальные
/// находки COL-01 и 3 GAP-02 (мебель пользователя, не наш код) и 6 подпороговых
/// стыков трубопровода (см. Baseline при каждом тесте). Тест 1 и тест 3 поэтому
/// не требуют «ноль», а держат BASELINE по образцу <see cref="ValidationInvariantTests"/>:
/// рост числа — регрессия, снижение — повод осознанно подвинуть baseline вниз. Все
/// пять находок теста 1 с тех пор закрылись починками загрузчика, поэтому его
/// baseline сейчас нулевой — но нулём он стал по разбору каждой находки, а не
/// потому, что от чужой сцены ждут чистоты.
///
/// Полный список проблем пишется в test-results/save-validation.log; в консоль
/// идёт только сводка — сейв содержит 100+ деталей.
/// </summary>
public class SaveValidationTests
{
    private const string SaveFileName = "Fixtures/pipe-gap-scene.save.json";
    private const string ReportFileName = "save-validation.log";

    /// <summary>Baseline теста 1, снижен 2026-09-10 дважды. Сначала обе прежние
    /// COL-01-ошибки закрылись сами — это была недосомкнутая пара стыков трубопровода
    /// (ScenePipeJointGridRepairTests чинит тот же класс дефекта), не ослабление теста.
    ///
    /// Затем ушли и три GAP-02 — зазор 5 мм под опорами B2/B4 (Leg_B2_B3, Leg_B3_B4,
    /// Leg_B4_B5 — это PillarElement, не винтовые ножки). Что зазора там БЫТЬ не
    /// должно, читается прямо в фикстуре: пол Pol_1 кончается на 0 мм, опора стоит на
    /// нём и высока 100 мм (dimY = 100, midHeightMM = 70 + 20 + 10), а дно шкафа
    /// B2_bottom — плита 16 мм с центром 108 мм — начинается ровно на 100 мм. Стык
    /// вплотную, мерить нечего.
    ///
    /// Единственное, что могло дать 5 мм на этой геометрии, — высота опоры, и
    /// менял её только загрузчик: RepairJointAfterGridSnap звал полный автоподбор
    /// перетаскивания, а тот пересчитывал высоту из просвета над опорой и подменял
    /// сохранённое число своим при КАЖДОМ открытии файла. С тех пор как загрузчик
    /// только сажает (PillarAutoFit.SeatWithoutResizing), в сцене стоит высота из
    /// сейва. Находка исчезла вместе с дефектом, который её порождал, а не потому,
    /// что правило замолчало: GAP-02 стережётся ValidationInvariantTests на своей
    /// сцене.</summary>
    private const int BaselineIssueErrors = 0;
    private const int BaselineIssueWarnings = 0;
    private const int BaselineIssueGap02 = 0;

    /// <summary>Baseline теста 3, снижен 2026-09-10: два стыка трубопровод-трубопровод,
    /// что несли реальный разомкнутый зазор, сомкнулись той же починкой (baseline 6 → 4).
    /// Остаток — четыре записи, все геометрический шум ≤0.075 мм, на порядок меньше
    /// Tolerance.ContactMm (0.5 мм): Truba↔Otvod_91 и Truba↔Otvod_92 по 0.025 мм
    /// (остаточная погрешность посадки порта), и Pol_1 (пол, кратен мм-сетке) ↔
    /// Obratka (фитинг, ISnapPorts — MmGrid его сознательно НЕ трогает, см.
    /// PipeDocking) по 0.075 мм — независимое округление двух систем, а не
    /// разомкнутый стык; тем же путём объяснена dn20-погрешность в SUBSYSTEMS.md.</summary>
    private const int BaselineSubToleranceJoints = 4;

    /// <summary>Сколько строк каждой категории печатать в консоль (остальное — в файл).</summary>
    private const int ConsoleSamples = 5;

    /// <summary>Допуск проверки кратности, мм. На порядок мельче самого мелкого
    /// реального отклонения (0.07 мм) и на два порядка крупнее точности float32
    /// на координатах ~10 м (≈0.001 мм), так что ложных срабатываний не даёт.</summary>
    private const float GridEpsMm = 0.01f;

    private string _json = "";
    private string _reportPath = "";
    private ProjectLoadStateGuard? _guard;

    /// <summary>Общая сцена класса. `SaveLoadManager.RestoreScene` на этой фикстуре стоит
    /// около секунды, а тесты 1–3 сцену только ЧИТАЮТ — ни один из них ничего не двигает.
    /// Поэтому она восстанавливается ЛЕНИВО и переиспользуется, а тест 4 (round-trip)
    /// сбрасывает её и поднимает свою: ему нужна сцена «до» того, как по ней кто-то
    /// прошёлся. Ленивый сброс вместо порядка тестов выбран сознательно — порядок NUnit
    /// не гарантирует, а при любом порядке восстановлений выходит ровно три вместо
    /// прежних пяти.</summary>
    private List<KitchenElement>? _scene;

    /// <summary>Сколько деталей даёт фикстура. Запоминается при первом восстановлении и
    /// сверяется при каждом следующем: одна и та же фикстура обязана давать один и тот
    /// же состав, иначе «общая сцена» превращается в тихо плывущие baseline соседа.</summary>
    private int _fixtureParts = -1;

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
        // Один отчёт на весь класс: каждый тест дописывает свою секцию.
        File.WriteAllText(_reportPath, $"# Валидация {SaveFileName}\n");

        CaptureGlobals();
    }

    /// <summary>Загрузка сейва переписывает ГЛОБАЛЬНОЕ состояние целиком: блок
    /// настроек (KitchenSettings — синглтон-ассет, включая ObjectsVisible /
    /// HideLightSources), режим ручек (ResizeHandleManager.Mode) и статики
    /// LightSourceElement.GlobalOn — RestoreScene
    /// выставляет их из файла проекта. Точечного сохранения пары флагов мало: в
    /// полном прогоне это роняло SceneVisibilityTests, SettingsPanelUITests, а
    /// тонировка утекала в эталоны SnapshotTests. Снимаем и возвращаем всё
    /// целиком, как это делает SnapshotTests.
    ///
    /// Снимок снимается и возвращается ОДИН раз на класс,
    /// а не на тест. Загрузка проекта тащит с собой не только детали: блок настроек, режим
    /// ручек, свет, комнаты и планы (<c>SceneRestorer.RestoreProjectState</c>). Сцена
    /// теперь общая — значит и это состояние общее, и потестовый <c>Restore()</c> сдирал бы
    /// с живой сцены её собственные настройки: первый тест видел бы файл, второй —
    /// умолчания, на той же самой геометрии. Контракт <see cref="ProjectLoadStateGuard"/>
    /// («набор, грузящий проект, возвращает состояние») при этом соблюдён: соседние классы
    /// получают состояние нетронутым.</summary>
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
        ClearScene();
        _scene = null;
        _guard?.Restore();
        _guard = null;
    }

    /// <summary>Сторож общей сцены: тест, который что-то в ней сдвинул или удалил, обязан
    /// быть виден здесь, а не в разъехавшемся baseline у соседа. Зовётся из <c>[SetUp]</c>
    /// — на границе тестов, чтобы падение указывало на ПРЕДЫДУЩИЙ тест, — и при каждом
    /// обращении к сцене.</summary>
    private void AssertSharedSceneIntact()
    {
        if (_scene == null) return;
        Assert.AreEqual(_fixtureParts, PartRegistry.GetAll().Count,
            "общая сцена поехала между тестами: деталей в реестре стало другое число");
        Assert.AreEqual(_scene!.Count, _scene!.Count(e => e != null),
            "в общей сцене появились уничтоженные детали");
    }

    /// <summary>Сцена фикстуры, общая для читающих тестов.</summary>
    private List<KitchenElement> SharedScene()
    {
        if (_scene != null)
        {
            AssertSharedSceneIntact();
            return _scene!;
        }

        ClearScene();
        var built = RestoreScene();
        int parts = PartRegistry.GetAll().Count;
        if (_fixtureParts < 0) _fixtureParts = parts;
        Assert.AreEqual(_fixtureParts, parts,
            "одна и та же фикстура дала разное число деталей при повторном восстановлении");
        _scene = built;
        return built;
    }

    /// <summary>Забыть общую сцену: следующий обратившийся поднимет её заново. Зовёт
    /// только тест, которому нужна своя сцена.</summary>
    private void DropSharedScene()
    {
        ClearScene();
        _scene = null;
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

        // Сейв несёт и project-level состояние (инструкции, комнаты, планы). Без
        // сброса оно протекает в соседние наборы — SnapshotTests сериализуют его
        // в эталоны (см. комментарий в SnapshotTests.ResetScene).
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

    /// <summary>Позиции по имени детали. Имена в реальном сейве не гарантированно
    /// уникальны, поэтому дубли схлопываем, а не роняем тест на ToDictionary.</summary>
    private static Dictionary<string, Vector3> PositionsByName(IEnumerable<KitchenElement> elements) =>
        elements.Where(e => e != null)
            .GroupBy(e => e.PartName)
            .ToDictionary(g => g.Key, g => g.First().transform.position);

    private void Report(string section, IEnumerable<string> lines)
    {
        var all = new List<string> { "", $"## {section}" };
        all.AddRange(lines);
        File.AppendAllLines(_reportPath, all);
    }

    private static string Mm(float mm) => mm.ToString("F3", CultureInfo.InvariantCulture);

    // ── Тест 1: правила SceneAnalyzer ───────────────────────────────────

    [Test]
    public void Analyze_FrozenPipeGapScene_MatchesKnownIssueBaseline()
    {
        SharedScene();
        var issues = SceneAnalyzer.Analyze();

        var lines = issues
            .OrderBy(i => i.Code, System.StringComparer.Ordinal)
            .ThenBy(i => i.Detail, System.StringComparer.Ordinal)
            .Select(i => $"{i.Level,-7} {i.Code,-7} {i.Detail,-48} {i.Message}")
            .ToList();
        Report("Правила SceneAnalyzer", lines.Count > 0 ? lines : new List<string> { "чисто" });

        int errors = issues.Count(i => i.Level == IssueLevel.Error);
        int warnings = issues.Count(i => i.Level == IssueLevel.Warning);
        int gap02 = issues.Count(i => i.Code == IssueCatalog.CodeNearContactFar);

        var mismatches = new List<string>();
        if (errors != BaselineIssueErrors)
            mismatches.Add($"errors    baseline {BaselineIssueErrors}   факт {errors}");
        if (warnings != BaselineIssueWarnings)
            mismatches.Add($"warnings  baseline {BaselineIssueWarnings}   факт {warnings}");
        if (gap02 != BaselineIssueGap02)
            mismatches.Add($"GAP-02    baseline {BaselineIssueGap02}   факт {gap02}");

        if (mismatches.Count == 0) return;

        Assert.Fail($"Находки SceneAnalyzer на {SaveFileName} разошлись с baseline "
            + $"({issues.Count} проблем всего):\n" + string.Join("\n", mismatches)
            + $"\n\n{Summarize(issues.Select(i => (i.Code, $"{i.Detail}: {i.Message}")))}"
            + $"\nПолный список: {_reportPath}");
    }

    // ── Тест 2: инвариант мм-сетки ──────────────────────────────────────

    /// <summary>Проверяется координата ГРАНИ, а не центра: у детали 1967 мм центр
    /// обязан лежать на .5 — это норма, а вот боковина 16 мм с центром 1200.5 мм
    /// стоит гранями на 1192.5 и 1208.5, и в такой проём ни одна целочисленная
    /// деталь уже не встанет. Именно так родился зазор 0.5 мм в цоколе.
    ///
    /// Детали с устьями (<c>ISnapPorts</c> — трубы и фитинги) сюда не входят: у
    /// dn20-трубы сечение 27 мм даёт центр на .5 по обеим поперечным осям для ЛЮБОЙ
    /// позиции устья, так что грани мимо целого мм — их нормальное геометрическое
    /// состояние, а не дефект. Это и есть причина, по которой <c>MmGrid.OffsetToGrid</c>
    /// перестал округлять такие детали вовсе (см. <c>ScenePipeJointGridRepairTests</c>) —
    /// посылка «любая деталь стоит гранями на мм» была верна ровно до этого класса
    /// деталей и здесь устарела.</summary>
    [Test]
    public void Geometry_ExampleSave_FacesOnMillimeterGrid()
    {
        var elements = SharedScene();
        const string catHalf = "ровно .5 мм";
        const string catTenth = "дробь на 0.1-мм сетке";
        const string catDirt = "мимо 0.1-мм сетки";
        var axisNames = new[] { "X", "Y", "Z" };

        var found = new List<(string category, float devMm, string line)>();
        foreach (var e in elements)
        {
            if (e == null || e is ISnapPorts) continue;
            bool axisAligned = IsAxisAligned(e.transform);
            var (min, max) = WorldBoundsMm(e);
            for (int axis = 0; axis < 3; axis++)
            {
                foreach (var (side, coordMm) in new[] { ("min", min[axis]), ("max", max[axis]) })
                {
                    float dev = coordMm - Mathf.Round(coordMm);
                    if (Mathf.Abs(dev) <= GridEpsMm) continue;

                    string category =
                        Mathf.Abs(Mathf.Abs(dev) - 0.5f) <= GridEpsMm ? catHalf
                        : Mathf.Abs(coordMm * 10f - Mathf.Round(coordMm * 10f)) <= GridEpsMm * 10f ? catTenth
                        : catDirt;
                    string rot = axisAligned ? "" : "  [повёрнут некратно 90°]";
                    found.Add((category, Mathf.Abs(dev),
                        $"{category,-22} {e.PartName,-28} {axisNames[axis]}{side,-4} = "
                        + $"{Mm(coordMm)} мм (отклонение {Mm(dev)}){rot}"));
                }
            }
        }

        var report = found.OrderBy(f => f.category, System.StringComparer.Ordinal)
            .ThenByDescending(f => f.devMm).Select(f => f.line).ToList();
        Report("Грани мимо мм-сетки", report.Count > 0 ? report : new List<string> { "чисто" });

        if (found.Count == 0) return;

        Assert.Fail($"Координат граней мимо мм-сетки: {found.Count} "
            + $"(деталей в сцене: {elements.Count}).\n"
            + Summarize(found.Select(f => (f.category, f.line)))
            + $"\nПолный список: {_reportPath}");
    }

    // ── Тест 3: подпороговые стыки ──────────────────────────────────────

    /// <summary>Слепая зона валидатора: зазор ровно 0.5 мм отсекается как «касание»
    /// (ConstraintValidator.MinParallelGap — порог включительный), а врезание мельче
    /// 0.5 мм не считается коллизией (AABBsIntersect вызывается с margin=ContactMm).
    /// Ни ошибки, ни предупреждения — а на распиле это брак, и перемещением детали
    /// он не лечится: деталь просто не того размера.</summary>
    [Test]
    public void Geometry_FrozenPipeGapScene_SubToleranceJointsMatchBaseline()
    {
        var elements = SharedScene();
        float toMm = 1f / AppConstants.MM_TO_UNITS;
        float deadBand = Tolerance.SnapEpsilon;                            // 0.01 мм
        float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS; // 0.5 мм

        var found = SubToleranceJointScanner.Find(elements, deadBand, contactDist);
        var report = found
            .OrderByDescending(j => Mathf.Abs(j.GapUnits))
            .Select(j => SubToleranceJointScanner.FormatLine(j, toMm))
            .ToList();
        Report("Подпороговые стыки (0.01 … 0.5 мм)",
            report.Count > 0 ? report : new List<string> { "чисто" });

        if (found.Count == BaselineSubToleranceJoints) return;

        Assert.Fail($"Стыков в слепой зоне валидатора: {found.Count} "
            + $"(baseline {BaselineSubToleranceJoints}). "
            + "Валидатор их не видит: |зазор| ≤ Tolerance.ContactMm считается касанием.\n"
            + string.Join("\n", report.Take(ConsoleSamples * 4))
            + (report.Count > ConsoleSamples * 4 ? $"\n… ещё {report.Count - ConsoleSamples * 4}" : "")
            + $"\nПолный список: {_reportPath}");
    }

    // ── Тест 4: что меняет round-trip ───────────────────────────────────

    /// <summary>Диагностика, а не требование: сохранение пишет ЛОГИЧЕСКУЮ позу
    /// (Wall.FullPosition, ClosedPosition двери/фасада/ящика — ElementCapture.FromElement),
    /// поэтому у этих типов перезагрузка стирает накопленный дрейф. Тест меряет,
    /// у каких именно деталей это происходит, и не валится.
    ///
    /// Единственный тест класса со СВОЕЙ сценой: он меряет позу «до» и сам же грузит
    /// сцену второй раз из того, что записал, — общую сцену класса он бы за собой
    /// оставил перезаписанной.</summary>
    [Test]
    public void RoundTrip_ExampleSave_ReportsPoseDrift()
    {
        DropSharedScene();
        var before = PositionsByName(RestoreScene());
        string captured = SaveLoadManager.CaptureCurrentJson();
        Assert.IsNotEmpty(captured);

        ClearScene();
        var data = SaveLoadManager.Deserialize(captured);
        Assert.IsNotNull(data);
        var restored = SaveLoadManager.RestoreScene(data!)
            .Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList()!;
        var after = PositionsByName(restored);

        float toMm = 1f / AppConstants.MM_TO_UNITS;
        var drift = new List<(float mm, string line)>();
        foreach (var kv in before)
        {
            if (!after.TryGetValue(kv.Key, out var pos)) continue;
            float dMm = (pos - kv.Value).magnitude * toMm;
            if (dMm <= GridEpsMm) continue;
            drift.Add((dMm, $"{kv.Key,-28} сдвиг {Mm(dMm),9} мм   "
                + $"{FormatPos(kv.Value)} → {FormatPos(pos)}"));
        }

        var report = drift.OrderByDescending(d => d.mm).Select(d => d.line).ToList();
        Report("Дрейф позы за round-trip", report.Count > 0 ? report : new List<string> { "нет" });
        Assert.Pass($"Деталей со сдвигом позы после save→load: {drift.Count} из {before.Count}. "
            + $"Подробности: {_reportPath}");
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static string FormatPos(Vector3 p) =>
        $"[{Mm(p.x * 1000f)}; {Mm(p.y * 1000f)}; {Mm(p.z * 1000f)}]";

    /// <summary>Поворот кратен 90°: все три локальные оси совпадают с мировыми.
    /// У повёрнутых иначе деталей AABB не обязан лежать на мм-сетке по природе.</summary>
    private static bool IsAxisAligned(Transform t)
    {
        foreach (var axis in new[] { t.right, t.up, t.forward })
        {
            float maxComponent = Mathf.Max(Mathf.Abs(axis.x),
                Mathf.Max(Mathf.Abs(axis.y), Mathf.Abs(axis.z)));
            if (maxComponent < 0.9999f) return false;
        }
        return true;
    }

    private static (Vector3 min, Vector3 max) WorldBoundsUnits(KitchenElement e)
    {
        var verts = e.GetVertices();
        Vector3 min = verts[0], max = verts[0];
        foreach (var v in verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
        return (min, max);
    }

    private static (Vector3 min, Vector3 max) WorldBoundsMm(KitchenElement e)
    {
        var (min, max) = WorldBoundsUnits(e);
        float toMm = 1f / AppConstants.MM_TO_UNITS;
        return (min * toMm, max * toMm);
    }

    /// <summary>Свод по категориям: счётчик и несколько примеров на каждую —
    /// полный список всё равно лежит в файле отчёта.</summary>
    private static string Summarize(IEnumerable<(string key, string line)> rows)
    {
        var groups = rows.GroupBy(r => r.key).OrderByDescending(g => g.Count());
        var sb = new System.Text.StringBuilder();
        foreach (var g in groups)
        {
            sb.Append($"\n{g.Key}: {g.Count()}");
            foreach (var row in g.Take(ConsoleSamples)) sb.Append($"\n    {row.line}");
            if (g.Count() > ConsoleSamples) sb.Append($"\n    … ещё {g.Count() - ConsoleSamples}");
        }
        return sb.ToString();
    }
}
