using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>
/// Валидация реального файла сохранения — «стоп-кран перед пилорамой».
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
/// Полный список проблем пишется в test-results/save-validation.log; в консоль
/// идёт только сводка — сейв содержит 100+ деталей.
/// </summary>
public class SaveValidationTests
{
    private const string SaveFileName = "example.save.json";
    private const string ReportFileName = "save-validation.log";

    /// <summary>Сколько строк каждой категории печатать в консоль (остальное — в файл).</summary>
    private const int ConsoleSamples = 5;

    /// <summary>Допуск проверки кратности, мм. На порядок мельче самого мелкого
    /// реального отклонения (0.07 мм) и на два порядка крупнее точности float32
    /// на координатах ~10 м (≈0.001 мм), так что ложных срабатываний не даёт.</summary>
    private const float GridEpsMm = 0.01f;

    private string _json = "";
    private string _reportPath = "";
    private ProjectLoadStateGuard? _guard;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "../docs", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        _json = File.ReadAllText(fullPath);
        Assert.IsNotEmpty(_json);

        var dir = Path.Combine(Application.dataPath, "../test-results");
        Directory.CreateDirectory(dir);
        _reportPath = Path.Combine(dir, ReportFileName);
        // Один отчёт на весь класс: каждый тест дописывает свою секцию.
        File.WriteAllText(_reportPath, $"# Валидация {SaveFileName}\n");
    }

    /// <summary>Загрузка сейва переписывает ГЛОБАЛЬНОЕ состояние целиком: блок
    /// настроек (KitchenSettings — синглтон-ассет, включая ObjectsVisible /
    /// HideLightSources), режим ручек (ResizeHandleManager.Mode) и статики
    /// LightSourceElement.GlobalOn и ElementHighlighter.TintEnabled — RestoreScene
    /// выставляет их из файла проекта. Точечного сохранения пары флагов мало: в
    /// полном прогоне это роняло SceneVisibilityTests, SettingsPanelUITests, а
    /// тонировка утекала в эталоны SnapshotTests. Снимаем и возвращаем всё
    /// целиком, как это делает SnapshotTests.</summary>
    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);

        _guard = ProjectLoadStateGuard.Capture();

        s.AutoSave = false;
        s.SpatialGrid = false;
        s.EdgeOutline = false;

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
    public void Analyze_ExampleSave_ReportsNoIssues()
    {
        RestoreScene();
        var issues = SceneAnalyzer.Analyze();

        var lines = issues
            .OrderBy(i => i.Code, System.StringComparer.Ordinal)
            .ThenBy(i => i.Detail, System.StringComparer.Ordinal)
            .Select(i => $"{i.Level,-7} {i.Code,-7} {i.Detail,-48} {i.Message}")
            .ToList();
        Report("Правила SceneAnalyzer", lines.Count > 0 ? lines : new List<string> { "чисто" });

        if (issues.Count == 0) return;

        int errors = issues.Count(i => i.Level == IssueLevel.Error);
        int warnings = issues.Count(i => i.Level == IssueLevel.Warning);
        Assert.Fail($"Проблемы в {SaveFileName}: {issues.Count} "
            + $"(errors={errors}, warnings={warnings}).\n"
            + Summarize(issues.Select(i => (i.Code, $"{i.Detail}: {i.Message}")))
            + $"\nПолный список: {_reportPath}");
    }

    // ── Тест 2: инвариант мм-сетки ──────────────────────────────────────

    /// <summary>Проверяется координата ГРАНИ, а не центра: у детали 1967 мм центр
    /// обязан лежать на .5 — это норма, а вот боковина 16 мм с центром 1200.5 мм
    /// стоит гранями на 1192.5 и 1208.5, и в такой проём ни одна целочисленная
    /// деталь уже не встанет. Именно так родился зазор 0.5 мм в цоколе.</summary>
    [Test]
    public void Geometry_ExampleSave_FacesOnMillimeterGrid()
    {
        var elements = RestoreScene();
        const string catHalf = "ровно .5 мм";
        const string catTenth = "дробь на 0.1-мм сетке";
        const string catDirt = "мимо 0.1-мм сетки";
        var axisNames = new[] { "X", "Y", "Z" };

        var found = new List<(string category, float devMm, string line)>();
        foreach (var e in elements)
        {
            if (e == null) continue;
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
    public void Geometry_ExampleSave_NoSubToleranceJoints()
    {
        var elements = RestoreScene();
        float toMm = 1f / AppConstants.MM_TO_UNITS;
        float deadBand = Tolerance.SnapEpsilon;                            // 0.01 мм
        float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS; // 0.5 мм

        var faces = new Dictionary<KitchenElement, KitchenElement.Face[]>();
        var boxes = new Dictionary<KitchenElement, (Vector3 min, Vector3 max)>();
        var parts = new List<KitchenElement>();
        foreach (var e in elements)
        {
            if (e == null || e is LightSourceElement) continue;
            parts.Add(e);
            faces[e] = e.GetFaces();
            boxes[e] = WorldBoundsUnits(e);
        }

        var joints = new List<(float absMm, string line)>();
        for (int i = 0; i < parts.Count; i++)
        {
            for (int j = i + 1; j < parts.Count; j++)
            {
                var a = parts[i];
                var b = parts[j];
                if (!BoxesWithin(boxes[a], boxes[b], contactDist)) continue;

                if (!WorstSubToleranceJoint(faces[a], faces[b], deadBand, contactDist,
                        out float gapUnits, out int faceIndex))
                    continue;

                float gapMm = gapUnits * toMm;
                string kind = gapMm > 0 ? "щель  " : "врезание";
                joints.Add((Mathf.Abs(gapMm),
                    $"{kind} {Mm(gapMm),9} мм   {a.PartName,-28} ↔ {b.PartName,-28} "
                    + $"(грань {FaceLabel(faceIndex)} у {a.PartName})"));
            }
        }

        var report = joints.OrderByDescending(j => j.absMm).Select(j => j.line).ToList();
        Report("Подпороговые стыки (0.01 … 0.5 мм)",
            report.Count > 0 ? report : new List<string> { "чисто" });

        if (joints.Count == 0) return;

        Assert.Fail($"Стыков в слепой зоне валидатора: {joints.Count}. "
            + "Валидатор их не видит: |зазор| ≤ Tolerance.ContactMm считается касанием.\n"
            + string.Join("\n", report.Take(ConsoleSamples * 4))
            + (report.Count > ConsoleSamples * 4 ? $"\n… ещё {report.Count - ConsoleSamples * 4}" : "")
            + $"\nПолный список: {_reportPath}");
    }

    // ── Тест 4: что меняет round-trip ───────────────────────────────────

    /// <summary>Диагностика, а не требование: сохранение пишет ЛОГИЧЕСКУЮ позу
    /// (Wall.FullPosition, ClosedPosition двери/фасада/ящика — ElementData.FromElement),
    /// поэтому у этих типов перезагрузка стирает накопленный дрейф. Тест меряет,
    /// у каких именно деталей это происходит, и не валится.</summary>
    [Test]
    public void RoundTrip_ExampleSave_ReportsPoseDrift()
    {
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

    private static readonly string[] FaceLabels = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };

    private static string FaceLabel(int index) =>
        index >= 0 && index < FaceLabels.Length ? FaceLabels[index] : "?";

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

    private static bool BoxesWithin((Vector3 min, Vector3 max) a, (Vector3 min, Vector3 max) b,
        float margin) =>
        a.min.x <= b.max.x + margin && a.max.x >= b.min.x - margin &&
        a.min.y <= b.max.y + margin && a.max.y >= b.min.y - margin &&
        a.min.z <= b.max.z + margin && a.max.z >= b.min.z - margin;

    /// <summary>Худший (по модулю) стык встречных граней в полосе (deadBand, maxDist].
    /// Знак: + щель, − врезание. Гейты те же, что у ConstraintValidator.MinParallelGap
    /// (параллельность, перекрытие ≥ MinSupportOverlap), но нормали обязаны быть
    /// ВСТРЕЧНЫМИ — только тогда расстояние между плоскостями имеет знак.</summary>
    private static bool WorstSubToleranceJoint(KitchenElement.Face[] fa, KitchenElement.Face[] fb,
        float deadBand, float maxDist, out float gapUnits, out int faceIndex)
    {
        gapUnits = 0f;
        faceIndex = -1;
        for (int a = 0; a < 6; a++)
        {
            for (int b = 0; b < 6; b++)
            {
                if (Vector3.Dot(fa[a].normal, fb[b].normal) > -Tolerance.ParallelDot) continue;

                float signed = Vector3.Dot(fb[b].center - fa[a].center, fa[a].normal);
                float abs = Mathf.Abs(signed);
                if (abs <= deadBand || abs > maxDist) continue;

                if (!FacesOverlap(fa[a], fb[b], out float ratio)) continue;
                if (ratio < Tolerance.MinSupportOverlap) continue;

                if (abs > Mathf.Abs(gapUnits)) { gapUnits = signed; faceIndex = a; }
            }
        }
        return faceIndex >= 0;
    }

    /// <summary>Перекрытие граней в плоскости — копия ConstraintValidator.FacesOverlap
    /// (там private). Полуосевое отношение, а не отношение площадей: узкие
    /// перпендикулярные грани иначе отсекались бы.</summary>
    private static bool FacesOverlap(KitchenElement.Face a, KitchenElement.Face b,
        out float overlapRatio)
    {
        Vector3 u = a.rightAxis;
        Vector3 v = a.upAxis;
        Rect aRect = FaceRect(a, u, v);
        Rect bRect = FaceRect(b, u, v);

        float interLeft = Mathf.Max(aRect.xMin, bRect.xMin);
        float interRight = Mathf.Min(aRect.xMax, bRect.xMax);
        float interBottom = Mathf.Max(aRect.yMin, bRect.yMin);
        float interTop = Mathf.Min(aRect.yMax, bRect.yMax);
        if (interLeft >= interRight || interBottom >= interTop)
        {
            overlapRatio = 0f;
            return false;
        }

        float ratioU = Mathf.Min(aRect.width, bRect.width) > 0
            ? (interRight - interLeft) / Mathf.Min(aRect.width, bRect.width) : 0f;
        float ratioV = Mathf.Min(aRect.height, bRect.height) > 0
            ? (interTop - interBottom) / Mathf.Min(aRect.height, bRect.height) : 0f;
        overlapRatio = ratioU * ratioV;
        return true;
    }

    private static Rect FaceRect(KitchenElement.Face face, Vector3 u, Vector3 v)
    {
        var center = new Vector2(Vector3.Dot(face.center, u), Vector3.Dot(face.center, v));
        float halfU = Mathf.Abs(Vector3.Dot(face.rightAxis, u)) * face.size.x * 0.5f
                    + Mathf.Abs(Vector3.Dot(face.upAxis, u)) * face.size.y * 0.5f;
        float halfV = Mathf.Abs(Vector3.Dot(face.rightAxis, v)) * face.size.x * 0.5f
                    + Mathf.Abs(Vector3.Dot(face.upAxis, v)) * face.size.y * 0.5f;
        return new Rect(center.x - halfU, center.y - halfV, halfU * 2f, halfV * 2f);
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
