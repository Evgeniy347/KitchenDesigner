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
/// <remarks>
/// [Explicit] — в обычный прогон НЕ входит, запускается только прицельно и по
/// согласованию: `.\tools\unity.ps1 tests -Platform EditMode -Filter SnapMutationTests`.
///
/// Почему. Это перебор, а не регрессионный тест: 79 секунд из 115 у всего
/// EditMode — один класс против остальных 2150 тестов, которые вместе идут 36
/// секунд. В цикле «правка → проверка» он превращал десятисекундную проверку в
/// двухминутную.
///
/// Чем это оплачено и о чём помнить: перебор ловит дыры в снапе, которых не
/// видит ни один точечный тест, поэтому перед серьёзной правкой
/// SnapSystem/ResizeSnap его надо гонять руками. Ровно та же договорённость,
/// что и для полного набора PlayMode.
///
/// И отдельно — про фикстуру. Тест грузит docs/example.save.json — ЖИВОЙ файл
/// пользователя: автосохранение десктопа пишет в путь открытого проекта
/// (AutoSaveManager.cs:56), так что достаточно открыть его в приложении.
/// Однажды это стоило разбора «когда снап сломался»: в рабочей копии с 41
/// детали была снята блокировка, они въехали в перебор и дали 200 ошибок
/// NO-SNAP. С закоммиченной версией того же файла тест зелёный — в коде не было
/// ничего.
///
/// Ровно «200 ошибок» пришло потом ещё раз, и причина была другая — свип
/// требовал от винтовой опоры прилипания граней, которых отбор у неё не берёт
/// (см. SnapPullsAlongFace). Двести — это был ПОТОЛОК списка, а не счёт, и
/// потому две разные болезни выглядели одинаково. Потолка больше нет, а сводка
/// печатает разбивку по деталям: одно имя на весь список — код, много разных
/// имён — фикстура.
///
/// Поэтому: покраснел перебор — СНАЧАЛА сверьте `git status
/// docs/example.save.json`. И НИКОГДА не откатывайте этот файл, чтобы тест
/// позеленел: это работа пользователя, у `git checkout --` нет отмены.
/// Правильное решение — дать тесту ЗАМОРОЖЕННУЮ копию сцены, как сделано в
/// ValidationInvariantTests.
/// </remarks>
[Explicit("перебор на 79 с: гонять прицельно, по согласованию — см. remarks")]
public class SnapMutationTests
{
    private const string SaveFileName = "example.save.json";
    private const int BigStepMm = 200;
    private const int SweepMaxMm = 200;
    private const int SweepStepMm = 1;

    private string _json = "";
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();
    private ProjectLoadStateGuard? _guard;

    /// <summary>Ось крепления текущей детали — снимается один раз на деталь:
    /// поворот внутри свипа восстанавливается, а ToGeometry() строит грани.</summary>
    private Vector3 _mountNormal;

    /// <summary>Индекс грани крепления у текущей детали, −1 у всех остальных:
    /// грани детали внутри свипа не меняются, поворот и размер восстанавливаются
    /// на каждом шаге.</summary>
    private int _mountFaceIndex = -1;

    private readonly Dictionary<KitchenElement, Face[]> _faceCache = new();

    /// <summary>Соседи текущей детали по имени. Свип ищет цель снэпа по имени на
    /// каждом сработавшем шаге — линейный поиск по списку стоил заметно дороже
    /// самой проверки. Первое вхождение, как у прежнего FirstOrDefault.</summary>
    private readonly Dictionary<string, KitchenElement> _othersByName = new();

    private KitchenElement? OtherByName(string? name)
        => name != null && _othersByName.TryGetValue(name, out var e) ? e : null;

    /// <summary>Снимки соседей текущей детали. Соседи в свипе неподвижны, поэтому
    /// снимок строится ОДИН раз на деталь: пересборка на каждом миллиметре стоила
    /// дороже самого расчёта прилипания.</summary>
    private List<ElementGeometry> _othersGeo = new();

    private long _ticksRestore, _ticksDimSet, _ticksTrySnap, _ticksPosSet;
    private int _countRestore, _countDimSet, _countTrySnap, _countPosSet;

    private long _ticksFindMove, _ticksDiagnose, _ticksIntersect;
    private int _countFindMove, _countDiagnose, _countIntersect;
    private long _ticksResizeCompute, _ticksFindResize, _ticksOpposing;
    private int _countResizeCompute, _countFindResize, _countOpposing;

    private void BuildFaceCache(List<KitchenElement> elements)
    {
        _faceCache.Clear();
        foreach (var e in elements)
            if (e != null && e.gameObject.activeInHierarchy)
                _faceCache[e] = e.GetFaces();
    }

    private Face[] GetFacesCached(KitchenElement e)
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

        // Загрузка сейва переписывает блок настроек, режим ручек, свет и
        // тонировку целиком — точечного снимка трёх флагов не хватало.
        _guard = ProjectLoadStateGuard.Capture();

