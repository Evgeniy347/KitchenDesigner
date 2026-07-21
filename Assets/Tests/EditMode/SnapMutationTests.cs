using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Мутирующий тест прилипания: загружает ЛЮБОЙ файл сохранения,
/// для КАЖДОГО элемента сбрасывает сцену и тестирует перемещение + ресайз.
///
/// Никаких хардкод-имён — геометрия извлекается из сцены динамически.
/// </summary>
public class SnapMutationTests
{
    private const string SaveFileName = "example.save.json";

    private string _json = "";
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();
    private bool _prevAutoSave;
    private bool _prevSpatialGrid;
    private bool _prevEdgeOutline;

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

        // Save settings that we'll modify
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
        // Restore settings to avoid poisoning other tests
        var s = KitchenSettings.Instance;
        if (s != null)
        {
            s.AutoSave = _prevAutoSave;
            s.SpatialGrid = _prevSpatialGrid;
            s.EdgeOutline = _prevEdgeOutline;
        }
        _errors.Clear();
        _warnings.Clear();
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

    [Test]
    public void Mutation_AllFacingFacePairs_SnapWhenWithinThreshold()
    {
        // First pass: discover all element names
        ClearScene();
        var allElements = RestoreScene();
        Assert.IsNotEmpty(allElements, "No KitchenElements in restored scene");
        var movableNames = allElements.Where(e => e.Movable && e.gameObject.activeInHierarchy)
            .Select(e => e.PartName).ToList();
        ClearScene();

        TestContext.Progress.WriteLine(
            $"=== Mutation: {SaveFileName} | {movableNames.Count} movable / {allElements.Count} total ===");

        int totalSnapOk = 0;

        foreach (var name in movableNames)
        {
            ClearScene();
            var elements = RestoreScene();
            var moved = elements.FirstOrDefault(e => e.PartName == name);
            if (moved == null) continue;

            var savedPos = moved.transform.position;
            var savedDims = moved.DimensionsMM;
            float snapThreshold = KitchenSettings.Instance.SnapThreshold;

            // ── Phase 1: For each other element, diagnose + test attraction ─
            foreach (var target in elements)
            {
                if (target == moved || target == null) continue;
                if (!target.gameObject.activeInHierarchy) continue;

                var diag = SnapSystem.Diagnose(moved,
                    new List<KitchenElement> { target },
                    savedPos, maxNeighbors: 50);

                foreach (var r in diag.neighbors)
                {
                    if (!r.withinThreshold || !r.hasFacingFaces) continue;

                    var faces = moved.GetFaces();
                    if (r.movedFaceIndex < 0 || r.movedFaceIndex >= faces.Length) continue;
                    var normal = faces[r.movedFaceIndex].normal;

                    if (r.wouldSnap)
                    {
                        // Attraction test: move away, expect snap back
                        float maxOff = Mathf.Max(1f, snapThreshold - r.gapMM - 1f);
                        float moveAwayMm = Mathf.Clamp(r.gapMM * 0.5f + 5f, 5f, maxOff);
                        Vector3 awayPos = savedPos - normal * (moveAwayMm * 0.001f);

                        var snapRes = SnapSystem.TrySnap(moved,
                            new List<KitchenElement> { target }, awayPos);

                        if (!snapRes.snapped)
                            _errors.Add($"NO-SNAP: {moved.PartName}[f{r.movedFaceIndex}]" +
                                $"↔{target.PartName}[f{r.otherFaceIndex}] " +
                                $"gap={r.gapMM:F1}mm ovl={r.overlapRatio:P0} away={moveAwayMm:F0}mm");
                        else
                        {
                            moved.transform.position = snapRes.position;
                            if (SnapSystem.ElementsIntersect(moved, target))
                                _errors.Add($"INTERSECT: {moved.PartName}↔{target.PartName} after snap-back");
                            moved.transform.position = savedPos;
                            totalSnapOk++;
                        }
                    }
                    else if (r.overlapRatio >= Tolerance.MinSnapOverlap)
                    {
                        // Edge-contact: overlap between 10-30%, should snap from closer
                        float testOffMm = Mathf.Clamp(r.gapMM * 0.5f + 5f, 5f, snapThreshold - r.gapMM);
                        Vector3 toward = savedPos + normal * (testOffMm * 0.001f);
                        var edgeRes = SnapSystem.TrySnap(moved,
                            new List<KitchenElement> { target }, toward);
                        if (!edgeRes.snapped)
                            _errors.Add($"EDGE-NOSNAP: {moved.PartName}[f{r.movedFaceIndex}]" +
                                $"↔{target.PartName}[f{r.otherFaceIndex}] " +
                                $"gap={r.gapMM:F1}mm ovl={r.overlapRatio:P0}");
                        else totalSnapOk++;
                    }
                }
            }

            // ── Phase 2: Aggressive resize (grow + shrink, multiple deltas) ─
            moved.transform.position = savedPos;
            int[] growSteps = { 25, 50, 100, 200 };
            int[] shrinkSteps = { -25, -50, -100 };
            string[] dimNames = { "width", "height", "depth" };

            for (int d = 0; d < 3; d++)
            {
                int origDim = d switch { 0 => savedDims.x, 1 => savedDims.y, _ => savedDims.z };

                foreach (int step in growSteps)
                    TestResize(moved, elements, savedPos, savedDims, d, step, origDim, dimNames[d],
                        snapThreshold, ref totalSnapOk);

                foreach (int step in shrinkSteps)
                {
                    int newVal = Mathf.Max(20, origDim + step);
                    if (newVal != origDim)
                        TestResize(moved, elements, savedPos, savedDims, d, newVal - origDim, origDim, dimNames[d],
                            snapThreshold, ref totalSnapOk);
                }
            }

            // ── Phase 3: Aggressive move (6 directions, multiple steps) ─
            moved.transform.position = savedPos;
            moved.DimensionsMM = savedDims;
            float[] moveStepsUnits = { 0.005f, 0.01f, 0.025f, 0.04f };
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

            var others = elements.Where(e => e != moved && e != null && e.gameObject.activeInHierarchy).ToList();
            foreach (var dir in dirs)
            {
                foreach (float step in moveStepsUnits)
                {
                    Vector3 testPos = savedPos + dir * step;
                    var snapRes = SnapSystem.TrySnap(moved, others, testPos);
                    if (snapRes.snapped) { moved.transform.position = savedPos; totalSnapOk++; }
                }
            }

            moved.transform.position = savedPos;
            moved.DimensionsMM = savedDims;
            ClearScene();
        }

        // ── Report ──────────────────────────────────────────────────────
        TestContext.Progress.WriteLine($"  Snap-OK: {totalSnapOk}");

        if (_warnings.Count > 0)
        {
            TestContext.Progress.WriteLine($"  Warnings ({_warnings.Count}):");
            foreach (var w in _warnings.Take(15)) TestContext.Progress.WriteLine($"    {w}");
        }

        if (_errors.Count > 0)
            Assert.Fail($"Snap mutation errors ({_errors.Count}):\n{string.Join("\n", _errors.Take(40))}");
        else
            Assert.Pass($"All {totalSnapOk} snap tests passed.");
    }

