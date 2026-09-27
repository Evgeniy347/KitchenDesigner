using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using Object = UnityEngine.Object;

/// <summary>`EdgeBanding.Coverage`'s per-element loop used to scan the WHOLE scene for every
/// qualifying element — O(elements) per call, O(elements^2) total, and the dominant cost of
/// `SceneAnalyzer`'s "edgeCover" stage (~97 ms @ 1600 elements). `SceneFaces.NeighborsOf` now
/// builds ONE grid (`ValidationBroadPhase`, shared with `ConstraintValidator`) per scene
/// snapshot and hands `Coverage` only the elements whose bounding sphere could actually touch —
/// a strict superset of the true neighbours, so the exact per-pair math downstream (unchanged)
/// still filters out anything that doesn't really touch.
///
/// This file is the proof that the accelerated path computes EXACTLY what the scan it replaced
/// computed: `EdgeBanding.CoverageBruteForceForTests` is the same method body, still scanning
/// every other element, kept only so this comparison has something to compare against. Both
/// paths funnel through the same `CoverageOverCandidates` — a set of candidates in any order
/// produces the same result because the accumulation (`covers[i].Add`) doesn't care about
/// order and `UnionArea` re-sorts before it ever looks at ordering — so an exact match here is
/// not a coincidence of the test data, it is what the refactor is supposed to guarantee.</summary>
public class EdgeCoverageBroadPhaseEquivalenceTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        // SaveLoadManager.RestoreScene (the frozen-fixture test) can create a BasePlate that
        // never appears in its own returned object list, so sweeping only `_spawned` leaked
        // one into the shared scene (caught by SceneLeakGuardReportTests). This is the same
        // FindObjectsByType sweep ValidationInvariantTests.ClearScene already uses for exactly
        // this reason.
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);

        PartRegistry.Clear();
    }

    private static void AssertSameCoverage(EdgeCoverage expected, EdgeCoverage actual, string where)
    {
        foreach (EdgeSide side in Enum.GetValues(typeof(EdgeSide)))
            Assert.AreEqual(expected.Ratio(side), actual.Ratio(side), 0f,
                $"{where}, сторона {side}: индексный путь обязан посчитать РОВНО то же число, "
                + "что и полный перебор — не «близкое», а то же самое");
    }

    [Test]
    public void Coverage_OnTheFrozenValidationScene_MatchesTheBruteForceScanForEveryElement()
    {
        var fullPath = Path.Combine(Application.dataPath, "Tests/EditMode/Fixtures/validation-scene.save.json");
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        string json = File.ReadAllText(fullPath);

        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data);
        var objs = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(objs);
        var elements = objs.Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList()!;
        _spawned.AddRange(objs);

        var scene = SceneFaces.Of(elements);
        int compared = 0;
        for (int k = 0; k < elements.Count; k++)
        {
            var element = elements[k];
            if (element == null) continue;

            var viaIndex = EdgeBanding.Coverage(element, scene, k);
            var viaScan = EdgeBanding.CoverageBruteForceForTests(element, scene, k);
            AssertSameCoverage(viaScan, viaIndex, $"элемент {element.PartName} (#{k})");
            compared++;
        }

        Assert.Greater(compared, 200, "фикстура обязана дать сотни элементов — иначе сравнение "
            + "ничего не гарантирует на масштабе, для которого чинился путь");
    }

    private KitchenElement MakeBoard(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = dims;
        el.transform.position = pos;
        PartRegistry.Register(el);
        _spawned.Add(go);
        return el;
    }

    /// <summary>800 touching pairs (1600 elements), each pair 3 m from its neighbours — every
    /// element has exactly one real neighbour to cover its edge against, and pairs sit far
    /// enough apart that the grid never lumps two UNRELATED pairs into the same cell. A scene of
    /// far-apart singles (as used to measure the original defect) would exercise zero actual
    /// coverage and prove nothing about the accumulation logic, only that the broad phase finds
    /// nobody; a naive dense grid of near-zero-height shelves (tried first here) collapsed every
    /// row into ONE grid cell along Y and made the broad phase itself the bottleneck — this
    /// layout is the fix for that: separation, not density, is what a broad phase needs.</summary>
    private const float PairSpacingUnits = 3.0f;

    private static Vector3 PairOriginAlongX(int i) => new Vector3(i * PairSpacingUnits, 0f, 0f);

    private static Vector3 PairOriginAlongZ(int i) => new Vector3(0f, 0f, i * PairSpacingUnits);

    private static Vector3 PairOriginAlongDiagonal(int i) =>
        new Vector3(i * PairSpacingUnits, 0f, i * PairSpacingUnits);

    private List<KitchenElement> MakeTouchingPairs(int pairCount, Func<int, Vector3> pairOrigin)
    {
        var dims = new Vector3Int(600, 18, 400);
        float widthUnits = dims.x * AppConstants.MM_TO_UNITS;

        var elements = new List<KitchenElement>(pairCount * 2);
        for (int i = 0; i < pairCount; i++)
        {
            Vector3 origin = pairOrigin(i);
            elements.Add(MakeBoard($"pair{i}_a", dims, origin));
            elements.Add(MakeBoard($"pair{i}_b", dims, origin + new Vector3(widthUnits, 0f, 0f)));
        }
        return elements;
    }

    [Test]
    public void Coverage_OnADenseSyntheticSceneOf1600Elements_MatchesTheBruteForceScan()
    {
        var elements = MakeTouchingPairs(800, PairOriginAlongX);
        var scene = SceneFaces.Of(elements);
        int mismatches = 0;
        var firstMismatch = "";
        for (int k = 0; k < elements.Count; k++)
        {
            var viaIndex = EdgeBanding.Coverage(elements[k], scene, k);
            var viaScan = EdgeBanding.CoverageBruteForceForTests(elements[k], scene, k);
            foreach (EdgeSide side in Enum.GetValues(typeof(EdgeSide)))
            {
                if (viaIndex.Ratio(side) == viaScan.Ratio(side)) continue;
                mismatches++;
                if (firstMismatch.Length == 0)
                    firstMismatch = $"элемент #{k}, сторона {side}: индекс {viaIndex.Ratio(side)}, "
                        + $"перебор {viaScan.Ratio(side)}";
            }
        }

        Assert.AreEqual(0, mismatches, $"первое расхождение — {firstMismatch}");
    }

    /// <summary>2026-09-26: заменил замер в МИЛЛИСЕКУНДАХ (было
    /// `EdgeCoverStage_OnA1600ElementScene_StaysUnderFortyFiveMilliseconds`, читал "edgeCover
    /// XXмс" из `[Perf] SceneAnalyzer.Analyze`) на счётчик ОПЕРАЦИЙ — по требованию: тест
    /// красился под соседской нагрузкой на машине (50,5 мс вместо порога 45 мс на том же коде),
    /// хотя сам `edgeCover` не менялся ни на йоту. Время не годится сенсором для формы роста:
    /// оно меряет ещё и то, что тест не контролирует (JIT прогрелся не до конца, антивирус
    /// проснулся, сосед по CPU). Форма роста — то, что этот тест ДОЛЖЕН стеречь (O(n log n),
    /// не O(n²) после `SphereSweep`), — не зависит от машины: она считается количеством
    /// (деталь, кандидат) пар, которые `EdgeBanding.CoverageOverCandidates` реально
    /// перебирает (`EdgeBanding.TakeCandidatesExamined`), а не тем, сколько это заняло по
    /// часам. Тот же приём, что и `2 × probes(N) == probes(2N)` в других сторожах этого
    /// проекта (conventions/CORRECTNESS.md).
    ///
    /// 2026-09-26: `SphereSweep` мело только по X, поэтому кухонный ряд вдоль стены Z (все
    /// корпуса на одном x, разные z) вырождал развёртку в перебор всех пар — рост оставался
    /// линейным ТОЛЬКО для этой, X-раскладки (`SphereSweepTests` доказывает это на голых
    /// сферах отдельно). Три раскладки ниже — X, Z и диагональ — кладут одни и те же
    /// touching-пары по разным осям и должны дать один и тот же линейный рост.</summary>
    [Test]
    public void EdgeCoverCandidatesExamined_GrowsLinearly_NotQuadratically_AlongX()
    {
        AssertCandidatesGrowLinearly(PairOriginAlongX, "X");
    }

    [Test]
    public void EdgeCoverCandidatesExamined_GrowsLinearly_NotQuadratically_AlongZ()
    {
        AssertCandidatesGrowLinearly(PairOriginAlongZ, "Z");
    }

    [Test]
    public void EdgeCoverCandidatesExamined_GrowsLinearly_NotQuadratically_AlongADiagonal()
    {
        AssertCandidatesGrowLinearly(PairOriginAlongDiagonal, "диагонали X=Z");
    }

    private void AssertCandidatesGrowLinearly(Func<int, Vector3> pairOrigin, string layoutName)
    {
        int small = CandidatesExaminedOverWholeScene(pairCount: 100, pairOrigin);
        int large = CandidatesExaminedOverWholeScene(pairCount: 400, pairOrigin);

        Assert.Greater(small, 0, $"раскладка вдоль {layoutName}: сцена из 200 элементов "
            + "обязана дать хоть один кандидат — иначе сравнение ничего не проверяет");
        Assert.Less(large, small * 8,
            $"раскладка вдоль {layoutName}: сцена выросла в 4 раза (100 пар -> 400 пар), число "
            + $"проверенных кандидатов выросло с {small} до {large}. Индексный путь "
            + "(`SceneFaces.NeighborsOf` через `SphereSweep`) на этой раздельной раскладке "
            + "даёт кандидатов O(n) — рост должен остаться около 4×. Рост около 16× (или "
            + "больше 8×, взятого с запасом между 4× и 16×) означает, что перебор снова стал "
            + "O(n²) и деградировал до `CoverageBruteForceForTests`-подобного поведения");
    }

    private int CandidatesExaminedOverWholeScene(int pairCount, Func<int, Vector3> pairOrigin)
    {
        var elements = MakeTouchingPairs(pairCount, pairOrigin);
        var scene = SceneFaces.Of(elements);

        EdgeBanding.TakeCandidatesExamined();
        for (int k = 0; k < elements.Count; k++)
            EdgeBanding.Coverage(elements[k], scene, k);
        int examined = EdgeBanding.TakeCandidatesExamined();

        foreach (var e in elements)
            if (e != null) Object.DestroyImmediate(e.gameObject);
        _spawned.RemoveAll(g => g == null);

        return examined;
    }
}