        s.AutoSave = false;
        s.SpatialGrid = false;
        s.NormalView.edgeOutline = false;
        s.SnapEnabled = true;
        s.SnapThreshold = 50f;
        s.BlockOnViolation = false;
        SnapSystem.VerboseLog = false;
    }

    [TearDown]
    public void TearDown()
    {
        KitchenElement.SuppressVisualRebuild = false;
        ClearScene();
        _guard?.Restore();
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
        var movableNames = allElements.Where(e => e.Transformable && e.gameObject.activeInHierarchy)
            .Select(e => e.PartName).ToList();
        BuildFaceCache(allElements);

        // Сцена уже построена — дальше мутируем только геометрию, и меш с
        // материалами никто не смотрит: прилипание работает по localScale и позе.
        // Пересборка меша на каждый миллиметр свипа была самой дорогой строчкой
        // фазы 4. Флаг снимается в TearDown, снимок сцены он не затрагивает.
        KitchenElement.SuppressVisualRebuild = true;

        TestContext.WriteLine(
            $"=== Mutation: {SaveFileName} | {movableNames.Count} movable / {allElements.Count} total ===");

        int totalSnapOk = 0;
        int totalBigResizeOk = 0;
        int totalBigMoveOk = 0;
        int totalSweepSnapEvents = 0;
        int totalSweepCompetitionWarnings = 0;

        long ticksP0 = 0, ticksP1 = 0, ticksP2 = 0, ticksP3 = 0, ticksP4 = 0, ticksP5 = 0;
        _ticksRestore = _ticksDimSet = _ticksTrySnap = _ticksPosSet = 0;
        _countRestore = _countDimSet = _countTrySnap = _countPosSet = 0;
        _ticksFindMove = _ticksDiagnose = _ticksIntersect = 0;
        _countFindMove = _countDiagnose = _countIntersect = 0;
        _ticksResizeCompute = _ticksFindResize = _ticksOpposing = 0;
        _countResizeCompute = _countFindResize = _countOpposing = 0;

        foreach (var name in movableNames)
        {
            var moved = allElements.FirstOrDefault(e => e.PartName == name);
            if (moved == null || !moved.gameObject.activeInHierarchy) continue;

            var savedPos = moved.transform.position;
            var savedDims = moved.DimensionsMM;
            var savedRot = moved.transform.rotation;
            _mountNormal = MountNormalOf(moved);
            _mountFaceIndex = MountFaceIndexOf(moved, _mountNormal);
            float threshold = KitchenSettings.Instance.SnapThreshold;
            var allOthers = allElements.Where(e => e != moved && e != null && e.gameObject.activeInHierarchy).ToList();

            float nearbyRadiusMm = SweepMaxMm + threshold + 100f;
            var others = GetNeighborsWithin(moved, allOthers, nearbyRadiusMm);

            var staticCache = new Dictionary<KitchenElement, Face[]>();
            _othersByName.Clear();
            foreach (var o in others)
            {
                staticCache[o] = GetFacesCached(o);
                if (!_othersByName.ContainsKey(o.PartName)) _othersByName[o.PartName] = o;
            }
            FaceCache.Set(staticCache);
            _othersGeo = others.ToGeometry();

            // ── Phase 0 ──
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var target in others)
                TestExistingPairAttraction(moved, target, savedPos, savedDims, ref totalSnapOk);
            ticksP0 += sw.ElapsedTicks;

            // Врезную технику за грань не тянут (ручек ресайза у неё нет, размеры
            // правятся в окне свойств), и её габаритная коробка — только бортик
            // на пласти: «высота» в свойствах описывает и то, что ушло внутрь
            // столешницы. Гнать по ней ресайз-фазы значит проверять сценарий,
            // которого в приложении нет. Перенос проверяем как у всех.
            bool resizable = ResizeHandleManager.SupportsHandleResize(moved);

            // ── Phase 1: +200 мм с каждой из 6 граней ──
            sw.Restart();
            if (resizable)
                for (int face = 0; face < 6; face++)
                    TestResizeFromFace(moved, others, savedPos, savedDims, savedRot, face, +BigStepMm,
                        threshold, ref totalBigResizeOk, "GROW");
            ticksP1 += sw.ElapsedTicks;

            // ── Phase 2: -200 мм с каждой из 6 граней ──
            sw.Restart();
            if (resizable)
                for (int face = 0; face < 6; face++)
                    TestResizeFromFace(moved, others, savedPos, savedDims, savedRot, face, -BigStepMm,
                        threshold, ref totalBigResizeOk, "SHRINK");
            ticksP2 += sw.ElapsedTicks;

            // ── Phase 3: перемещение на 200 мм в 6 направлениях ──
            sw.Restart();
            foreach (var dir in MoveDirs)
                TestMoveDirection(moved, others, savedPos, savedDims, savedRot, dir, BigStepMm,
                    threshold, ref totalBigMoveOk);
            ticksP3 += sw.ElapsedTicks;

            // ── Phase 4: покадровый (1 мм) ресайз ──
            sw.Restart();
            if (resizable)
                for (int face = 0; face < 6; face++)
                    SweepResizeFromFace(moved, others, savedPos, savedDims, savedRot, face,
                        SweepMaxMm, SweepStepMm, threshold, ref totalSweepSnapEvents,
                        ref totalSweepCompetitionWarnings);
            ticksP4 += sw.ElapsedTicks;

            // ── Phase 5: покадровый (1 мм) перенос ──
            sw.Restart();
            foreach (var dir in MoveDirs)
                SweepMoveDirection(moved, others, savedPos, savedDims, savedRot, dir,
                    SweepMaxMm, SweepStepMm, threshold, ref totalSweepSnapEvents,
                    ref totalSweepCompetitionWarnings);
            ticksP5 += sw.ElapsedTicks;

            RestoreElementState(moved, savedPos, savedDims, savedRot);
            FaceCache.Clear();
        }

        swTotal.Stop();

        // ── Report ────────────────────────────────────────────────────────
        var report = new System.Text.StringBuilder();
        report.AppendLine($"Phase 0 (existing): {TicksToMs(ticksP0):F0}ms");
        report.AppendLine($"Phase 1 (big grow):  {TicksToMs(ticksP1):F0}ms");
        report.AppendLine($"Phase 2 (big shrink):{TicksToMs(ticksP2):F0}ms");
        report.AppendLine($"Phase 3 (big move):  {TicksToMs(ticksP3):F0}ms");
        report.AppendLine($"Phase 4 (sweep resize):{TicksToMs(ticksP4):F0}ms");
        report.AppendLine($"Phase 5 (sweep move):  {TicksToMs(ticksP5):F0}ms");
        report.AppendLine($"  ── sweep internals ──");
        report.AppendLine($"  RestoreElementState: {TicksToMs(_ticksRestore):F0}ms ({_countRestore} calls)");
        report.AppendLine($"  DimensionsMM set:    {TicksToMs(_ticksDimSet):F0}ms ({_countDimSet} calls)");
        report.AppendLine($"  TrySnap:             {TicksToMs(_ticksTrySnap):F0}ms ({_countTrySnap} calls)");
        report.AppendLine($"  transform.position:  {TicksToMs(_ticksPosSet):F0}ms ({_countPosSet} calls)");
        report.AppendLine($"  ── phase 4 details ──");
        report.AppendLine($"  FindResizeTargets:   {TicksToMs(_ticksFindResize):F0}ms ({_countFindResize} calls)");
        report.AppendLine($"  ResizeMath.Compute:  {TicksToMs(_ticksResizeCompute):F0}ms ({_countResizeCompute} calls)");
        report.AppendLine($"  Opposing-face check: {TicksToMs(_ticksOpposing):F0}ms ({_countOpposing} calls)");
        report.AppendLine($"  ── phase 5 details ──");
        report.AppendLine($"  FindMoveTargets:     {TicksToMs(_ticksFindMove):F0}ms ({_countFindMove} calls)");
        report.AppendLine($"  Diagnose:            {TicksToMs(_ticksDiagnose):F0}ms ({_countDiagnose} calls)");
        report.AppendLine($"  ElementsIntersect:   {TicksToMs(_ticksIntersect):F0}ms ({_countIntersect} calls)");
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

            report.AppendLine($"  полный список: {DumpFindings("snap-warnings.log", _warnings)}");
        }

        string errorsPath = DumpFindings("snap-errors.log", _errors);
        TestContext.WriteLine(report.ToString());

        if (_errors.Count > 0)
            Assert.Fail(ErrorSummary(errorsPath));
        else
            Assert.Pass($"All snap mutation tests passed. " +
                $"Events: existing={totalSnapOk}, bigResize={totalBigResizeOk}, bigMove={totalBigMoveOk}, " +
                $"sweep={totalSweepSnapEvents}, warnings={_warnings.Count}, time={swTotal.Elapsed.TotalSeconds:F1}s. " +
                $"Пустой список находок: {errorsPath}.");
    }

    /// <summary>Для пары деталей из исходной сцены проверяет, что встречные грани
    /// в пределах порога действительно прилипают, а краевой контакт при достаточном
    /// перекрытии тоже даёт снэп.
    ///
    /// Пробная позиция ВЫВОДИТСЯ из замеренного зазора, поэтому обязана остаться
    /// там, где ожидаемое поведение вообще определено. На паре, стоящей почти
    /// вплотную к порогу, отвод зажимался в 1 мм и уносил деталь ЗА порог
    /// (зазор 51 при пороге 50) — после чего свип требовал прилипания, которого
    /// быть не должно. Отказ кода правильный и закреплён в
    /// `SnapCoreEdgeCaseTests.Threshold_51mm_NoSnap`; ошибкой был сам свип.
    /// Пара, у которой отвод выносит за порог, пропускается.
    ///
    /// Второй такой случай — винтовая опора. Оракул здесь — SnapSystem.Diagnose,
    /// а он знает ровно одно правило: «встречные грани, зазор в пределах порога,
    /// перекрытие не меньше 30% ⇒ обязано прилипнуть». Для центрующейся детали
    /// это правило неверно (SnapPullsAlongFace объясняет, почему), и отвод здесь
    /// делается ВДОЛЬ НОРМАЛИ — то есть ровно по той оси, по которой посадка
    /// опоры не двигает её вовсе. Требование заменено на встречное: снэп не
    /// имеет права утащить опору вдоль такой грани (MOUNT-PULL).
    ///
    /// Третий такой случай — ВЛОЖЕННЫЕ детали: окно и дверь стоят внутри
    /// габарита стены, их плоскости совпадают, и лучшая пара граней у них
    /// сонаправленная (FarEdgeAlignment), а не встречная. Отбор такую пару
    /// отбрасывает намеренно — выравнивание по дальней кромке загнало бы стену
    /// внутрь двери (SnapCoreContractTests.FarEdgeAlignment_ThatWouldDrive-
    /// ThePartIntoTheNeighbour_IsRejected: «так низ короба уезжал в стену»).
    /// До того как оракул научился судить по правилам отбора, сонаправленные
    /// пары он вовсе не показывал, и свип их не видел; увидев — стал требовать
    /// прилипания, которого продукт запрещает, и выдал 28 находок EDGE-NOSNAP
    /// на стенах, дверях и окнах. Признак приходит из ТОЙ ЖЕ функции, что
    /// принимает решение (SnapPairOffer.landsInsideNeighbour → SnapNeighbourFacts
    /// → SnapNeighborReport.alignmentLandsInsideNeighbour), а требование снова
    /// заменено на встречное: снэп не имеет права утащить деталь таким
    /// выравниванием (ALIGN-PULL).</summary>
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
            bool pullsAlongThisFace = SnapPullsAlongFace(_mountNormal, normal);

            if (r.wouldSnap)
            {
                float maxOff = Mathf.Max(1f, snapThreshold - r.gapMM - 1f);
                float moveAwayMm = Mathf.Clamp(r.gapMM * 0.5f + 5f, 5f, maxOff);
                if (r.gapMM + moveAwayMm > snapThreshold) continue;
                Vector3 awayPos = savedPos - normal * (moveAwayMm * 0.001f);

                var snapRes = SnapSystem.TrySnap(moved, new List<KitchenElement> { target }, awayPos);

                if (!pullsAlongThisFace)
                {
                    float pulledMm = snapRes.snapped
                        ? Vector3.Dot(snapRes.position - awayPos, normal) / AppConstants.MM_TO_UNITS
                        : 0f;
                    if (Mathf.Abs(pulledMm) > 0.5f)
                        AddError($"MOUNT-PULL: {moved.PartName}[f{r.movedFaceIndex}]" +
                            $"↔{target.PartName}[f{r.otherFaceIndex}] " +
                            $"gap={r.gapMM:F1}mm ovl={r.overlapRatio:P0} " +
                            $"утащило на {pulledMm:F1}mm вдоль грани, которую отбор не берёт");
                    continue;
                }

                if (!snapRes.snapped)
                    AddError($"NO-SNAP: {moved.PartName}[f{r.movedFaceIndex}]" +
                        $"↔{target.PartName}[f{r.otherFaceIndex}] " +
                        $"gap={r.gapMM:F1}mm ovl={r.overlapRatio:P0} away={moveAwayMm:F0}mm");
                else
                {
                    moved.transform.position = snapRes.position;
                    if (SnapSystem.ElementsIntersect(moved, target)
                        && !LegitimateOverlap(moved, target))
                        AddError($"INTERSECT: {moved.PartName}↔{target.PartName} after snap-back");
                    snapOk++;
                }
            }
            else if (pullsAlongThisFace && r.overlapRatio >= Tolerance.MinSupportOverlap)
            {
                float testOffMm = Mathf.Clamp(r.gapMM * 0.5f + 5f, 5f, snapThreshold - r.gapMM);
                Vector3 toward = savedPos + normal * (testOffMm * 0.001f);
                var edgeRes = SnapSystem.TrySnap(moved, new List<KitchenElement> { target }, toward);

                if (r.alignmentLandsInsideNeighbour)
                {
                    float pulledMm = edgeRes.snapped
                        ? Vector3.Dot(edgeRes.position - toward, normal) / AppConstants.MM_TO_UNITS
                        : 0f;
                    if (Mathf.Abs(pulledMm) > 0.5f)
                        AddError($"ALIGN-PULL: {moved.PartName}[f{r.movedFaceIndex}]" +
                            $"↔{target.PartName}[f{r.otherFaceIndex}] " +
                            $"gap={r.gapMM:F1}mm ovl={r.overlapRatio:P0} " +
                            $"утащило на {pulledMm:F1}mm выравниванием, которое загоняет " +
                            "деталь внутрь соседа");
                    continue;
                }

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
            savedPos, sizeStartUnits, rawDelta, _othersGeo, moved.ToGeometry(),
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
        var snap = SnapSystem.TrySnap(moved, _othersGeo, testPos);

        string label = $"{moved.PartName} MOVE {distanceMm}мм dir={dir}";
        if (snap.snapped)
        {
            moved.transform.position = snap.position;
            var snapTarget = others.FirstOrDefault(o => o.PartName == snap.targetName);
            if (snapTarget != null && SnapSystem.ElementsIntersect(moved, snapTarget)
                && !LegitimateOverlap(moved, snapTarget))
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
            var swR = System.Diagnostics.Stopwatch.StartNew();
            RestoreElementState(moved, savedPos, savedDims, savedRot);
            _ticksRestore += swR.ElapsedTicks;
            _countRestore++;

            var f = moved.GetFaces()[faceIndex];
            float rawDelta = mm * AppConstants.MM_TO_UNITS;
            float sizeStartUnits = origDim * AppConstants.MM_TO_UNITS;

            var swFR = System.Diagnostics.Stopwatch.StartNew();
            var expected = FindResizeTargets(f, rawDelta, others, thresholdMm);
            _ticksFindResize += swFR.ElapsedTicks;
            _countFindResize++;

            var swRC = System.Diagnostics.Stopwatch.StartNew();
            ResizeMath.Compute(savedDims, axis, f.normal, f.center, f.rightAxis, f.upAxis, f.size,
                savedPos, sizeStartUnits, rawDelta, _othersGeo, moved.ToGeometry(),
                snapEnabled: true, thresholdMm * AppConstants.MM_TO_UNITS,
                out Vector3Int newDims, out Vector3 _, out bool snapped);
            _ticksResizeCompute += swRC.ElapsedTicks;
            _countResizeCompute++;

            if (!snapped && expected.Count > 0)
            {
                var nearest = expected.OrderBy(t => Mathf.Abs(t.gapMm)).First();
                AddError($"RESIZE-NOSNAP: {moved.PartName} {FaceLabels[faceIndex]} @ {mm}мм " +
                    $"грань в {Mathf.Abs(nearest.gapMm):F1}мм от {nearest.targetName} " +
                    $"({(nearest.edgeOnly ? "по ребру" : "по площади")}), но прилипание не сработало");
            }

            var swD = System.Diagnostics.Stopwatch.StartNew();
            moved.DimensionsMM = newDims;
            _ticksDimSet += swD.ElapsedTicks;
            _countDimSet++;
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
                var swOp = System.Diagnostics.Stopwatch.StartNew();
                var movedFace = moved.GetFaces()[faceIndex];
                var closest = FindClosestOpposingFace(movedFace, others, thresholdMm);
                _ticksOpposing += swOp.ElapsedTicks;
                _countOpposing++;

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
            var swR = System.Diagnostics.Stopwatch.StartNew();
            RestoreElementState(moved, savedPos, savedDims, savedRot);
            _ticksRestore += swR.ElapsedTicks;
            _countRestore++;
            Vector3 testPos = savedPos + d * (mm * AppConstants.MM_TO_UNITS);

            var swSnap = System.Diagnostics.Stopwatch.StartNew();
            var snap = SnapSystem.TrySnap(moved, _othersGeo, testPos);
            _ticksTrySnap += swSnap.ElapsedTicks;
            _countTrySnap++;
            if (!snap.snapped)
            {
                // Пропущенное прилипание: есть встречная грань в пределах порога с
                // достаточным перекрытием, а снэпа нет. Без этой проверки тест
                // видел только СОСТОЯВШИЕСЯ снэпы и не мог поймать «не прилипает».
                var swF = System.Diagnostics.Stopwatch.StartNew();
                var missed = FindMoveTargets(moved, others, testPos, thresholdMm);
                _ticksFindMove += swF.ElapsedTicks;
                _countFindMove++;
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
            var swP = System.Diagnostics.Stopwatch.StartNew();
            moved.transform.position = snap.position;
            _ticksPosSet += swP.ElapsedTicks;
            _countPosSet++;
            var swI = System.Diagnostics.Stopwatch.StartNew();
            var snapTarget = OtherByName(snap.targetName);
            bool intersects = snapTarget != null && SnapSystem.ElementsIntersect(moved, snapTarget)
                && !LegitimateOverlap(moved, snapTarget);
            _ticksIntersect += swI.ElapsedTicks;
            _countIntersect++;
            if (intersects)
                AddError($"INTERSECT-AFTER-SNAP: {moved.PartName} SWEEP-MOVE {dirLabel} @ {mm}мм → {snap.targetName}");

            // Снэп «на месте» — это подтверждение уже существующего контакта, а не
            // выбор между кандидатами: конкурировать не за что. Раньше такие шаги
            // давали десятки тысяч ложных MOVE-SNAP-OUTSIDE-DIAG (Diagnose не
            // показывает контакты с нулевым зазором среди активных кандидатов).
            if (Vector3.Distance(snap.position, testPos) <= Tolerance.EpsilonUnits) continue;

            // Конкуренция — это спор ЗА ОДНУ ОСЬ: какой из встречных граней вдоль
            // нормали отдать деталь. Кандидаты с других осей не конкуренты, а
            // дополнение — TrySnap добирает их отдельными проходами.
            var swDg = System.Diagnostics.Stopwatch.StartNew();
            // Результат снэпа уже посчитан выше — второй полный проход не нужен.
            var diag = SnapSystem.Diagnose(moved, others, testPos, snap, maxNeighbors: 50);
            _ticksDiagnose += swDg.ElapsedTicks;
            _countDiagnose++;
            var chosen = diag.neighbors.FirstOrDefault(n => n.name == snap.targetName && n.wouldSnap);
            if (chosen == null) continue;

            // Деталь загнана ВНУТРЬ цели — снэп здесь разводит тела, а не выбирает
            // между кандидатами. Коллизии по условию задачи не рассматриваем.
            if (chosen.intersects) continue;

            // Грань крепления не спорит за ось. Посадка двигает деталь ПОПЕРЁК
            // неё (du/dv по граням цели), а вдоль неё — ровно на ноль:
            // SnapCandidateCollector.AddCentringContact не трогает planeShift.
            // Diagnose этого правила не знает и меряет той же грани зазор по
            // плоскости, так что свип сравнивал сдвиг посадки с зазором до пола
            // как две величины одной оси. Все 78 MOVE-COMPETITION прогона
            // 2026-09-08 — этот случай: опора поднята на d мм, пол внизу на d мм,
            // низ бока A4_side_L — на |42−d| мм, и «ближе» всегда оказывался тот,
            // кого выбор и не рассматривал. Что верно ВМЕСТО этого, требуют
            // SnapCoreScrewLegCentreTests: посадка не меняет высоту, а поднятая
            // опора возвращается пяткой на пол, а не уезжает к царге.
            if (_mountFaceIndex >= 0 && chosen.movedFaceIndex == _mountFaceIndex) continue;

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
                .Where(n => n.wouldSnap && n.gapMM > 0.5f && n.movedFaceIndex / 2 == axis
                            && (_mountFaceIndex < 0 || n.movedFaceIndex != _mountFaceIndex))
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
        // Запись размера тянет за собой ApplyDimensions: пересборку меша, UV и
        // property block. В свипе ПЕРЕНОСА размер не меняется вообще, поэтому
        // 268 938 таких записей были чистой потерей. Сеттер сам это отсечь не
        // может — там ApplyDimensions ещё и СОБИРАЕТ дочернюю геометрию, и
        // деталь, загруженная с дефолтным размером, осталась бы без неё.
        if (moved.DimensionsMM != dims) moved.DimensionsMM = dims;
        moved.transform.rotation = rot;
    }

    private static double TicksToMs(long ticks) => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

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

    /// <summary>Полные списки находок — в файлы рядом с результатами тестов.
    /// В консоль печатать бессмысленно: предупреждений десятки тысяч.
    ///
    /// Ошибки не доезжали до читателя ДВАЖДЫ: AddError переставал их складывать
    /// после двухсотой, а Assert.Fail печатал первые пятьдесят из уже обрезанного
    /// списка — до заказчика прогона доехали ПЯТЬ строк. Прогон стоит 245 с, и
    /// каждая такая обрезка покупается лишним кругом: «а какие ещё детали в
    /// списке?» — это ещё четыре минуты. Теперь копится всё, а в сообщение теста
    /// идёт сводка (см. ErrorSummary) с путём к файлу.
    ///
    /// Файл пишется ВСЕГДА, в том числе пустой на зелёном прогоне: оставшийся с
    /// прошлой красноты файл — ровно та же ловушка, что и перезаписываемый
    /// TestResults.xml (AGENTS.md → «The run's report survives exactly until the
    /// next run»).</summary>
    private static string DumpFindings(string fileName, IEnumerable<string> lines)
    {
        var dir = Path.Combine(Application.dataPath, "../test-results");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllLines(path, lines);
        return path;
    }

    private static string TagOf(string message)
    {
        int colon = message.IndexOf(':');
        return colon > 0 ? message.Substring(0, colon) : message;
    }

    private static readonly char[] SubjectStops = { ' ', '[', '↔' };

    private static string SubjectOf(string message)
    {
        int colon = message.IndexOf(':');
        if (colon < 0 || colon + 1 >= message.Length) return "?";
        string rest = message.Substring(colon + 1).Trim();
        int cut = rest.IndexOfAny(SubjectStops);
        return cut > 0 ? rest.Substring(0, cut) : rest;
    }

    /// <summary>Сводка вместо простыни: сколько всего, по видам, по деталям и по
    /// два примера на вид. Разбивка по ДЕТАЛИ — не украшение: двести ошибок с
    /// одной подписью и одним именем детали читаются как одна причина, а двести
    /// разных имён — как разъехавшаяся фикстура.</summary>
    private string ErrorSummary(string path)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Snap mutation errors: {_errors.Count}. Полный список: {path}");
        sb.AppendLine("по виду:");
        foreach (var g in _errors.GroupBy(TagOf).OrderByDescending(g => g.Count()))
            sb.AppendLine($"  {g.Key}: {g.Count()}");
        sb.AppendLine("по детали (первые 10):");
        foreach (var g in _errors.GroupBy(SubjectOf).OrderByDescending(g => g.Count()).Take(10))
            sb.AppendLine($"  {g.Key}: {g.Count()}");
        sb.AppendLine("примеры, по два на вид:");
        foreach (var g in _errors.GroupBy(TagOf))
            foreach (var e in g.Take(2)) sb.AppendLine($"  {e}");
        return sb.ToString();
    }

    private void AddError(string message) => _errors.Add(message);

    /// <summary>Ось крепления детали — из того же снимка, который читает
    /// SnapCandidateCollector. Спрашивать здесь «это винтовая опора?» нельзя:
    /// тогда у теста и у продакшена было бы два описания одного правила, и на
    /// следующем центрующемся типе они разошлись бы молча.</summary>
    private static Vector3 MountNormalOf(KitchenElement e) => e.ToGeometry().MountNormal;

    /// <summary>Индекс грани крепления в GetFaces(). Порядок граней — контракт
    /// (index/2 = ось, чётный индекс = положительное направление), но брать
    /// индекс готовым нельзя: нормаль крепления живёт в мировых координатах и
    /// зависит от поворота детали.</summary>
    private static int MountFaceIndexOf(KitchenElement e, Vector3 mountNormal)
    {
        if (mountNormal == Vector3.zero) return -1;
        var faces = e.GetFaces();
        for (int i = 0; i < faces.Length; i++)
            if (Vector3.Dot(faces[i].normal, mountNormal) >= Tolerance.ParallelDot) return i;
        return -1;
    }

    /// <summary>Пересечение габаритов после снэпа, которое ЗАКОННО: паз, посадка
    /// по оси крепления или посадка по устьям. Каждое из трёх записано геометрией,
    /// а не списком имён деталей, и каждое стоит ПОСЛЕ проверки пересечения —
    /// снимки для устьев строятся только на тех шагах, где пересечение уже есть.</summary>
    private bool LegitimateOverlap(KitchenElement moved, KitchenElement target) =>
        IsPanel(moved) || SeatedIntoIt(moved, target) || SeatedByPorts(moved, target);

    /// <summary>Деталь ПРИСТЫКОВАНА к цели устьем в устье: снэп совместил мышку
    /// трубы с мышкой соседа, и после этого их габариты налезают друг на друга.
    ///
    /// Это не ослабление, а вторая форма той же посадки, что и SeatedIntoIt.
    /// Посадка по устьям — заявленное поведение продукта, а не промах:
    /// SnapPortSeatTests.Best_NoFacingPairInRange_StillSeatsTheNearestMouth
    /// прямо говорит «поднёс — повернулось — соединилось», то есть устье кладётся
    /// на устье и тогда, когда оси ещё не сведены, а пользователь доворачивает
    /// деталь потом. Пока этого исключения здесь не было, свип звал ошибкой
    /// каждый такой стык: прогон 2026-09-11 дал 1496 строк INTERSECT-AFTER-SNAP
    /// на пятнадцати трубах и фитингах — все до одной про пары «труба ↔ фитинг».
    ///
    /// Правило узкое: устья должны СОВПАСТЬ (alreadySeated при допуске контакта
    /// 0,5 мм). Труба, которую снэп поставил внутрь соседа гранью, а не устьем,
    /// под исключение не попадает, и деталь без устьев — тоже.</summary>
    private static bool SeatedByPorts(KitchenElement moved, KitchenElement target)
    {
        if (target == null) return false;

        var seat = SnapPortSeat.Best(moved.ToGeometry(), target.ToGeometry(),
            Tolerance.ContactMm * AppConstants.MM_TO_UNITS);
        return seat.taken && seat.alreadySeated;
    }

    /// <summary>Деталь ВКРУЧЕНА в цель: её грань крепления лежит внутри толщи
    /// цели вдоль оси крепления. Это посадка, а не столкновение — тот же случай,
    /// что панель в пазу (IsPanel рядом), и записан он через геометрию, а не
    /// через «это винтовая опора»: на следующем центрующемся типе правило
    /// сработает само.
    ///
    /// Числа сцены. Опора 14×58×14 стоит пяткой на полу (центр y=29 мм), цоколь
    /// A4_plint_drawer_L_inner 382×80×18 висит дном на 20 мм: верх опоры (58 мм)
    /// сидит в его толще на 38 мм ЕЩЁ ДО любой мутации — их AABB пересекаются в
    /// покое. Прогон 2026-09-08 дал 565 INTERSECT-AFTER-SNAP, и все 565 — про три
    /// эти опоры и три доски над ними (A4_plint_drawer_L_inner, ..._bottom,
    /// A4_side_L); чужих пар нет ни одной. Проверка требовала «после снэпа не
    /// пересекать», хотя вкрученная опора обязана пересекать.
    ///
    /// Ослабления тут нет. Посадкой считается ТОЛЬКО уход грани крепления внутрь
    /// цели: опора, загнанная пяткой в пол, под правило не попадает (пол снизу,
    /// грань крепления сверху), и любая деталь без оси крепления — тоже.
    /// Встречные проверки — в SnapCoreScrewLegCentreTests:
    /// TheSeatedLeg_ThreadsIntoThePlinth_AndThatOverlapIsTheSeating краснеет,
    /// если посадка перестанет заводить опору в цоколь.</summary>
    private bool SeatedIntoIt(KitchenElement moved, KitchenElement target)
    {
        if (_mountNormal == Vector3.zero || target == null) return false;

        float mountFace = float.MinValue;
        foreach (var v in moved.GetVertices())
            mountFace = Mathf.Max(mountFace, Vector3.Dot(v, _mountNormal));

        float min = float.MaxValue, max = float.MinValue;
        foreach (var v in target.GetVertices())
        {
            float d = Vector3.Dot(v, _mountNormal);
            min = Mathf.Min(min, d);
            max = Mathf.Max(max, d);
        }

        return mountFace > min + Tolerance.EpsilonUnits
            && mountFace < max - Tolerance.EpsilonUnits;
    }

    /// <summary>Правило продакшена, без которого оракулы этого свипа врут: у
    /// центрующейся детали (винтовая опора) снэп тянет её ВДОЛЬ НОРМАЛИ грани
    /// только тогда, когда грань вообще участвует в отборе и не является гранью
    /// крепления.
    ///
    ///  • SnapCandidateCollector.CollectAgainst выбрасывает у такой детали все
    ///    грани, кроме лежащих на оси крепления (21ca342f). Боковых контактов у
    ///    круглой пятки Ø25 не бывает: пока они участвовали, бок выигрывал по
    ///    сдвигу у единственной осмысленной посадки и уносил опору с середины
    ///    царги на её пласть — в сцене это был прыжок −1856 → −1814 мимо −1835.
    ///  • AddCentringContact двигает опору ТОЛЬКО в плоскости грани крепления
    ///    (du·u + dv·v). Высоту она добирает длиной резьбы после отпускания, а
    ///    не съезжая вниз при перетаскивании.
    ///
    /// Пятка — грань, ПРОТИВОПОЛОЖНАЯ крепёжной, — под исключение не попадает:
    /// её контакт с полом обычный, и требование прилипания для неё остаётся.
    /// У обычной детали MountNormal нулевой, и правило не срабатывает вовсе.
    ///
    /// Пока этого правила здесь не было, свип требовал прилипания ровно там, где
    /// его по построению не бывает. Прогон 2026-09-08 упёрся в потолок ошибок, и
    /// все доехавшие строки были про опору — обе формы, [f2] против грани
    /// крепления и [f4] против бока. Сколько их было на самом деле, тот прогон
    /// сказать уже не мог: см. DumpFindings. Ослабления тут нет — обе строки
    /// заменены на встречные проверки: MOUNT-PULL ниже и два теста в
    /// SnapCoreScrewLegCentreTests
    /// (ASideFaceOfTheLeg_IsNotOfferedEvenAtFullOverlap и
    /// LoweredUnderThePlinth_TheLegIsNotPulledBackUpAlongItsThread), которые
    /// краснеют, если отбор снова начнёт брать эти грани.</summary>
    private static bool SnapPullsAlongFace(Vector3 mountNormal, Vector3 faceNormal)
    {
        if (mountNormal == Vector3.zero) return true;
        float alignment = Vector3.Dot(faceNormal, mountNormal);
        if (Mathf.Abs(alignment) < Tolerance.ParallelDot) return false;
        return alignment < Tolerance.ParallelDot;
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
    private List<ResizeTarget> FindResizeTargets(Face movedFace, float rawDelta,
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
    ///    контакта и snapped уже true, сюда мы просто не попадаем;
    ///  • грань центрующейся детали, вдоль нормали которой снэп по построению не
    ///    тянет — см. SnapPullsAlongFace. Без этой строки свип требовал от
    ///    винтовой опоры прилипания её боками и её крепёжной гранью и давал сотни
    ///    MOVE-NOSNAP на здоровом коде.</summary>
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
                    if (!SnapPullsAlongFace(_mountNormal, mf.normal)) continue;
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
    private FaceCandidate FindClosestOpposingFace(Face movedFace,
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

    private List<FaceCandidate> FindAllOpposingFaces(Face movedFace,
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
