using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Мутирующий тест прилипания: загружает ЛЮБОЙ файл сохранения, перебирает
/// все детали и проверяет, что система снапа отрабатывает корректно при
/// перемещении и ресайзе.
///
/// Тест НЕ привязан к конкретным именам/размерам/позициям из файла —
/// вся геометрия извлекается из сохранения динамически.
///
/// Чтобы протестировать другой файл — замени <see cref="SaveFileName"/>.
/// </summary>
public class SnapMutationTests
{
    private const string SaveFileName = "example.save.json";
    private const string SaveFileRelativePath = "Assets/../docs/" + SaveFileName;

    private readonly List<GameObject> _spawned = new();

    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s, "KitchenSettings.asset not found");
        s.GridStep = 1;
        s.GridEnabled = true;
        s.SnapEnabled = true;
        s.SnapThreshold = 50f;
        s.BlockOnViolation = false;
        SnapSystem.VerboseLog = false;

        var fullPath = Path.Combine(Application.dataPath, "../docs", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath),
            $"Save file not found: {fullPath}. Place a .save.json or update SaveFileName constant.");

        var json = File.ReadAllText(fullPath);
        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data, $"Failed to deserialize: {fullPath}");

        var restored = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(restored, "RestoreScene returned empty list");
        _spawned.AddRange(restored);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);

        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

    [Test]
    public void Mutation_AllFacingFacePairs_SnapWhenWithinThreshold()
    {
        var elements = _spawned
            .Select(g => g.GetComponent<KitchenElement>())
            .Where(e => e != null)
            .ToList();

        Assert.IsNotEmpty(elements, "No KitchenElements in restored scene");

        var movable = elements.Where(e => e.Movable && e.gameObject.activeInHierarchy).ToList();
        var snapThreshold = KitchenSettings.Instance.SnapThreshold;

        var errors = new List<string>();
        var resizeWarnings = new List<string>();
        var snapOk = 0;
        var noFacingFaces = 0;

        TestContext.Progress.WriteLine(
            $"=== Mutation test: \"{SaveFileName}\" ===");
        TestContext.Progress.WriteLine(
            $"  Elements: {elements.Count} total, {movable.Count} movable");
        TestContext.Progress.WriteLine(
            $"  Snap threshold: {snapThreshold}mm");

        // ── Phase 1+2: per-pair diagnose + attraction test ──────────────

        for (int i = 0; i < movable.Count; i++)
        {
            var moved = movable[i];
            if (moved == null) continue;

            var savedPos = moved.transform.position;
            var savedDims = moved.DimensionsMM;

            for (int j = 0; j < elements.Count; j++)
            {
                var target = elements[j];
                if (target == null || target == moved) continue;
                if (!target.gameObject.activeInHierarchy) continue;

                // Diagnose snap opportunities from current position
                var diag = SnapSystem.Diagnose(moved,
                    new List<KitchenElement> { target },
                    savedPos, maxNeighbors: 50);

                foreach (var r in diag.neighbors)
                {
                    if (!r.withinThreshold) continue;
                    if (!r.hasFacingFaces)
                    {
                        noFacingFaces++;
                        continue;
                    }

                    // Get world-space normal of the moved face
                    var faces = moved.GetFaces();
                    if (r.movedFaceIndex < 0 || r.movedFaceIndex >= faces.Length) continue;
                    var normal = faces[r.movedFaceIndex].normal;

                    if (r.wouldSnap)
                    {
                        // ── Attraction test: move AWAY, expect snap BACK ──
                        // Must NOT exceed threshold: finalGap = gapMM + moveAwayMm <= snapThreshold
                        float maxOffset = Mathf.Max(1f, snapThreshold - r.gapMM - 1f);
                        float moveAwayMm = Mathf.Clamp(r.gapMM * 0.5f + 5f, 5f, maxOffset);

                        Vector3 awayPos = savedPos - normal * (moveAwayMm * 0.001f);

                        var result = SnapSystem.TrySnap(moved,
                            new List<KitchenElement> { target }, awayPos);

                        if (!result.snapped)
                        {
                            errors.Add(
                                $"NO-SNAP: {moved.PartName}[f{r.movedFaceIndex}]" +
                                $"↔{target.PartName}[f{r.otherFaceIndex}] " +
                                $"gap={r.gapMM:F1}mm ovl={r.overlapRatio:P0} " +
                                $"→ away {moveAwayMm:F0}mm (within {snapThreshold}mm), " +
                                $"expected snap back, got NONE. {r.verdict}");
                        }
                        else
                        {
                            // Verify: snapped position must not intersect
                            moved.transform.position = result.position;
                            if (SnapSystem.ElementsIntersect(moved, target))
                            {
                                errors.Add(
                                    $"INTERSECTION: {moved.PartName}↔{target.PartName} " +
                                    $"after snap-back from {moveAwayMm:F0}mm away " +
                                    $"→ snapPos={result.position:F3}");
                            }
                            moved.transform.position = savedPos;
                            snapOk++;
                        }
                    }
                    else
                    {
                        // ── Edge-contact: facing faces within threshold, overlap < 30% ──
                        // Only flag when faces have meaningful overlap (≥10%) but snap fails.
                        // Below 10% is a valid no-snap case (MinSnapOverlap).
                        if (r.overlapRatio < Tolerance.MinSnapOverlap) continue;

                        // Verify by moving toward the target
                        float testOffsetMm = Mathf.Max(2f, r.gapMM * 0.5f);
                        testOffsetMm = Mathf.Min(testOffsetMm, snapThreshold - r.gapMM);
                        Vector3 towardPos = savedPos + normal * (testOffsetMm * 0.001f);

                        var edgeResult = SnapSystem.TrySnap(moved,
                            new List<KitchenElement> { target }, towardPos);

                        if (!edgeResult.snapped)
                        {
                            errors.Add(
                                $"EDGE-NOSNAP: {moved.PartName}[f{r.movedFaceIndex}]" +
                                $"↔{target.PartName}[f{r.otherFaceIndex}] " +
                                $"gap={r.gapMM:F1}mm ovl={r.overlapRatio:P0} " +
                                $"→ toward {testOffsetMm:F0}mm, expected snap, got NONE. " +
                                $"ver=\"{r.verdict}\"");
                        }
                        else
                        {
                            moved.transform.position = edgeResult.position;
                            if (SnapSystem.ElementsIntersect(moved, target))
                            {
                                errors.Add(
                                    $"EDGE-INTERSECT: {moved.PartName}↔{target.PartName} " +
                                    $"edge-snap from {testOffsetMm:F0}mm toward → intersection");
                            }
                            moved.transform.position = savedPos;
                            snapOk++;
                        }
                    }
                }
            }

            // ── Phase 3: Resize test ────────────────────────────────────
            moved.transform.position = savedPos;

            var reductions = new (int dimIdx, int amount, string name)[]
            {
                (0, 50, "width"),
                (1, 50, "height"),
                (2, 50, "depth"),
            };

            foreach (var (dimIdx, amount, dimName) in reductions)
            {
                int origDim = dimIdx switch { 0 => savedDims.x, 1 => savedDims.y, _ => savedDims.z };
                if (origDim < 50) continue; // skip already-thin dimensions

                int newVal = Mathf.Max(20, origDim - amount);
                var newDims = savedDims;
                switch (dimIdx)
                {
                    case 0: newDims.x = newVal; break;
                    case 1: newDims.y = newVal; break;
                    case 2: newDims.z = newVal; break;
                }

                if (newDims == savedDims) continue;

                try
                {
                    moved.DimensionsMM = newDims;
                }
                catch
                {
                    // Some element types reject dimension changes via property setter
                    continue;
                }

                // Check for new intersections after resize
                var others = elements
                    .Where(e => e != moved && e != null && e.gameObject.activeInHierarchy)
                    .ToList();

                foreach (var other in others)
                {
                    if (SnapSystem.ElementsIntersect(moved, other))
                    {
                        resizeWarnings.Add(
                            $"RW-RESIZE-X: {moved.PartName} after -{dimName} " +
                            $"(from {savedDims} to {newDims}) intersects {other.PartName}");
                    }
                }

                // Diagnose snap after resize
                var diagR = SnapSystem.Diagnose(moved, others, savedPos, maxNeighbors: 50);
                foreach (var r in diagR.neighbors)
                {
                    if (r.wouldSnap && r.withinThreshold)
                    {
                        // Verify TrySnap agrees
                        var faces2 = moved.GetFaces();
                        if (r.movedFaceIndex < 0 || r.movedFaceIndex >= faces2.Length) continue;
                        var n2 = faces2[r.movedFaceIndex].normal;

                        float maxOff = Mathf.Max(1f, snapThreshold - r.gapMM - 1f);
                        float offsetMm = Mathf.Clamp(r.gapMM * 0.5f + 5f, 5f, maxOff);
                        var testPos2 = savedPos - n2 * (offsetMm * 0.001f);

                        var target2 = others.FirstOrDefault(
                            o => o != null && o.PartName == r.name);
                        if (target2 == null) continue;

                        var snapRes = SnapSystem.TrySnap(moved,
                            new List<KitchenElement> { target2 }, testPos2);

                        if (!snapRes.snapped)
                        {
                            errors.Add(
                                $"RESIZE-NOSNAP: {moved.PartName} -{dimName} " +
                                $"(now {newDims})[f{r.movedFaceIndex}]↔{r.name}[f{r.otherFaceIndex}] " +
                                $"gap={r.gapMM:F1}mm expected snap, got NONE");
                        }
                        else
                        {
                            moved.transform.position = snapRes.position;
                            if (SnapSystem.ElementsIntersect(moved, target2))
                            {
                                resizeWarnings.Add(
                                    $"RW-RESIZE-INTERSECT: {moved.PartName} -{dimName} " +
                                    $"snapped to {r.name} but intersects");
                            }
                            moved.transform.position = savedPos;
                            snapOk++;
                        }
                    }
                    else if (r.withinThreshold && r.hasFacingFaces)
                    {
                        if (r.overlapRatio < Tolerance.MinSnapOverlap) continue;

                        float testOffMm = Mathf.Max(2f, r.gapMM * 0.5f);
                        testOffMm = Mathf.Min(testOffMm, snapThreshold - r.gapMM);
                        var faces3 = moved.GetFaces();
                        if (r.movedFaceIndex < 0 || r.movedFaceIndex >= faces3.Length) continue;
                        var n3 = faces3[r.movedFaceIndex].normal;
                        var towardPos3 = savedPos + n3 * (testOffMm * 0.001f);

                        var tgt3 = others.FirstOrDefault(
                            o => o != null && o.PartName == r.name);
                        if (tgt3 == null) continue;

                        var res3 = SnapSystem.TrySnap(moved,
                            new List<KitchenElement> { tgt3 }, towardPos3);

                        if (!res3.snapped)
                        {
                            errors.Add(
                                $"RESIZE-EDGE-NOSNAP: {moved.PartName} -{dimName} " +
                                $"(now {newDims})[f{r.movedFaceIndex}]↔{r.name}[f{r.otherFaceIndex}] " +
                                $"gap={r.gapMM:F1}mm ovl={r.overlapRatio:P0} → expected snap, got NONE. " +
                                $"ver=\"{r.verdict}\"");
                        }
                        else
                        {
                            moved.transform.position = res3.position;
                            if (SnapSystem.ElementsIntersect(moved, tgt3))
                            {
                                errors.Add(
                                    $"RESIZE-EDGE-INTERSECT: {moved.PartName} -{dimName} " +
                                    $"snapped to {r.name} but intersects");
                            }
                            moved.transform.position = savedPos;
                            snapOk++;
                        }
                    }
                }

                moved.DimensionsMM = savedDims;
            }

            moved.transform.position = savedPos;
        }

        // ── Phase 4: Report ────────────────────────────────────────────

        TestContext.Progress.WriteLine($"\n  Snap-OK: {snapOk}");
        TestContext.Progress.WriteLine($"  No facing faces (skipped): {noFacingFaces}");

        if (resizeWarnings.Count > 0)
        {
            TestContext.Progress.WriteLine($"\n  Resize intersection warnings ({resizeWarnings.Count}):");
            foreach (var rw in resizeWarnings.Distinct().Take(10))
                TestContext.Progress.WriteLine($"    {rw}");
            if (resizeWarnings.Count > 10)
                TestContext.Progress.WriteLine($"    ... +{resizeWarnings.Count - 10} more");
        }

        if (errors.Count > 0)
        {
            Assert.Fail(
                $"Snap mutation errors ({errors.Count}):\n" +
                string.Join("\n", errors.Take(40)));
        }
        else
        {
            Assert.Pass(
                $"All {snapOk} snap tests passed. {resizeWarnings.Count} resize-warnings.");
        }
    }
}
