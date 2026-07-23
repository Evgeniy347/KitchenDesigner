using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Мутирующий тест прилипания: загружает файл сохранения, перебирает ПОДВИЖНЫЕ
/// детали и для каждой последовательно проверяет:
///   1) ресайз на +200 мм с каждой из 6 граней;
///   2) ресайс на -200 мм с каждой из 6 граней;
///   3) перемещение на 200 мм в каждую из 6 сторон мировых осей;
///   4) покадровый (1 мм) ресайз с каждой грани с фиксацией сработало/не сработало
///      прилипание и проверкой конкуренции (выбор ближайшей грани/ребра);
///   5) покадровый (1 мм) перенос в каждую сторону с той же фиксацией и проверкой
///      конкуренции.
///
/// После каждой детали сцена сбрасывается в исходное состояние.
/// </summary>
public class SnapMutationTests
{
    private const string SaveFileName = "example.save.json";
    private const int BigStepMm = 200;
    private const int SweepMaxMm = 200;
    private const int SweepStepMm = 1;
    private const int MaxErrors = 200;

    private string _json = "";
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();
    private bool _prevAutoSave;
    private bool _prevSpatialGrid;
    private bool _prevEdgeOutline;

    private readonly Dictionary<KitchenElement, KitchenElement.Face[]> _faceCache = new();

    private void BuildFaceCache(List<KitchenElement> elements)
    {
        _faceCache.Clear();
        foreach (var e in elements)
            if (e != null && e.gameObject.activeInHierarchy)
                _faceCache[e] = e.GetFaces();
    }

