using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Связка «прилипание + растягивание» на снимках геометрии: без сцены,
/// поэтому исполняется и Unity, и обычным dotnet test. Сценозависимые проверки
/// (пересечение тел после ресайза, переключение режима ручек) остались в
/// ResizeMathSceneTests.</summary>
public class ResizeMathTests
{
    private static ElementGeometry Box(string name, Vector3 pos, Vector3Int dims)
        => ElementGeometry.Box(name, pos,
            new Vector3(dims.x, dims.y, dims.z) * AppConstants.MM_TO_UNITS);

    // Растягиваем грань faceIndex объекта на rawDelta вдоль её внешней нормали.
    private static void Resize(in ElementGeometry target, Vector3 targetPos, Vector3Int dims,
        int faceIndex, float rawDelta, IReadOnlyList<ElementGeometry> others, float threshold,
        out Vector3Int newDims, out Vector3 newCenter, out bool snapped)
    {
        var f = target.Faces[faceIndex];
        int axisIndex = faceIndex / 2;
        int dimMM = axisIndex == 0 ? dims.x : (axisIndex == 1 ? dims.y : dims.z);
        float sizeStart = dimMM * AppConstants.MM_TO_UNITS;

        ResizeMath.Compute(dims, axisIndex, f.normal.normalized, f.center, f.rightAxis, f.upAxis, f.size,
            targetPos, sizeStart, rawDelta, others, target,
            threshold > 0f, threshold, out newDims, out newCenter, out snapped);
    }

