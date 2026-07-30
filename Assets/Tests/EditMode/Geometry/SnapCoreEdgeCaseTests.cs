using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Граничные условия снэпа на СНИМКАХ: порог расстояния, доля
/// перекрытия граней, геометрические аномалии.
///
/// Перенос сценового `SnapEdgeCaseTests` в ядро (этап 5 плана). В Unity-версии
/// осталось только то, что без сцены не имеет смысла: чтение `KitchenSettings`
/// и отключённые (`SetActive(false)`) детали — ядро ни настроек, ни сцены не
/// видит, порог оно получает аргументом, а снимки строит вызывающий.</summary>
public class SnapCoreEdgeCaseTests : SnapCoreTestBase
{
    // ── Порог расстояния (50 мм) ────────────────────────────────────────

    [Test]
    public void Threshold_49mm_Snaps()
    {
        AssertSnappedAt(MakeStd("B"), Std("A", Vector3.zero),
            new Vector3(0.849f, 0f, 0f), new Vector3(0.80f, 0f, 0f));
    }

    [Test]
    public void Threshold_50mm_BoundarySnaps()
    {
        AssertSnappedAt(MakeStd("B"), Std("A", Vector3.zero),
            new Vector3(0.85f, 0f, 0f), new Vector3(0.80f, 0f, 0f),
            "ровно на пороге — прилипает (инклюзивный порог)");
    }

    [Test]
    public void Threshold_51mm_NoSnap()
    {
        AssertNotSnapped(MakeStd("B"), Std("A", Vector3.zero),
            new Vector3(0.851f, 0f, 0f), "51 мм > 50 мм");
    }

    [Test]
    public void Threshold_100mm_NoSnap()
    {
        AssertNotSnapped(MakeStd("B"), Std("A", Vector3.zero), new Vector3(0.9f, 0f, 0f));
    }

    [Test]
    public void MovingFarAway_NoSnap()
    {
        AssertNotSnapped(MakeStd("B"), Std("A", Vector3.zero), new Vector3(2.0f, 0f, 0f));
    }

    // ── Доля перекрытия граней (нужно ≥30%) ─────────────────────────────

    [Test]
    public void Overlap_Full_Snaps()
    {
        AssertSnappedAt(MakeStd("B"), Std("A", Vector3.zero),
            new Vector3(0.83f, 0f, 0f), new Vector3(0.80f, 0f, 0f));
    }

    [Test]
    public void Overlap_Above30Percent_Snaps()
    {
        // Сдвиг по Y на 270 мм: перекрытие граней ±X = (400−270)/400 = 32.5%.
        var r = Snap(MakeStd("B"), Std("A", Vector3.zero), new Vector3(0.83f, 0.27f, 0f));
        Assert.IsTrue(r.snapped, "перекрытие 32.5% > 30% — прилипает");
    }

    [Test]
    public void Overlap_Below30Percent_NoSnap()
    {
        // Сдвиг по Y на 310 мм: перекрытие = (400−310)/400 = 22.5%.
        AssertNotSnapped(MakeStd("B"), Std("A", Vector3.zero),
            new Vector3(0.83f, 0.31f, 0f), "перекрытие 22.5% < 30%");
    }

    [Test]
    public void Overlap_None_ParallelFacesApart_NoSnap()
    {
        // Грани ±X параллельны, но по Y не перекрываются вовсе.
        AssertNotSnapped(MakeStd("B"), Std("A", Vector3.zero), new Vector3(0.83f, 0.5f, 0f));
    }

    // ── Геометрические аномалии ─────────────────────────────────────────

    [Test]
    public void Intersecting_ThroughBody_SnapPushesApart()
    {
        // Детали смещены по X на 200 мм и пересекаются телами; ближайший выход —
        // встык по ±Z (толщина 18 мм).
        var r = Snap(MakeStd("B"), Std("A", Vector3.zero), new Vector3(0.2f, 0f, 0f));

        Assert.IsTrue(r.snapped, "снэп разведёт пересекающиеся детали");
        Assert.AreEqual(0.2f, r.position.x, Tol, "X не изменился");
        Assert.AreEqual(-0.018f, r.position.z, Tol, "Z — встык по Z-граням");
    }

    [Test]
    public void Touching_Gap0_IsNotIntersection()
    {
        var a = Std("A", Vector3.zero);
        var b = Std("B", new Vector3(0.8f, 0f, 0f)); // ровно встык

        Assert.IsFalse(Intersect(a, b),
            "идеально прилегающие гранью детали не считаются пересекающимися");
    }

    [Test]
    public void VeryLargeBoards_Snap()
    {
        var a = At(Make("A", new Vector3Int(5000, 5000, 18)), Vector3.zero);
        AssertSnappedAt(Make("B", new Vector3Int(5000, 5000, 18)), a,
            new Vector3(5.03f, 0f, 0f), new Vector3(5.0f, 0f, 0f));
    }

    [Test]
    public void VerySmallBoards_Snap()
    {
        var a = At(Make("A", new Vector3Int(20, 20, 20)), Vector3.zero);
        // Полуширина 10 мм → встык по X центр B = 0.02.
        AssertSnappedAt(Make("B", new Vector3Int(20, 20, 20)), a,
            new Vector3(0.025f, 0f, 0f), new Vector3(0.02f, 0f, 0f));
    }

    [Test]
    public void LargeCoordinate_1000m_StillSnaps()
    {
        var a = Std("A", new Vector3(1000f, 0f, 0f));
        var r = Snap(MakeStd("B"), a, new Vector3(1000.83f, 0f, 0f));

        Assert.IsTrue(r.snapped, "на 1000 м снэп ещё работает (float-точность достаточна)");
        Assert.AreEqual(1000.8f, r.position.x, 0.005f);
    }

    /// <summary>Баг: деталь 400×400 не липла к 1200×600, стоящей перпендикулярно.
    /// Узкие грани (18 мм) пересекаются под прямым углом: площадь перекрытия
    /// 18×18 = 4.5% от меньшей грани — ниже порога 30% ПО ПЛОЩАДИ, но 100% по
    /// каждой полуоси. Ради этого случая перекрытие и считается полуосевым.</summary>
    [Test]
    public void Overlap_PerpendicularEdgeFaces_Snaps()
    {
        // Сценовый аналог задаёт поворот как Quaternion.Euler(90, 270, 0). В ядре
        // Euler запрещён (вызов в нативный движок), поэтому тот же поворот
        // собирается умножением: Unity применяет углы в порядке Z→X→Y.
        var rotation = RotY(270f) * RotX(90f);
        var big = At(Make("big", new Vector3Int(1200, 600, 18), rotation),
            new Vector3(-0.5592f, 0.3f, -0.948f));
        var small = Make("small", new Vector3Int(400, 400, 18));

        var r = Snap(small, big, new Vector3(-0.060f, 0.2985f, -0.824f));

        Assert.IsTrue(r.snapped, "перпендикулярные кромки 18×400 и 18×1200 — прилипание");
        Assert.AreEqual(-0.0592f, r.position.x, Tol, "заподлицо с гранью большой детали");
    }
}