    private void TestResize(KitchenElement moved, List<KitchenElement> elements,
        Vector3 savedPos, Vector3Int savedDims, int dimIdx, int deltaMM, int origDim, string dimName,
        float snapThreshold, ref int snapOk)
    {
        int newVal = deltaMM > 0 ? origDim + deltaMM : Mathf.Max(20, origDim + deltaMM);
        if (newVal == origDim) return;
        var newDims = savedDims;
        switch (dimIdx) { case 0: newDims.x = newVal; break; case 1: newDims.y = newVal; break; case 2: newDims.z = newVal; break; }

        try { moved.DimensionsMM = newDims; } catch { return; }

        var sign = deltaMM > 0 ? "+" : "";
        var label = $"{moved.PartName} {dimName}{sign}{deltaMM} ({origDim}→{newVal})";
        var others = elements.Where(e => e != moved && e != null && e.gameObject.activeInHierarchy).ToList();

        foreach (var other in others)
            if (SnapSystem.ElementsIntersect(moved, other))
                _warnings.Add($"RW-X: {label} intersects {other.PartName}");

        var diagR = SnapSystem.Diagnose(moved, others, savedPos, maxNeighbors: 50);
        var facesRS = moved.GetFaces();

        foreach (var r in diagR.neighbors)
        {
            if (r.wouldSnap && r.withinThreshold && r.movedFaceIndex >= 0 && r.movedFaceIndex < facesRS.Length)
            {
                var mf = facesRS[r.movedFaceIndex];
                if (!ResizeSnap.SnapDelta(mf.center, mf.normal, mf.rightAxis, mf.upAxis,
                    new Vector2(mf.size.x, mf.size.y), others, moved, snapThreshold * 0.001f, out _))
                {
                    _errors.Add($"RS-NOSNAP: {label} f{r.movedFaceIndex}↔{r.name} gap={r.gapMM:F1}mm");
                }
            }
        }

        moved.DimensionsMM = savedDims;
    }
}