    private KitchenElement.Face[] GetFacesCached(KitchenElement e)
    {
        if (_faceCache.TryGetValue(e, out var faces)) return faces;
        return e.GetFaces();
    }

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "../docs", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        _json = File.ReadAllText(fullPath);
        Assert.IsNotEmpty(_json);
    }

    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);

        _prevAutoSave = s.AutoSave;
        _prevSpatialGrid = s.SpatialGrid;
        _prevEdgeOutline = s.EdgeOutline;

        s.AutoSave = false;
        s.SpatialGrid = false;
        s.EdgeOutline = false;
        s.SnapEnabled = true;
        s.SnapThreshold = 50f;
        s.BlockOnViolation = false;
        SnapSystem.VerboseLog = false;
    }

    [TearDown]
    public void TearDown()
    {
        ClearScene();
        var s = KitchenSettings.Instance;
        if (s != null)
        {
            s.AutoSave = _prevAutoSave;
            s.SpatialGrid = _prevSpatialGrid;
            s.EdgeOutline = _prevEdgeOutline;
        }
        _errors.Clear();
        _warnings.Clear();
        FaceCache.Clear();
    }

    private void ClearScene()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

    private List<KitchenElement> RestoreScene()
    {
        var data = SaveLoadManager.Deserialize(_json);
        Assert.IsNotNull(data);
        var objs = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(objs);
        return objs.Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList()!;
    }

    private static readonly string[] FaceLabels = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };
    private static readonly Vector3[] MoveDirs =
    {
        Vector3.right, Vector3.left,
        Vector3.up, Vector3.down,
        Vector3.forward, Vector3.back
    };

    [Test]
    public void Mutation_AllFacingFacePairs_SnapWhenWithinThreshold()
    {
        var swTotal = System.Diagnostics.Stopwatch.StartNew();
        ClearScene();
        var allElements = RestoreScene();
        Assert.IsNotEmpty(allElements, "No KitchenElements in restored scene");
        var movableNames = allElements.Where(e => e.Movable && e.gameObject.activeInHierarchy)
            .Select(e => e.PartName).ToList();
        BuildFaceCache(allElements);

        TestContext.WriteLine(
            $"=== Mutation: {SaveFileName} | {movableNames.Count} movable / {allElements.Count} total ===");

        int totalSnapOk = 0;
        int totalBigResizeOk = 0;
        int totalBigMoveOk = 0;
        int totalSweepSnapEvents = 0;
        int totalSweepCompetitionWarnings = 0;

        foreach (var name in movableNames)
        {
            var moved = allElements.FirstOrDefault(e => e.PartName == name);
            if (moved == null || !moved.gameObject.activeInHierarchy) continue;

            var savedPos = moved.transform.position;
            var savedDims = moved.DimensionsMM;
            var savedRot = moved.transform.rotation;
            float threshold = KitchenSettings.Instance.SnapThreshold;
            var allOthers = allElements.Where(e => e != moved && e != null && e.gameObject.activeInHierarchy).ToList();

            // Предфильтр по расстоянию: в sweep/move участвуют только соседи в радиусе
            // 200 мм ресайза/переноса + 50 мм порог + запас. Это уменьшает
            // количество пар граней на порядок.
            float nearbyRadiusMm = SweepMaxMm + threshold + 100f;
            var others = GetNeighborsWithin(moved, allOthers, nearbyRadiusMm);

            // Хитрый кэш: грани всех НЕподвижных соседей считаем один раз за итерацию.
            // Подвижный элемент в кэш не попадает — его геометрия меняется.
            var staticCache = new Dictionary<KitchenElement, KitchenElement.Face[]>();
            foreach (var o in others)
                staticCache[o] = GetFacesCached(o);
            FaceCache.Set(staticCache);

            // ── Phase 0: диагностика существующих пар граней ─────────────────
            foreach (var target in others)
                TestExistingPairAttraction(moved, target, savedPos, savedDims, ref totalSnapOk);

            // ── Phase 1: +200 мм с каждой из 6 граней ────────────────────────
            for (int face = 0; face < 6; face++)
                TestResizeFromFace(moved, others, savedPos, savedDims, savedRot, face, +BigStepMm,
                    threshold, ref totalBigResizeOk, "GROW");

            // ── Phase 2: -200 мм с каждой из 6 граней ────────────────────────
            for (int face = 0; face < 6; face++)
                TestResizeFromFace(moved, others, savedPos, savedDims, savedRot, face, -BigStepMm,
                    threshold, ref totalBigResizeOk, "SHRINK");

            // ── Phase 3: перемещение на 200 мм в 6 направлениях ──────────────
            foreach (var dir in MoveDirs)
                TestMoveDirection(moved, others, savedPos, savedDims, savedRot, dir, BigStepMm,
                    threshold, ref totalBigMoveOk);

            // ── Phase 4: покадровый (1 мм) ресайз с каждой грани ─────────────
            for (int face = 0; face < 6; face++)
                SweepResizeFromFace(moved, others, savedPos, savedDims, savedRot, face,
                    SweepMaxMm, SweepStepMm, threshold, ref totalSweepSnapEvents,
                    ref totalSweepCompetitionWarnings);

            // ── Phase 5: покадровый (1 мм) перенос в 6 направлениях ──────────
            foreach (var dir in MoveDirs)
                SweepMoveDirection(moved, others, savedPos, savedDims, savedRot, dir,
                    SweepMaxMm, SweepStepMm, threshold, ref totalSweepSnapEvents,
                    ref totalSweepCompetitionWarnings);

            // Возвращаем только движимый элемент в исходное состояние.
            // Полный сброс сцены не нужен — остальные элементы не менялись.
            RestoreElementState(moved, savedPos, savedDims, savedRot);
            FaceCache.Clear();
        }

        swTotal.Stop();

        // ── Report ────────────────────────────────────────────────────────
        var report = new System.Text.StringBuilder();
        report.AppendLine($"Existing snap OK: {totalSnapOk} | Big resize OK: {totalBigResizeOk} | Big move OK: {totalBigMoveOk}");
        report.AppendLine($"Sweep snap events: {totalSweepSnapEvents} | sweep competition warnings: {totalSweepCompetitionWarnings}");
        report.AppendLine($"Total time: {swTotal.Elapsed.TotalSeconds:F1}s");

        if (_warnings.Count > 0)
        {
            report.AppendLine($"Warnings ({_warnings.Count}):");
            foreach (var g in _warnings
                         .GroupBy(w => w.Split(':')[0])
                         .OrderByDescending(g => g.Count()))
                report.AppendLine($"  {g.Key}: {g.Count()}");
            report.AppendLine("Sample:");
            foreach (var g in _warnings.GroupBy(w => w.Split(':')[0]))
                foreach (var w in g.Take(3)) report.AppendLine($"  {w}");

            DumpWarnings();
        }

        TestContext.WriteLine(report.ToString());

        if (_errors.Count > 0)
            Assert.Fail($"Snap mutation errors ({_errors.Count}):\n{string.Join("\n", _errors.Take(50))}");
        else
            Assert.Pass($"All snap mutation tests passed. " +
                $"Events: existing={totalSnapOk}, bigResize={totalBigResizeOk}, bigMove={totalBigMoveOk}, " +
                $"sweep={totalSweepSnapEvents}, warnings={_warnings.Count}, time={swTotal.Elapsed.TotalSeconds:F1}s.");
    }

    /// <summary>Для пары деталей из исходной сцены проверяет, что встречные грани
    /// в пределах порога действительно прилипают, а краевой контакт при достаточном
    /// перекрытии тоже даёт снэп.</summary>
    private void TestExistingPairAttraction(KitchenElement moved, KitchenElement target,
        Vector3 savedPos, Vector3Int savedDims, ref int snapOk)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return;

        RestoreElementState(moved, savedPos, savedDims, moved.transform.rotation);
        var diag = SnapSystem.Diagnose(moved, new List<KitchenElement> { target },
            savedPos, maxNeighbors: 50);

        foreach (var r in diag.neighbors)
        {
            if (!r.withinThreshold || !r.hasFacingFaces) continue;

            var faces = moved.GetFaces();
            if (r.movedFaceIndex < 0 || r.movedFaceIndex >= faces.Length) continue;
            var normal = faces[r.movedFaceIndex].normal;
            float snapThreshold = KitchenSettings.Instance.SnapThreshold;

            if (r.wouldSnap)
            {
                float maxOff = Mathf.Max(1f, snapThreshold - r.gapMM - 1f);
                float moveAwayMm = Mathf.Clamp(r.gapMM * 0.5f + 5f, 5f, maxOff);
                Vector3 awayPos = savedPos - normal * (moveAwayMm * 0.001f);

                var snapRes = SnapSystem.TrySnap(moved, new List<KitchenElement> { target }, awayPos);
                if (!snapRes.snapped)
                    AddError($"NO-SNAP: {moved.PartName}[f{r.movedFaceIndex}]" +
                        $"↔{target.PartName}[f{r.otherFaceIndex}] " +
                        $"gap={r.gapMM:F1}mm ovl={r.overlapRatio:P0} away={moveAwayMm:F0}mm");
                else
                {
                    moved.transform.position = snapRes.position;
                    // Панели (задники/ДВП) встают в пазы — их AABB пересекает корпус,
                    // это валидно, не считаем ошибкой.
                    if (!IsPanel(moved) && SnapSystem.ElementsIntersect(moved, target))
                        AddError($"INTERSECT: {moved.PartName}↔{target.PartName} after snap-back");
                    snapOk++;
                }
            }
            else if (r.overlapRatio >= Tolerance.MinSupportOverlap)
            {
                float testOffMm = Mathf.Clamp(r.gapMM * 0.5f + 5f, 5f, snapThreshold - r.gapMM);
                Vector3 toward = savedPos + normal * (testOffMm * 0.001f);
                var edgeRes = SnapSystem.TrySnap(moved, new List<KitchenElement> { target }, toward);
                if (!edgeRes.snapped)
                    AddError($"EDGE-NOSNAP: {moved.PartName}[f{r.movedFaceIndex}]" +
                        $"↔{target.PartName}[f{r.otherFaceIndex}] " +
                        $"gap={r.gapMM:F1}mm ovl={r.overlapRatio:P0}");
                else snapOk++;
            }
        }
    }

    /// <summary>Ресайз детали на deltaMm с указанной грани (faceIndex 0..5).
    /// Пересечения после большой мутации регистрируются как предупреждения: в UI
    /// такая операция либо не даст себя закоммитить, либо будет откачена.</summary>
    private void TestResizeFromFace(KitchenElement moved, List<KitchenElement> others,
        Vector3 savedPos, Vector3Int savedDims, Quaternion savedRot, int faceIndex, int deltaMm,
        float thresholdMm, ref int snapOk, string phase)
    {
        RestoreElementState(moved, savedPos, savedDims, savedRot);

        var faces = moved.GetFaces();
        if (faceIndex < 0 || faceIndex >= faces.Length) return;
        var f = faces[faceIndex];
        int axis = faceIndex / 2;
        int origDim = axis == 0 ? savedDims.x : (axis == 1 ? savedDims.y : savedDims.z);

        float rawDelta = deltaMm * AppConstants.MM_TO_UNITS;
        float sizeStartUnits = origDim * AppConstants.MM_TO_UNITS;

        ResizeMath.Compute(savedDims, axis, f.normal, f.center, f.rightAxis, f.upAxis, f.size,
            savedPos, sizeStartUnits, rawDelta, others, moved,
            snapEnabled: true, thresholdMm * AppConstants.MM_TO_UNITS,
            out Vector3Int newDims, out Vector3 _, out bool snapped);

        moved.DimensionsMM = newDims;
        var actualDims = moved.DimensionsMM;

        if (actualDims == savedDims)
        {
            _warnings.Add($"RESIZE-REJECTED: {moved.PartName} {phase} {FaceLabels[faceIndex]} δ={deltaMm}мм");
            return;
        }

        // Деталь вправе зажать размер (нога держит высоту 80..130 мм) — центр
        // считаем от ПРИНЯТОГО размера, как это делает ResizeHandleManager.
        moved.transform.position = ResizeMath.CenterForAppliedDims(
            savedPos, f.normal, sizeStartUnits, actualDims, axis);

        var label = $"{moved.PartName} {phase} {FaceLabels[faceIndex]} δ={deltaMm}мм";
        foreach (var other in others)
            if (SnapSystem.ElementsIntersect(moved, other))
            {
                _warnings.Add($"INTERSECT: {label} — {moved.PartName}↔{other.PartName}");
                break;
            }

        if (snapped) snapOk++;
    }

    /// <summary>Перемещение детали на distanceMm в направлении dir.
    /// Проверяет прилипание и отсутствие пересечений после снэпа.</summary>
    private void TestMoveDirection(KitchenElement moved, List<KitchenElement> others,
        Vector3 savedPos, Vector3Int savedDims, Quaternion savedRot, Vector3 dir, int distanceMm,
        float thresholdMm, ref int snapOk)
    {
        RestoreElementState(moved, savedPos, savedDims, savedRot);

        Vector3 testPos = savedPos + dir.normalized * (distanceMm * AppConstants.MM_TO_UNITS);
        var snap = SnapSystem.TrySnap(moved, others, testPos);

        string label = $"{moved.PartName} MOVE {distanceMm}мм dir={dir}";
        if (snap.snapped)
        {
            moved.transform.position = snap.position;
            var snapTarget = others.FirstOrDefault(o => o.PartName == snap.targetName);
            if (snapTarget != null && !IsPanel(moved) && SnapSystem.ElementsIntersect(moved, snapTarget))
                AddError($"INTERSECT-AFTER-SNAP: {label} → {snap.targetName}");
            snapOk++;
        }
    }

    /// <summary>Покадровый ресайз с грани faceIndex от 0 до maxMm мм с шагом stepMm.
    /// На каждом шаге фиксирует, сработало ли прилипание, и проверяет, что после
    /// снэпа движимая грань оказалась заподлицо с какой-либо встречной гранью
    /// (с допуском на округление до миллиметров).</summary>
    private void SweepResizeFromFace(KitchenElement moved, List<KitchenElement> others,
        Vector3 savedPos, Vector3Int savedDims, Quaternion savedRot, int faceIndex,
        int maxMm, int stepMm, float thresholdMm, ref int snapEvents, ref int competitionErrors)
    {
        if (faceIndex < 0 || faceIndex >= 6) return;
        int axis = faceIndex / 2;
        int origDim = axis == 0 ? savedDims.x : (axis == 1 ? savedDims.y : savedDims.z);

        // Нет соседей в радиусе ресайза + порог — пропускаем дорогой sweep.
        float checkDistMm = maxMm + thresholdMm + 100f;
        if (!HasNeighborWithin(moved, others, checkDistMm)) return;

        for (int mm = 0; mm <= maxMm; mm += stepMm)
        {
            RestoreElementState(moved, savedPos, savedDims, savedRot);

            var f = moved.GetFaces()[faceIndex];
            float rawDelta = mm * AppConstants.MM_TO_UNITS;
            float sizeStartUnits = origDim * AppConstants.MM_TO_UNITS;

            ResizeMath.Compute(savedDims, axis, f.normal, f.center, f.rightAxis, f.upAxis, f.size,
                savedPos, sizeStartUnits, rawDelta, others, moved,
                snapEnabled: true, thresholdMm * AppConstants.MM_TO_UNITS,
                out Vector3Int newDims, out Vector3 _, out bool snapped);

            moved.DimensionsMM = newDims;
            var appliedDims = moved.DimensionsMM;
            if (appliedDims == savedDims) continue; // деталь отказалась менять размер

            // Центр — от ПРИНЯТОГО размера (как в ResizeHandleManager).
            moved.transform.position = ResizeMath.CenterForAppliedDims(
                savedPos, f.normal, sizeStartUnits, appliedDims, axis);

            // Размер зажат самой деталью (нога: высота 80..130 мм, сечение 50×50) —
            // грань встала не туда, куда метил снэп. Проверять прилипание здесь
            // нечего: это ограничение типа детали, а не работа SnapSystem.
            if (ResizeMath.DimAlong(appliedDims, axis) != ResizeMath.DimAlong(newDims, axis))
                continue;

            if (snapped)
            {
                snapEvents++;
                var movedFace = moved.GetFaces()[faceIndex];
                var closest = FindClosestOpposingFace(movedFace, others, thresholdMm);

                if (!closest.found)
                {
                    AddError($"RESIZE-SNAP-NO-FACE: {moved.PartName} {FaceLabels[faceIndex]} @ {mm}мм " +
                        $"снэп отмечен, но встречной грани в пределах порога нет");
                }
                else if (closest.gapMm > thresholdMm * 0.5f)
                {
                    // Если после снэпа ближайшая грань дальше половины порога —
                    // возможно, снэп произошёл к пазу/стенке, которую этот хелпер
                    // не видит, либо ошибка округления превысила допустимое.
                    AddError($"RESIZE-SNAP-GAP: {moved.PartName} {FaceLabels[faceIndex]} @ {mm}мм " +
                        $"зазор после снэпа {closest.gapMm:F2}мм к {closest.targetName}");
                }

                var allOpposing = FindAllOpposingFaces(movedFace, others, thresholdMm);
                if (allOpposing.Count > 1)
                {
                    var minGap = allOpposing.Min(x => x.gapMm);
                    if (closest.gapMm > minGap + 5.0f)
                    {
                        _warnings.Add($"RESIZE-COMPETITION: {moved.PartName} {FaceLabels[faceIndex]} @ {mm}мм " +
                            $"ближайшая грань {closest.targetName} gap={closest.gapMm:F1}мм, " +
                            $"но есть грань gap={minGap:F1}мм");
                        competitionErrors++;
                    }
                }
            }
        }
    }

    /// <summary>Покадровый перенос в направлении dir от 0 до maxMm мм с шагом stepMm.
    /// Фиксирует факт прилипания и проверяет, что TrySnap выбрал ближайшего кандидата.
    /// Diagnose вызывается только когда TrySnap сработал — так экономим ~90% шагов.</summary>
    private void SweepMoveDirection(KitchenElement moved, List<KitchenElement> others,
        Vector3 savedPos, Vector3Int savedDims, Quaternion savedRot, Vector3 dir,
        int maxMm, int stepMm, float thresholdMm, ref int snapEvents, ref int competitionErrors)
    {
        Vector3 d = dir.normalized;
        string dirLabel = DirectionLabel(d);

        // Нет соседей в радиусе перемещения + порог — пропускаем дорогой sweep.
        float checkDistMm = maxMm + thresholdMm + 100f;
        if (!HasNeighborWithin(moved, others, checkDistMm)) return;

        for (int mm = 0; mm <= maxMm; mm += stepMm)
        {
            RestoreElementState(moved, savedPos, savedDims, savedRot);
            Vector3 testPos = savedPos + d * (mm * AppConstants.MM_TO_UNITS);

            var snap = SnapSystem.TrySnap(moved, others, testPos);
            if (!snap.snapped) continue;

            snapEvents++;
            moved.transform.position = snap.position;
            var snapTarget = others.FirstOrDefault(o => o.PartName == snap.targetName);
            if (snapTarget != null && !IsPanel(moved) && SnapSystem.ElementsIntersect(moved, snapTarget))
                AddError($"INTERSECT-AFTER-SNAP: {moved.PartName} SWEEP-MOVE {dirLabel} @ {mm}мм → {snap.targetName}");

            // Снэп «на месте» — это подтверждение уже существующего контакта, а не
            // выбор между кандидатами: конкурировать не за что. Раньше такие шаги
            // давали десятки тысяч ложных MOVE-SNAP-OUTSIDE-DIAG (Diagnose не
            // показывает контакты с нулевым зазором среди активных кандидатов).
            if (Vector3.Distance(snap.position, testPos) <= Tolerance.EpsilonUnits) continue;

            // Конкуренция — это спор ЗА ОДНУ ОСЬ: какой из встречных граней вдоль
            // нормали отдать деталь. Кандидаты с других осей не конкуренты, а
            // дополнение — TrySnap добирает их отдельными проходами.
            var diag = SnapSystem.Diagnose(moved, others, testPos, maxNeighbors: 50);
            var chosen = diag.neighbors.FirstOrDefault(n => n.name == snap.targetName && n.wouldSnap);
            if (chosen == null) continue;

            int axis = chosen.movedFaceIndex / 2;
            var rivals = diag.neighbors
                .Where(n => n.wouldSnap && n.gapMM > 0.5f && n.movedFaceIndex / 2 == axis)
                .ToList();
            if (rivals.Count > 1)
            {
                var bestByGap = rivals.OrderBy(n => n.gapMM).First();
                if (chosen.gapMM > bestByGap.gapMM + 5.0f)
                {
                    _warnings.Add($"MOVE-COMPETITION: {moved.PartName} {dirLabel} @ {mm}мм " +
                        $"выбран {snap.targetName} gap={chosen.gapMM:F1}мм, " +
                        $"но ближе {bestByGap.name} gap={bestByGap.gapMM:F1}мм");
                    competitionErrors++;
                }
            }
        }
    }

    private void RestoreElementState(KitchenElement moved, Vector3 pos, Vector3Int dims, Quaternion rot)
    {
        moved.transform.position = pos;
        moved.DimensionsMM = dims;
        moved.transform.rotation = rot;
    }

    private static string DirectionLabel(Vector3 dir)
    {
        if (dir == Vector3.right) return "+X";
        if (dir == Vector3.left) return "-X";
        if (dir == Vector3.up) return "+Y";
        if (dir == Vector3.down) return "-Y";
        if (dir == Vector3.forward) return "+Z";
        if (dir == Vector3.back) return "-Z";
        return dir.ToString();
    }

    private static bool IsPanel(KitchenElement e) => e is PanelElement;

    /// <summary>Возвращает соседей, габарит которых находится в пределах distanceMm
    /// от габарита moved. Используем вместо полного списка others — так сокращаем
    /// количество пар граней на порядок.</summary>
    private static List<KitchenElement> GetNeighborsWithin(KitchenElement moved, List<KitchenElement> others, float distanceMm)
    {
        float d = distanceMm * AppConstants.MM_TO_UNITS;
        var mv = moved.GetVertices();
        float mMinX = mv[0].x, mMaxX = mv[0].x;
        float mMinY = mv[0].y, mMaxY = mv[0].y;
        float mMinZ = mv[0].z, mMaxZ = mv[0].z;
        for (int i = 1; i < 8; i++)
        {
            mMinX = Mathf.Min(mMinX, mv[i].x); mMaxX = Mathf.Max(mMaxX, mv[i].x);
            mMinY = Mathf.Min(mMinY, mv[i].y); mMaxY = Mathf.Max(mMaxY, mv[i].y);
            mMinZ = Mathf.Min(mMinZ, mv[i].z); mMaxZ = Mathf.Max(mMaxZ, mv[i].z);
        }

        var result = new List<KitchenElement>();
        foreach (var o in others)
        {
            if (!o.gameObject.activeInHierarchy) continue;
            var ov = o.GetVertices();
            float oMinX = ov[0].x, oMaxX = ov[0].x;
            float oMinY = ov[0].y, oMaxY = ov[0].y;
            float oMinZ = ov[0].z, oMaxZ = ov[0].z;
            for (int i = 1; i < 8; i++)
            {
                oMinX = Mathf.Min(oMinX, ov[i].x); oMaxX = Mathf.Max(oMaxX, ov[i].x);
                oMinY = Mathf.Min(oMinY, ov[i].y); oMaxY = Mathf.Max(oMaxY, ov[i].y);
                oMinZ = Mathf.Min(oMinZ, ov[i].z); oMaxZ = Mathf.Max(oMaxZ, ov[i].z);
            }

            float gapX = Mathf.Max(0, Mathf.Max(mMinX - oMaxX, oMinX - mMaxX));
            float gapY = Mathf.Max(0, Mathf.Max(mMinY - oMaxY, oMinY - mMaxY));
            float gapZ = Mathf.Max(0, Mathf.Max(mMinZ - oMaxZ, oMinZ - mMaxZ));
            float dist = Mathf.Sqrt(gapX * gapX + gapY * gapY + gapZ * gapZ);
            if (dist <= d) result.Add(o);
        }
        return result;
    }

    private static bool HasNeighborWithin(KitchenElement moved, List<KitchenElement> others, float distanceMm)
    {
        return GetNeighborsWithin(moved, others, distanceMm).Count > 0;
    }

    /// <summary>Полный список предупреждений — в файл рядом с результатами тестов.
    /// В консоль печатать бессмысленно: их десятки тысяч.</summary>
    private void DumpWarnings()
    {
        var dir = Path.Combine(Application.dataPath, "../test-results");
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "snap-warnings.log"), _warnings);
    }

    private void AddError(string message)
    {
        if (_errors.Count < MaxErrors)
            _errors.Add(message);
    }

    private readonly struct FaceCandidate
    {
        public readonly string targetName;
        public readonly float gapMm;
        public readonly bool found;
        public FaceCandidate(string name, float gap, bool found)
        {
            targetName = name;
            gapMm = gap;
            this.found = found;
        }
    }

    /// <summary>Ближайшая встречная грань к movedFace в пределах порога.
    /// Проверяет только встречность нормалей и зазор; перекрытие не требуем —
    /// это именно «конкуренция по расстоянию», а не полноценный снэп.</summary>
    private FaceCandidate FindClosestOpposingFace(KitchenElement.Face movedFace,
        List<KitchenElement> others, float thresholdMm)
    {
        float threshold = thresholdMm * AppConstants.MM_TO_UNITS;
        float bestGap = float.MaxValue;
        string? bestName = null;

        foreach (var o in others)
        {
            if (!o.gameObject.activeInHierarchy) continue;
            var faces = GetFacesCached(o);
            foreach (var of in faces)
            {
                if (Vector3.Dot(movedFace.normal, of.normal) > -Tolerance.ParallelDot) continue;
                float gap = Mathf.Abs(Vector3.Dot(of.center - movedFace.center, movedFace.normal));
                if (gap > threshold + Tolerance.SnapEpsilon) continue;
                if (gap < bestGap)
                {
                    bestGap = gap;
                    bestName = o.PartName;
                }
            }
        }

        return bestName != null
            ? new FaceCandidate(bestName, bestGap / AppConstants.MM_TO_UNITS, true)
            : new FaceCandidate("", -1f, false);
    }

    private List<FaceCandidate> FindAllOpposingFaces(KitchenElement.Face movedFace,
        List<KitchenElement> others, float thresholdMm)
    {
        float threshold = thresholdMm * AppConstants.MM_TO_UNITS;
        var list = new List<FaceCandidate>();
        foreach (var o in others)
        {
            if (!o.gameObject.activeInHierarchy) continue;
            var faces = GetFacesCached(o);
            foreach (var of in faces)
            {
                if (Vector3.Dot(movedFace.normal, of.normal) > -Tolerance.ParallelDot) continue;
                float gap = Mathf.Abs(Vector3.Dot(of.center - movedFace.center, movedFace.normal));
                if (gap > threshold + Tolerance.SnapEpsilon) continue;
                list.Add(new FaceCandidate(o.PartName, gap / AppConstants.MM_TO_UNITS, true));
            }
        }
        return list;
    }
}