    [Test]
    public void FreeResize_NoNeighbour_GrowsByDelta()
    {
        var pos = new Vector3(0, 0.2f, 0);
        var dims = new Vector3Int(800, 400, 18);
        var a = Box("A", pos, dims);

        Resize(a, pos, dims, 0, 0.1f, new List<ElementGeometry>(), 0.05f,
            out var newDims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(900, newDims.x);          // 800 + 100 мм
        Assert.AreEqual(0.05f, center.x, 0.001f); // центр сместился на полделты
    }

    [Test]
    public void Resize_SnapsToOpposingBoard_Flush()
    {
        var pos = new Vector3(0, 0.2f, 0);
        var dims = new Vector3Int(800, 400, 18);
        var a = Box("A", pos, dims);
        // B: -X-грань на x=0.45 (зазор 0.05 от +X-грани A на x=0.4).
        var b = Box("B", new Vector3(0.85f, 0.2f, 0), dims);

        Resize(a, pos, dims, 0, 0.03f, new List<ElementGeometry> { b }, 0.05f,
            out var newDims, out var center, out bool snapped);

        Assert.IsTrue(snapped);
        Assert.AreEqual(850, newDims.x);
        // +X-грань A встаёт заподлицо с -X-гранью B (x=0.45).
        Assert.AreEqual(0.45f, center.x + newDims.x * AppConstants.MM_TO_UNITS * 0.5f, 0.001f);
    }

    [Test]
    public void Resize_LCornerPartialOverlap_Snaps()
    {
        // «Г»: грань A растёт к грани B, но перекрываются они лишь частично (угол).
        var pos = new Vector3(0, 0.2f, 0);
        var dims = new Vector3Int(800, 400, 18);
        var a = Box("A", pos, dims);
        // B смещён по Y → -X-грань B (Y∈[0.3,0.7]) перекрывает +X-грань A (Y∈[0,0.4])
        // только на участке Y∈[0.3,0.4] (≈25% — старый порог 0.3 это отбрасывал).
        var b = Box("B", new Vector3(0.85f, 0.5f, 0), dims);

        Resize(a, pos, dims, 0, 0.03f, new List<ElementGeometry> { b }, 0.05f,
            out var newDims, out _, out bool snapped);

        Assert.IsTrue(snapped, "частичное угловое перекрытие тоже должно прилипать");
        Assert.AreEqual(850, newDims.x);
    }

    [Test]
    public void Resize_WallBottom_SnapsToFloor()
    {
        var floor = Box("Floor", new Vector3(0, -0.009f, 0), new Vector3Int(3000, 18, 3000));
        // Стена парит на 0.25 м над полом (низ на y=0.25).
        var wallPos = new Vector3(0, 1.5f, 0);
        var wallDims = new Vector3Int(2000, 2500, 100);
        var wall = Box("Wall", wallPos, wallDims);

        // Тянем нижнюю (-Y) грань вниз на 0.22 → она в 0.03 от пола (порог 0.05).
        Resize(wall, wallPos, wallDims, 3, 0.22f, new List<ElementGeometry> { floor }, 0.05f,
            out var newDims, out var center, out bool snapped);

        Assert.IsTrue(snapped);
        Assert.AreEqual(2750, newDims.y);
        // Низ стены встаёт ровно на пол (y=0).
        Assert.AreEqual(0f, center.y - newDims.y * AppConstants.MM_TO_UNITS * 0.5f, 0.001f);
    }

    /// <summary>Снэп ставит грань заподлицо в НЕцелых мм, а округление до
    /// миллиметра могло удлинить деталь СКВОЗЬ плоскость соседа: перекрытие
    /// больше допуска валидатор считает пересечением — деталь краснела сразу
    /// после «сработавшего» снэпа, а глазом сдвиг не виден.</summary>
    [Test]
    public void Resize_SnapToOffMmNeighbour_NeverOvershootsIntoIt()
    {
        var pos = new Vector3(0, 0.2f, 0);
        var dims = new Vector3Int(800, 400, 18);
        var a = Box("A", pos, dims);
        // B: -X-грань на x=0.44963 — НЕ на целом мм от противоположной грани A.
        // Заподлицо требует ширины 849.63 мм; Round дал бы 850, и грань A зашла
        // бы на 0.37 мм ВНУТРЬ B.
        var b = Box("B", new Vector3(0.84963f, 0.2f, 0), dims);

        Resize(a, pos, dims, 0, 0.03f, new List<ElementGeometry> { b }, 0.05f,
            out var newDims, out var center, out bool snapped);

        Assert.IsTrue(snapped);
        Assert.AreEqual(849, newDims.x, "округление не должно перехлёстывать за грань соседа");
        float face = center.x + newDims.x * AppConstants.MM_TO_UNITS * 0.5f;
        Assert.LessOrEqual(face, 0.44963f + 1e-4f, "грань не заходит за снэп-плоскость");
    }

    [Test]
    public void Resize_WallBottom_BeyondThreshold_NoSnap()
    {
        var floor = Box("Floor", new Vector3(0, -0.009f, 0), new Vector3Int(3000, 18, 3000));
        var wallPos = new Vector3(0, 2f, 0); // низ на y=0.75
        var wallDims = new Vector3Int(2000, 2500, 100);
        var wall = Box("Wall", wallPos, wallDims);

        Resize(wall, wallPos, wallDims, 3, 0.1f, new List<ElementGeometry> { floor }, 0.05f,
            out var newDims, out _, out bool snapped);

        Assert.IsFalse(snapped, "пол слишком далеко — свободное растягивание");
        Assert.AreEqual(2600, newDims.y); // 2500 + 100 мм
    }

    // ── Габарит вдоль оси и центр по ПРИНЯТОМУ размеру ──────────────────

    [Test]
    public void DimAlong_PicksComponentByAxis()
    {
        var dims = new Vector3Int(800, 400, 18);

        Assert.AreEqual(800, ResizeMath.DimAlong(dims, 0));
        Assert.AreEqual(400, ResizeMath.DimAlong(dims, 1));
        Assert.AreEqual(18, ResizeMath.DimAlong(dims, 2));
    }

    /// <summary>Деталь вправе зажать запрошенный размер (у опоры высота
    /// ограничена 80..130 мм). Тогда центр из <c>Compute</c> посчитан для
    /// размера, которого нет: противоположная грань уезжает, деталь повисает
    /// в воздухе. Пересчёт от ПРИНЯТОГО размера оставляет её на месте.</summary>
    [Test]
    public void CenterForAppliedDims_KeepsOppositeFaceInPlace()
    {
        var start = new Vector3(0f, 0.5f, 0f);
        float sizeStart = 100f * AppConstants.MM_TO_UNITS;   // высота была 100 мм
        var applied = new Vector3Int(800, 130, 18);          // приняли 130, а не 200

        var center = ResizeMath.CenterForAppliedDims(start, Vector3.up, sizeStart, applied, 1);

        // Верх уехал на 30 мм, значит центр — на 15 мм.
        Assert.AreEqual(0.515f, center.y, 1e-5f);
        // Низ (противоположная грань) остался на месте: 0.5 − 0.05 = 0.515 − 0.065.
        Assert.AreEqual(start.y - sizeStart * 0.5f,
            center.y - applied.y * AppConstants.MM_TO_UNITS * 0.5f, 1e-5f);
    }

    [Test]
    public void CenterForAppliedDims_ShrinkMovesCenterBack()
    {
        var start = new Vector3(0f, 0.5f, 0f);
        float sizeStart = 200f * AppConstants.MM_TO_UNITS;
        var applied = new Vector3Int(800, 80, 18);           // зажали до минимума

        var center = ResizeMath.CenterForAppliedDims(start, Vector3.up, sizeStart, applied, 1);

        Assert.AreEqual(0.44f, center.y, 1e-5f, "размер уменьшился на 120 мм → центр на 60 мм вниз");
    }
}
