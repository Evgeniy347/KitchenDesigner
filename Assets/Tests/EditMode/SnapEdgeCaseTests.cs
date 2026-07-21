using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Граничные условия SnapSystem: настройки, пороги расстояния, перекрытие,
/// геометрические аномалии. Предпосылки выверены под реальную семантику снэпа.
/// </summary>
public class SnapEdgeCaseTests : SnapTestBase
{
    // ===== Настройки =====

    [Test]
    public void SnapDisabled_ReturnsNotSnapped()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertNotSnapped(b, a, new Vector3(0.83f, 0f, 0f));
    }

    [Test]
    public void ThresholdMin1mm_GapAboveIt_NoSnap()
    {
        // Порог клампится к ≥1 мм. Ставим 1 мм и зазор 5 мм → нет снэпа.
        KitchenSettings.Instance.SnapThreshold = 1f;
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertNotSnapped(b, a, new Vector3(0.805f, 0f, 0f), "зазор 5 мм > порога 1 мм");
    }

    [Test]
    public void ThresholdMin1mm_WithinIt_Snaps()
    {
        KitchenSettings.Instance.SnapThreshold = 1f;
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        // зазор 0.5 мм < порога 1 мм
        AssertSnappedAt(b, a, new Vector3(0.8005f, 0f, 0f), new Vector3(0.80f, 0f, 0f));
    }

    [Test]
    public void ThresholdLarge_SnapsFarBoards()
    {
        KitchenSettings.Instance.SnapThreshold = 1000f;
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(1.2f, 0f, 0f), new Vector3(0.80f, 0f, 0f),
            "при большом пороге далёкая деталь притягивается");
    }

    // ===== Пороги расстояния (порог 50 мм) =====

    [Test]
    public void Threshold_49mm_Snaps()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(0.849f, 0f, 0f), new Vector3(0.80f, 0f, 0f));
    }

    [Test]
    public void Threshold_50mm_BoundarySnaps()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(0.85f, 0f, 0f), new Vector3(0.80f, 0f, 0f),
            "ровно на пороге — прилипает (инклюзивный порог)");
    }

    [Test]
    public void Threshold_51mm_NoSnap()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertNotSnapped(b, a, new Vector3(0.851f, 0f, 0f), "51 мм > 50 мм");
    }

    [Test]
    public void Threshold_100mm_NoSnap()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertNotSnapped(b, a, new Vector3(0.9f, 0f, 0f));
    }

    // ===== Перекрытие граней (нужно ≥30%) =====

    [Test]
    public void Overlap_Full_Snaps()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(0.83f, 0f, 0f), new Vector3(0.80f, 0f, 0f));
    }

    [Test]
    public void Overlap_Above30Percent_Snaps()
    {
        // Сдвиг по Y на 270 мм: перекрытие граней ±X = (400-270)/400 = 32.5% > 30%.
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        var r = Snap(b, a, new Vector3(0.83f, 0.27f, 0f));
        Assert.IsTrue(r.snapped, "перекрытие 32.5% > 30% — прилипает");
    }

    [Test]
    public void Overlap_Above10Percent_Snaps()
    {
        // Сдвиг по Y на 310 мм: перекрытие = (400-310)/400 = 22.5% > 10%.
        // С новым MinSnapOverlap=10% — должен прилипнуть.
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(0.83f, 0.31f, 0f), new Vector3(0.80f, 0.31f, 0f),
            "перекрытие 22.5% > 10% — должен прилипнуть");
    }

    [Test]
    public void Overlap_Below10Percent_NoSnap()
    {
        // Сдвиг по Y на 370 мм: перекрытие = (400-370)/400 = 7.5% < 10%.
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertNotSnapped(b, a, new Vector3(0.83f, 0.37f, 0f), "перекрытие 7.5% < 10%");
    }

    [Test]
    public void Overlap_None_ParallelFacesApart_NoSnap()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        // Грани ±X параллельны, но B поднята так, что не перекрываются по Y вовсе.
        AssertNotSnapped(b, a, new Vector3(0.83f, 0.5f, 0f));
    }

    // ===== Геометрические аномалии =====

    [Test]
    public void Intersecting_ThroughBody_NoSnap()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        // детали смещены по X на 200 мм, но грани ±Z в зазоре 18 мм —
        // снэп разведёт детали по Z встык.
        var r = Snap(b, a, new Vector3(0.2f, 0f, 0f));
        Assert.IsTrue(r.snapped, "снэп разведёт пересекающиеся детали");
        Assert.AreEqual(0.2f, r.position.x, Tol, "X не изменился");
        Assert.AreEqual(-0.018f, r.position.z, Tol, "Z — встык по Z-граням");
    }

    [Test]
    public void Touching_Gap0_NotCountedAsIntersection()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", new Vector3(0.8f, 0f, 0f)); // ровно встык
        Assert.IsFalse(SnapSystem.ElementsIntersect(a, b),
            "идеально прилегающие гранью детали не считаются пересекающимися");
    }

    [Test]
    public void MovingAwayFromTarget_NoSnap()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertNotSnapped(b, a, new Vector3(2.0f, 0f, 0f));
    }

    [Test]
    public void VeryLargeBoards_Snap()
    {
        var a = Make("A", new Vector3Int(5000, 5000, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(5000, 5000, 18), Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(5.03f, 0f, 0f), new Vector3(5.0f, 0f, 0f));
    }

    [Test]
    public void VerySmallBoards_Snap()
    {
        var a = Make("A", new Vector3Int(20, 20, 20), Vector3.zero);
        var b = Make("B", new Vector3Int(20, 20, 20), Vector3.zero);
        // полуширина 10 мм → встык по X центр B = 0.02
        AssertSnappedAt(b, a, new Vector3(0.025f, 0f, 0f), new Vector3(0.02f, 0f, 0f));
    }

    [Test]
    public void LargeCoordinate_1000m_StillSnaps()
    {
        var a = MakeStd("A", new Vector3(1000f, 0f, 0f));
        var b = MakeStd("B", new Vector3(1000f, 0f, 0f));
        var r = Snap(b, a, new Vector3(1000.83f, 0f, 0f));
        Assert.IsTrue(r.snapped, "на 1000 м снэп ещё работает (float-точность достаточна)");
        Assert.AreEqual(1000.8f, r.position.x, 0.005f);
    }

    [Test]
    public void DisabledTarget_NoSnap()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        a.gameObject.SetActive(false);
        AssertNotSnapped(b, a, new Vector3(0.83f, 0f, 0f), "отключённая цель не участвует");
    }

    [Test]
    public void DisabledMoved_NoSnap()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        b.gameObject.SetActive(false);
        AssertNotSnapped(b, a, new Vector3(0.83f, 0f, 0f), "отключённая деталь не снэпается");
    }

    // ===== Перпендикулярные узкие грани (баг: 400×400 не липла к 1200×600) =====

    [Test]
    public void Overlap_PerpendicularEdgeFaces_Snaps()
    {
        // деталь 1200×600 повёрнута на (90,270,0); деталь 400×400 без поворота.
        // Узкие грани (18 мм) пересекаются под прямым углом: площадь перекрытия
        // 18×18 = 324 мм², что составляет 4.5% от min(7200, 21600) = 7200 мм² —
        // ниже порога 30% по площади, но 100% от меньшей полуоси каждой грани.
        var big = Make("big", new Vector3Int(1200, 600, 18), new Vector3(-0.5592f, 0.3f, -0.948f),
            Quaternion.Euler(90f, 270f, 0f));
        var small = Make("small", new Vector3Int(400, 400, 18), Vector3.zero);

        // Ожидаемое прилипание: центр small по X = -0.0592 (заподлицо с гранью big),
        // Y = 0.30 (центровка кромок), Z = -0.824 (без сдвига — вне порога по Z).
        AssertFlushContact(small, big, new Vector3(-0.060f, 0.2985f, -0.824f),
            "перпендикулярные кромки 18×400 и 18×1200 — прилипание");
    }
}
