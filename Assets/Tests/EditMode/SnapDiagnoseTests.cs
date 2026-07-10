using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// SnapSystem.Diagnose — инструмент разбора «почему не прилипло». Проверяем,
/// что каждая причина отказа (пересечение, зазор, перекрытие, поворот)
/// распознаётся правильно и что диагноз согласуется с реальным TrySnap.
/// </summary>
public class SnapDiagnoseTests : SnapTestBase
{
    private static SnapDiagnosis Diagnose(KitchenElement moved, KitchenElement other, Vector3 testPos)
        => SnapSystem.Diagnose(moved, new List<KitchenElement> { other }, testPos);

    [Test]
    public void BoardsWithinThreshold_DiagnosisSaysWouldSnap()
    {
        // Две стоящие панели лицом к лицу, зазор 10 мм < порога 50 мм.
        var a = MakeStd("A", new Vector3(0f, 0.2f, 0f));
        var b = MakeStd("B", new Vector3(0f, 0.2f, 0.028f)); // 18мм толщина => зазор 10мм

        var d = Diagnose(b, a, b.transform.position);

        Assert.IsTrue(d.snapEnabled);
        Assert.IsTrue(d.wouldSnap, "TrySnap должен прилипнуть");
        Assert.AreEqual("A", d.snapTarget);
        Assert.AreEqual(1, d.neighbors.Count);
        var n = d.neighbors[0];
        Assert.IsTrue(n.wouldSnap, n.verdict);
        Assert.IsTrue(n.withinThreshold);
        Assert.IsTrue(n.overlapEnough);
        Assert.AreEqual(10f, n.gapMM, 0.5f, "зазор по нормали ~10 мм");
        StringAssert.Contains("OK", n.verdict);
    }

    [Test]
    public void GapAboveThreshold_DiagnosisExplainsGap()
    {
        var a = MakeStd("A", new Vector3(0f, 0.2f, 0f));
        var b = MakeStd("B", new Vector3(0f, 0.2f, 0.118f)); // зазор 100 мм > 50 мм

        var d = Diagnose(b, a, b.transform.position);

        Assert.IsFalse(d.wouldSnap);
        var n = d.neighbors[0];
        Assert.IsFalse(n.wouldSnap);
        Assert.IsFalse(n.withinThreshold, "зазор больше порога");
        Assert.AreEqual(100f, n.gapMM, 0.5f);
        StringAssert.Contains("зазор", n.verdict);
    }

    [Test]
    public void IntersectingBoards_DiagnosisReportsIntersection()
    {
        var a = MakeStd("A", new Vector3(0f, 0.2f, 0f));
        var b = MakeStd("B", new Vector3(0f, 0.2f, 0.005f)); // толщина 18: глубокое перекрытие

        var d = Diagnose(b, a, b.transform.position);

        // Хотя AABB пересекаются, gap = 23 мм < порога 50 мм, поэтому
        // снэп сработает и разведёт доски заподлицо.
        Assert.IsTrue(d.wouldSnap, "снэп разведёт пересекающиеся доски");
        var n = d.neighbors[0];
        Assert.IsTrue(n.intersects, "AABB действительно пересекаются");
        Assert.IsTrue(n.wouldSnap, "face-pair разведёт доски");
        StringAssert.Contains("снэп сработает", n.verdict);
    }

    [Test]
    public void RotatedBoard_DiagnosisReportsNoFacingFaces()
    {
        // Поворот вокруг ДВУХ осей: ни одна грань не остаётся осевой
        // (при повороте вокруг одной оси грани этой оси остаются встречными).
        var a = MakeStd("A", new Vector3(0f, 0.2f, 0f), Quaternion.Euler(45f, 45f, 0f));
        var b = MakeStd("B", new Vector3(0f, 0.2f, 0.4f));

        var d = Diagnose(b, a, b.transform.position);

        Assert.IsFalse(d.wouldSnap);
        var n = d.neighbors[0];
        Assert.IsFalse(n.hasFacingFaces, "при 45° встречных параллельных граней нет");
        StringAssert.Contains("встречных", n.verdict);
    }

    [Test]
    public void SmallOverlap_DiagnosisReportsOverlapTooSmall()
    {
        // Смещение вбок: пересечение проекций мало (<30% меньшей грани).
        var a = MakeStd("A", new Vector3(0f, 0.2f, 0f));
        var b = MakeStd("B", new Vector3(0.76f, 0.2f, 0.028f)); // перекрытие по X всего 40мм из 800

        var d = Diagnose(b, a, b.transform.position);

        var n = d.neighbors[0];
        Assert.IsFalse(n.wouldSnap);
        Assert.IsTrue(n.hasFacingFaces);
        Assert.IsFalse(n.overlapEnough, $"перекрытие должно быть <30%, факт: {n.overlapRatio:P0}");
        StringAssert.Contains("перекрытие", n.verdict.ToLowerInvariant());
    }

    [Test]
    public void Diagnosis_DoesNotMoveBoard()
    {
        var a = MakeStd("A", new Vector3(0f, 0.2f, 0f));
        var b = MakeStd("B", new Vector3(0f, 0.2f, 0.3f));
        var before = b.transform.position;

        SnapSystem.Diagnose(b, new List<KitchenElement> { a }, new Vector3(5f, 5f, 5f));

        Assert.AreEqual(before, b.transform.position, "диагностика не должна двигать доску");
    }

    [Test]
    public void Diagnosis_SortsNeighborsByGap_AndLimitsCount()
    {
        var moved = MakeStd("M", new Vector3(0f, 0.2f, 0f));
        var others = new List<KitchenElement>();
        for (int i = 0; i < 7; i++)
            others.Add(MakeStd($"N{i}", new Vector3(0f, 0.2f, 0.1f * (i + 1))));

        var d = SnapSystem.Diagnose(moved, others, moved.transform.position, maxNeighbors: 5);

        Assert.AreEqual(5, d.neighbors.Count, "не больше maxNeighbors");
        Assert.AreEqual("N0", d.neighbors[0].name, "ближайший сосед — первым");
        for (int i = 1; i < d.neighbors.Count; i++)
            Assert.GreaterOrEqual(d.neighbors[i].gapMM, d.neighbors[i - 1].gapMM,
                "соседи отсортированы по зазору");
    }

    [Test]
    public void Diagnosis_AgreesWithTrySnap_OnScatteredPositions()
    {
        // Инвариант: neighbors[0].wouldSnap == реальный TrySnap для пары досок.
        var a = MakeStd("A", new Vector3(0f, 0.2f, 0f));
        var b = MakeStd("B", Vector3.zero);

        float[] offsets = { 0.019f, 0.03f, 0.05f, 0.068f, 0.069f, 0.1f, 0.2f };
        foreach (var dz in offsets)
        {
            var pos = new Vector3(0f, 0.2f, dz);
            var d = Diagnose(b, a, pos);
            var real = SnapSystem.TrySnap(b, new List<KitchenElement> { a }, pos);
            Assert.AreEqual(real.snapped, d.wouldSnap,
                $"dz={dz}: диагноз ({d.neighbors[0].verdict}) разошёлся с TrySnap");
        }
    }
}
