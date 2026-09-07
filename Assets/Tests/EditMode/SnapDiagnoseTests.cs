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
        // снэп сработает и разведёт детали заподлицо.
        Assert.IsTrue(d.wouldSnap, "снэп разведёт пересекающиеся детали");
        var n = d.neighbors[0];
        Assert.IsTrue(n.intersects, "AABB действительно пересекаются");
        Assert.IsTrue(n.wouldSnap, "face-pair разведёт детали");
        StringAssert.Contains("снэп сработает", n.verdict);
    }

    [Test]
    public void RotatedBoard_DiagnosisReportsNoFacingFaces()
    {
        // Поворот вокруг ДВУХ осей: ни одна грань не остаётся осевой
        // (при повороте вокруг одной оси грани этой оси остаются встречными).
        var a = MakeStd("A", new Vector3(0f, 0.2f, 0f), ManagedRotation.Euler(45f, 45f, 0f));
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

        Assert.AreEqual(before, b.transform.position, "диагностика не должна двигать деталь");
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
    public void Diagnosis_PicksTheSnappablePair_NotTheNearestOne()
    {
        // Стойка 18×18×800 и планка 10×18×800, поднятая по Y на 45 мм.
        // По X пара граней ближе всего (14 мм), но грани разъехались по Y и не
        // перекрываются вовсе; прилипание идёт по паре Y (27 мм, перекрытие 100%).
        var post = Make("Стойка", new Vector3Int(18, 18, 800), Vector3.zero);
        var rail = Make("Планка", new Vector3Int(10, 18, 800), new Vector3(0f, 0.045f, 0f));

        var real = SnapSystem.TrySnap(rail, new List<KitchenElement> { post }, rail.transform.position);
        Assume.That(real.snapped, Is.True, "проба должна быть прилипающей, иначе тест пуст");

        var n = Diagnose(rail, post, rail.transform.position).neighbors[0];

        Assert.AreEqual(1, n.movedFaceIndex / 2,
            "выбрана пара по оси Y (индекс/2 = 1), а не более близкая пара по X: "
            + "ранг важнее зазора. Отбор по одному зазору выдавал вердикт по "
            + "случайной ближней негодной паре — Diagnose говорил «не прилипнет» "
            + "там, где TrySnap прилипал по другой паре того же соседа");
        Assert.AreEqual(27f, n.gapMM, 0.5f, "зазор именно у пары Y");
        Assert.IsTrue(n.wouldSnap, n.verdict);
        Assert.AreEqual(real.snapped, n.wouldSnap, "диагноз обязан совпасть с TrySnap");
    }

    [Test]
    public void Diagnosis_NeighbourWithoutFacingFaces_IsRankedByCentreDistance()
    {
        var moved = MakeStd("M", new Vector3(0f, 0.2f, 0f));
        // У повёрнутой детали встречных граней нет, поэтому зазора (gapMM = -1)
        // для неё не существует; стоит она вчетверо дальше встречной.
        var turned = MakeStd("Повёрнутая", new Vector3(0f, 0.2f, 0.5f), ManagedRotation.Euler(45f, 45f, 0f));
        var facing = MakeStd("Встречная", new Vector3(0f, 0.2f, 0.05f));

        var d = SnapSystem.Diagnose(moved, new List<KitchenElement> { turned, facing },
            moved.transform.position);

        Assert.AreEqual(2, d.neighbors.Count);
        Assert.AreEqual("Встречная", d.neighbors[0].name,
            "у детали без встречных граней gapMM = -1, и сортировка прямо по нему "
            + "вынесла бы её в начало списка как «самую близкую»; такие соседи "
            + "ранжируются по расстоянию между центрами");
        Assert.Less(d.neighbors[1].gapMM, 0f, "у повёрнутой зазора нет");
        Assert.Greater(d.neighbors[1].centerDistanceMM, d.neighbors[0].gapMM,
            "повёрнутая и правда дальше — иначе порядок ничего не доказывает");
    }

    [Test]
    public void Diagnosis_WithAKnownSnapResult_MatchesTheRecomputedOne()
    {
        var a = MakeStd("A", new Vector3(0f, 0.2f, 0f));
        var b = MakeStd("B", new Vector3(0f, 0.2f, 0.028f));
        var pos = b.transform.position;
        var known = SnapSystem.TrySnap(b, new List<KitchenElement> { a }, pos);

        var recomputed = SnapSystem.Diagnose(b, new List<KitchenElement> { a }, pos);
        var reused = SnapSystem.Diagnose(b, new List<KitchenElement> { a }, pos, known);

        Assert.AreEqual(recomputed.wouldSnap, reused.wouldSnap,
            "прилипание — чистая функция от (moved, others, testPosition): передать "
            + "готовый результат и посчитать заново обязано быть одним и тем же, "
            + "иначе свип экономит второй проход ценой другого диагноза");
        Assert.AreEqual(recomputed.snapTarget, reused.snapTarget);
        Assert.AreEqual(recomputed.neighbors[0].verdict, reused.neighbors[0].verdict);
    }

    [Test]
    public void Diagnosis_FacingBoards_ReportBestDotAndTheFacePairAndTheOverlap()
    {
        var a = MakeStd("A", new Vector3(0f, 0.2f, 0f));
        var b = MakeStd("B", new Vector3(0f, 0.2f, 0.028f));

        var n = Diagnose(b, a, b.transform.position).neighbors[0];

        Assert.AreEqual(-1f, n.bestDot, 1e-3f,
            "bestDot — самый встречный dot нормалей: -1 у строго встречных граней");
        Assert.AreEqual(2, n.movedFaceIndex / 2, "пара найдена по оси Z (индекс/2 = 2)");
        Assert.AreEqual(2, n.otherFaceIndex / 2);
        Assert.AreEqual(1f, n.overlapRatio, 1e-3f, "грани совпадают — перекрытие 100%");
        Assert.AreEqual(28f, n.centerDistanceMM, 0.5f,
            "centerDistanceMM меряется от примеряемой позиции до центра соседа");
    }

    [Test]
    public void Diagnosis_AgreesWithTrySnap_OnScatteredPositions()
    {
        // Инвариант: neighbors[0].wouldSnap == реальный TrySnap для пары деталей.
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
