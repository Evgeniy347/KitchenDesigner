using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Measure;

/// <summary>Математика рулетки: проекция на ось, определение оси отрезка,
/// «помощь попадания» по вершине и по отрезку, формат подписи.</summary>
public class MeasureGeometryTests
{
    private const float Mm = AppConstants.MM_TO_UNITS;

    [TearDown]
    public void Teardown() => MeasureMode.Reset();

    // ── Проекция на доминирующую ось ────────────────────────────────────

    [Test]
    public void MeasureGeometry_ProjectOnDominantAxis_PicksX()
    {
        var anchor = new Vector3(1f, 2f, 3f);
        var end = MeasureGeometry.ProjectOnDominantAxis(anchor, anchor + new Vector3(0.5f, 0.2f, 0.1f));
        Assert.AreEqual(new Vector3(1.5f, 2f, 3f), end);
    }

    [Test]
    public void MeasureGeometry_ProjectOnDominantAxis_PicksY()
    {
        var anchor = new Vector3(1f, 2f, 3f);
        var end = MeasureGeometry.ProjectOnDominantAxis(anchor, anchor + new Vector3(-0.1f, -0.7f, 0.3f));
        Assert.AreEqual(new Vector3(1f, 1.3f, 3f), end);
    }

    [Test]
    public void MeasureGeometry_ProjectOnDominantAxis_PicksZ()
    {
        var anchor = Vector3.zero;
        var end = MeasureGeometry.ProjectOnDominantAxis(anchor, new Vector3(0.2f, 0.3f, -0.9f));
        Assert.AreEqual(new Vector3(0f, 0f, -0.9f), end);
    }

    // ── Ось отрезка ─────────────────────────────────────────────────────

    [TestCase(1f, 0f, 0f, 0)]
    [TestCase(0f, 1f, 0f, 1)]
    [TestCase(0f, 0f, 1f, 2)]
    public void MeasureGeometry_AxisOf_ReturnsAxisForAlignedSegment(float dx, float dy, float dz, int expected)
    {
        Assert.AreEqual(expected, MeasureGeometry.AxisOf(Vector3.zero, new Vector3(dx, dy, dz)));
    }

    [Test]
    public void MeasureGeometry_AxisOf_ReturnsMinusOneForDiagonal()
    {
        Assert.AreEqual(-1, MeasureGeometry.AxisOf(Vector3.zero, new Vector3(1f, 1f, 0f)));
    }

    [Test]
    public void MeasureGeometry_AxisOf_ReturnsMinusOneForDegenerate()
    {
        Assert.AreEqual(-1, MeasureGeometry.AxisOf(Vector3.zero, Vector3.zero));
    }

    /// <summary>Отклонение мельче геометрического шума (0.1 мм) — отрезок всё
    /// ещё считается осевым: вершины деталей приходят с float-погрешностью.</summary>
    [Test]
    public void MeasureGeometry_AxisOf_TreatsSubEpsilonDriftAsAligned()
    {
        var b = new Vector3(1f, Tolerance.EpsilonUnits * 0.5f, 0f);
        Assert.AreEqual(0, MeasureGeometry.AxisOf(Vector3.zero, b));
    }

    [Test]
    public void MeasureGeometry_AxisOf_TreatsAboveEpsilonDriftAsDiagonal()
    {
        var b = new Vector3(1f, Tolerance.EpsilonUnits * 2f, 0f);
        Assert.AreEqual(-1, MeasureGeometry.AxisOf(Vector3.zero, b));
    }

    // ── Помощь попадания ────────────────────────────────────────────────

    [Test]
    public void MeasureGeometry_NearestIndex_PicksClosestInsideRadius()
    {
        var points = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(4, 0) };
        Assert.AreEqual(2, MeasureGeometry.NearestIndex(points, new Vector2(5, 0), 18f));
    }

    [Test]
    public void MeasureGeometry_NearestIndex_ReturnsMinusOneOutsideRadius()
    {
        var points = new List<Vector2> { new Vector2(0, 0) };
        Assert.AreEqual(-1, MeasureGeometry.NearestIndex(points, new Vector2(50, 0), 18f));
    }

    [Test]
    public void MeasureGeometry_NearestIndex_ReturnsMinusOneForEmptyList()
    {
        Assert.AreEqual(-1, MeasureGeometry.NearestIndex(new List<Vector2>(), Vector2.zero, 18f));
    }

    [Test]
    public void MeasureGeometry_DistancePointToSegmentPx_ProjectsInside()
    {
        float d = MeasureGeometry.DistancePointToSegmentPx(
            new Vector2(0, 0), new Vector2(100, 0), new Vector2(50, 7));
        Assert.AreEqual(7f, d, 0.001f);
    }

    /// <summary>За торцом отрезка расстояние считается до конца, а не до прямой —
    /// иначе клик далеко за концом «попадал» бы в отрезок.</summary>
    [Test]
    public void MeasureGeometry_DistancePointToSegmentPx_ClampsBeyondEnd()
    {
        float d = MeasureGeometry.DistancePointToSegmentPx(
            new Vector2(0, 0), new Vector2(100, 0), new Vector2(130, 40));
        Assert.AreEqual(50f, d, 0.001f);
    }

    [Test]
    public void MeasureGeometry_DistancePointToSegmentPx_HandlesDegenerateSegment()
    {
        float d = MeasureGeometry.DistancePointToSegmentPx(
            new Vector2(5, 5), new Vector2(5, 5), new Vector2(5, 9));
        Assert.AreEqual(4f, d, 0.001f);
    }

    // ── Подпись ─────────────────────────────────────────────────────────

    [Test]
    public void MeasureGeometry_FormatMm_RoundsToWholeMillimetres()
    {
        Assert.AreEqual("718 мм", MeasureGeometry.FormatMm(717.6f * Mm, axisAligned: true));
    }

    [Test]
    public void MeasureGeometry_FormatMm_PrefixesAngleGlyphForDiagonal()
    {
        string text = MeasureGeometry.FormatMm(500f * Mm, axisAligned: false);
        StringAssert.StartsWith(KitchenDesigner.Core.UI.UIStyle.GlyphAngle, text);
        StringAssert.EndsWith("500 мм", text);
    }

    // ── Режим и хранилище ───────────────────────────────────────────────

    [Test]
    public void MeasureSegment_LengthMm_UsesMillimetres()
    {
        var seg = new MeasureSegment(Vector3.zero, new Vector3(0.6f, 0f, 0f));
        Assert.AreEqual(600f, seg.LengthMm, 0.001f);
        Assert.AreEqual(0, seg.Axis);
    }

    [Test]
    public void MeasureMode_SetActiveFalse_ClearsSegments()
    {
        MeasureMode.SetActive(true);
        MeasureStore.Add(new MeasureSegment(Vector3.zero, Vector3.right));
        MeasureStore.Select(MeasureStore.Segments[0]);

        MeasureMode.SetActive(false);

        Assert.AreEqual(0, MeasureStore.Segments.Count);
        Assert.IsNull(MeasureStore.Selected);
    }

    [Test]
    public void MeasureStore_Remove_DropsSelection()
    {
        var seg = new MeasureSegment(Vector3.zero, Vector3.up);
        MeasureStore.Add(seg);
        MeasureStore.Select(seg);

        MeasureStore.Remove(seg);

        Assert.AreEqual(0, MeasureStore.Segments.Count);
        Assert.IsNull(MeasureStore.Selected);
    }
}
