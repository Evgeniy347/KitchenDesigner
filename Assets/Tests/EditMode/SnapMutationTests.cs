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
        // Transformable, а не Movable: открытая дверца и выдвинутый ящик строят
        // геометрию от закрытой позы, приложение их двигать и растягивать не даёт —
        // фаззеру тоже нечего там проверять.
        var movableNames = allElements.Where(e => e.Transformable && e.gameObject.activeInHierarchy)
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

            // Чего МЫ ждём от прилипания — считается независимо от ResizeSnap по
            // сырому (не прилипшему) положению грани. Без этого тест видит только
            // состоявшиеся снэпы, а пропущенное прилипание для него невидимо —
            // именно поэтому он молчал на стойке, касающейся панели по ребру.
            var expected = FindResizeTargets(f, rawDelta, others, thresholdMm);

            ResizeMath.Compute(savedDims, axis, f.normal, f.center, f.rightAxis, f.upAxis, f.size,
                savedPos, sizeStartUnits, rawDelta, others, moved,
                snapEnabled: true, thresholdMm * AppConstants.MM_TO_UNITS,
                out Vector3Int newDims, out Vector3 _, out bool snapped);

            if (!snapped && expected.Count > 0)
            {
                var nearest = expected.OrderBy(t => Mathf.Abs(t.gapMm)).First();
                AddError($"RESIZE-NOSNAP: {moved.PartName} {FaceLabels[faceIndex]} @ {mm}мм " +
                    $"грань в {Mathf.Abs(nearest.gapMm):F1}мм от {nearest.targetName} " +
                    $"({(nearest.edgeOnly ? "по ребру" : "по площади")}), но прилипание не сработало");
            }

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
            if (!snap.snapped)
            {
                // Пропущенное прилипание: есть встречная грань в пределах порога с
                // достаточным перекрытием, а снэпа нет. Без этой проверки тест
                // видел только СОСТОЯВШИЕСЯ снэпы и не мог поймать «не прилипает».
                var missed = FindMoveTargets(moved, others, testPos, thresholdMm);
                if (missed.Count > 0)
                {
                    var nearest = missed.OrderBy(t => Mathf.Abs(t.gapMm)).First();
                    AddError($"MOVE-NOSNAP: {moved.PartName} {dirLabel} @ {mm}мм " +
                        $"грань в {Mathf.Abs(nearest.gapMm):F1}мм от {nearest.targetName} " +
                        $"({(nearest.edgeOnly ? "по ребру" : "по площади")}), но прилипание не сработало");
                }
                continue;
            }

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

            // Деталь загнана ВНУТРЬ цели — снэп здесь разводит тела, а не выбирает
            // между кандидатами. Коллизии по условию задачи не рассматриваем.
            if (chosen.intersects) continue;

            int axis = chosen.movedFaceIndex / 2;

            // По оси, где деталь УЖЕ стоит заподлицо, конкуренции нет: любой сдвиг
            // вдоль неё разорвал бы существующий контакт, и TrySnap законно ищет
            // прилипание по другой оси. Полка, зажатая между стеной (0 мм) и ДВП
            // (1 мм), обязана остаться у стены — это не проигрыш конкуренции.
            bool axisAlreadyInContact = diag.neighbors.Any(
                n => n.movedFaceIndex >= 0 && n.gapMM >= 0f && n.gapMM <= 0.5f
                     && n.movedFaceIndex / 2 == axis);
            if (axisAlreadyInContact) continue;

            var rivals = diag.neighbors
                .Where(n => n.wouldSnap && n.gapMM > 0.5f && n.movedFaceIndex / 2 == axis)
                .ToList();
            if (rivals.Count > 1)
            {
                var bestByGap = rivals.OrderBy(n => n.gapMM).First();
                if (chosen.gapMM > bestByGap.gapMM + 5.0f)
                {
                    // Конкуренция за одну ось при свободной оси и без коллизии —
                    // это уже ошибка выбора, а не допущение модели.
                    AddError($"MOVE-COMPETITION: {moved.PartName} {dirLabel} @ {mm}мм " +
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

    private readonly struct ResizeTarget
    {
        public readonly string targetName;
        public readonly float gapMm;      // со знаком, вдоль нормали растягиваемой грани
        public readonly bool edgeOnly;    // соприкосновение ровно по ребру/углу
        public ResizeTarget(string name, float gap, bool edgeOnly)
        {
            targetName = name;
            gapMm = gap;
            this.edgeOnly = edgeOnly;
        }
    }

    /// <summary>Грани, к которым растягиваемая грань ОБЯЗАНА прилипнуть: нормали
    /// противоположны, плоскость в пределах порога вдоль нормали, а footprint'ы
    /// в плоскости грани соприкасаются — включая касание ровно по ребру (нулевая
    /// площадь пересечения) и по углу.
    ///
    /// Считается по СЫРОМУ положению грани (faceCenter + normal*rawDelta), то есть
    /// ровно там, куда её тянет пользователь до вмешательства снэпа. Реализацию
    /// ResizeSnap намеренно не повторяет: это независимая формулировка требования,
    /// иначе тест не смог бы поймать ошибку в самой реализации.
    ///
    /// Грани пазов не учитываются — они дают ДОПОЛНИТЕЛЬНЫЕ детенты, от их
    /// отсутствия здесь возможен только пропуск ошибки, но не ложная.</summary>
    private List<ResizeTarget> FindResizeTargets(KitchenElement.Face movedFace, float rawDelta,
        List<KitchenElement> others, float thresholdMm)
    {
        var result = new List<ResizeTarget>();
        float threshold = thresholdMm * AppConstants.MM_TO_UNITS;
        Vector3 center = movedFace.center + movedFace.normal * rawDelta;
        Vector3 u = movedFace.rightAxis, v = movedFace.upAxis;
        Rect mRect = FaceRect(center, u, v, u, v, movedFace.size.x, movedFace.size.y);

        foreach (var o in others)
        {
            if (!o.gameObject.activeInHierarchy) continue;
            foreach (var of in GetFacesCached(o))
            {
                // Двусторонняя плоскость: встык к ближней грани ИЛИ заподлицо с
                // дальней — оба детента обязаны работать при ресайзе.
                if (Mathf.Abs(Vector3.Dot(of.normal, movedFace.normal)) < Tolerance.ParallelDot) continue;

                float gap = Vector3.Dot(of.center - center, movedFace.normal);
                if (Mathf.Abs(gap) > threshold + Tolerance.SnapEpsilon) continue;

                Rect oRect = FaceRect(of.center, u, v, of.rightAxis, of.upAxis, of.size.x, of.size.y);
                float left = Mathf.Max(mRect.xMin, oRect.xMin);
                float right = Mathf.Min(mRect.xMax, oRect.xMax);
                float bottom = Mathf.Max(mRect.yMin, oRect.yMin);
                float top = Mathf.Min(mRect.yMax, oRect.yMax);
                if (left > right + Tolerance.SnapEpsilon || bottom > top + Tolerance.SnapEpsilon) continue;

                bool edgeOnly = right - left <= Tolerance.SnapEpsilon || top - bottom <= Tolerance.SnapEpsilon;
                result.Add(new ResizeTarget(o.PartName, gap / AppConstants.MM_TO_UNITS, edgeOnly));
            }
        }
        return result;
    }

    /// <summary>Грани, к которым деталь в позиции testPos ОБЯЗАНА прилипнуть при
    /// перемещении: нормали противоположны, зазор в пределах порога, перекрытие
    /// граней не меньше минимума (касание по ребру считается полным выравниванием
    /// по этой оси — как в SnapSystem.FacesOverlap).
    ///
    /// Исключаются два случая, где отказ от снэпа законен:
    ///  • деталь глубоко внутри соседа — снэп не имеет права загонять центр внутрь
    ///    габарита (та же проверка, что в Collect);
    ///  • контакт с нулевым зазором — тогда TrySnap возвращает подтверждение
    ///    контакта и snapped уже true, сюда мы просто не попадаем.</summary>
    private List<ResizeTarget> FindMoveTargets(KitchenElement moved, List<KitchenElement> others,
        Vector3 testPos, float thresholdMm)
    {
        var result = new List<ResizeTarget>();
        float threshold = thresholdMm * AppConstants.MM_TO_UNITS;

        var prevPos = moved.transform.position;
        moved.transform.position = testPos;
        var movedFaces = moved.GetFaces();

        foreach (var o in others)
        {
            if (!o.gameObject.activeInHierarchy) continue;

            var ov = o.GetVertices();
            Vector3 oMin = ov[0], oMax = ov[0];
            for (int k = 1; k < 8; k++) { oMin = Vector3.Min(oMin, ov[k]); oMax = Vector3.Max(oMax, ov[k]); }

            foreach (var of in GetFacesCached(o))
                foreach (var mf in movedFaces)
                {
                    if (Vector3.Dot(of.normal, mf.normal) > -Tolerance.ParallelDot) continue;

                    float gap = Vector3.Dot(of.center - mf.center, mf.normal);
                    if (Mathf.Abs(gap) > threshold + Tolerance.SnapEpsilon) continue;
                    if (Mathf.Abs(gap) <= Tolerance.EpsilonUnits) continue; // контакт — TrySnap его подтвердит

                    Vector3 u = mf.rightAxis, v = mf.upAxis;
                    Rect mRect = FaceRect(mf.center, u, v, u, v, mf.size.x, mf.size.y);
                    Rect oRect = FaceRect(of.center, u, v, of.rightAxis, of.upAxis, of.size.x, of.size.y);

                    float left = Mathf.Max(mRect.xMin, oRect.xMin), right = Mathf.Min(mRect.xMax, oRect.xMax);
                    float bottom = Mathf.Max(mRect.yMin, oRect.yMin), top = Mathf.Min(mRect.yMax, oRect.yMax);
                    if (left > right + Tolerance.SnapEpsilon || bottom > top + Tolerance.SnapEpsilon) continue;

                    float overlapU = Mathf.Max(0, right - left), overlapV = Mathf.Max(0, top - bottom);
                    float minW = Mathf.Min(mRect.width, oRect.width), minH = Mathf.Min(mRect.height, oRect.height);
                    float ratioU = overlapU <= Tolerance.SnapEpsilon ? 1f : (minW > 0 ? overlapU / minW : 0f);
                    float ratioV = overlapV <= Tolerance.SnapEpsilon ? 1f : (minH > 0 ? overlapV / minH : 0f);
                    if (ratioU * ratioV < Tolerance.MinSupportOverlap) continue;

                    // Снэп сдвинул бы центр внутрь габарита соседа — законный отказ.
                    Vector3 snapPos = testPos + gap * mf.normal;
                    if (snapPos.x > oMin.x && snapPos.x < oMax.x &&
                        snapPos.y > oMin.y && snapPos.y < oMax.y &&
                        snapPos.z > oMin.z && snapPos.z < oMax.z) continue;

                    bool edgeOnly = overlapU <= Tolerance.SnapEpsilon || overlapV <= Tolerance.SnapEpsilon;
                    result.Add(new ResizeTarget(o.PartName, gap / AppConstants.MM_TO_UNITS, edgeOnly));
                }
        }

        moved.transform.position = prevPos;
        return result;
    }

    /// <summary>Прямоугольник грани в осях (u,v) — та же проекция, что в
    /// SnapSystem.GetFaceRect и ResizeSnap.RectFor.</summary>
    private static Rect FaceRect(Vector3 center, Vector3 u, Vector3 v,
        Vector3 rightAxis, Vector3 upAxis, float sizeX, float sizeY)
    {
        float cu = Vector3.Dot(center, u);
        float cv = Vector3.Dot(center, v);
        float halfU = Mathf.Abs(Vector3.Dot(rightAxis, u)) * sizeX * 0.5f
                    + Mathf.Abs(Vector3.Dot(upAxis, u)) * sizeY * 0.5f;
        float halfV = Mathf.Abs(Vector3.Dot(rightAxis, v)) * sizeX * 0.5f
                    + Mathf.Abs(Vector3.Dot(upAxis, v)) * sizeY * 0.5f;
        return new Rect(cu - halfU, cv - halfV, halfU * 2f, halfV * 2f);
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

    /// <summary>Ближайшая параллельная грань к movedFace в пределах порога.
    /// Плоскость двусторонняя: годится и встречная (встык), и со-направленная
    /// (заподлицо с дальней кромкой) — ресайз использует обе. Проверяем только
    /// параллельность нормалей и зазор; перекрытие не требуем — это «конкуренция
    /// по расстоянию», а не полноценный снэп.</summary>
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
                if (Mathf.Abs(Vector3.Dot(movedFace.normal, of.normal)) < Tolerance.ParallelDot) continue;
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
                if (Mathf.Abs(Vector3.Dot(movedFace.normal, of.normal)) < Tolerance.ParallelDot) continue;
                float gap = Mathf.Abs(Vector3.Dot(of.center - movedFace.center, movedFace.normal));
                if (gap > threshold + Tolerance.SnapEpsilon) continue;
                list.Add(new FaceCandidate(o.PartName, gap / AppConstants.MM_TO_UNITS, true));
            }
        }
        return list;
    }
}
