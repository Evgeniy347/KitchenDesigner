using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Связка «прилипание + растягивание» (ResizeMath): детали и стены.</summary>
public class ResizeMathTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(Vector3 pos, Vector3Int dims, bool basePlate = false)
    {
        var go = new GameObject(basePlate ? "Floor" : "E");
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = dims;
        if (basePlate) go.AddComponent<BasePlate>();
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    // Растягиваем грань faceIndex объекта на rawDelta вдоль её внешней нормали.
    private static void Resize(KitchenElement target, int faceIndex, float rawDelta,
        IList<KitchenElement> others, float threshold,
        out Vector3Int newDims, out Vector3 newCenter, out bool snapped)
    {
        var f = target.GetFaces()[faceIndex];
        int axisIndex = faceIndex / 2;
        var dims = target.DimensionsMM;
        int dimMM = axisIndex == 0 ? dims.x : (axisIndex == 1 ? dims.y : dims.z);
        float sizeStart = dimMM * AppConstants.MM_TO_UNITS;

        ResizeMath.Compute(dims, axisIndex, f.normal.normalized, f.center, f.rightAxis, f.upAxis, f.size,
            target.transform.position, sizeStart, rawDelta, others.ToGeometry(), target.ToGeometry(),
            threshold > 0f, threshold, out newDims, out newCenter, out snapped);
    }

    [Test]
    public void FreeResize_NoNeighbour_GrowsByDelta()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, 0.1f, new List<KitchenElement>(), 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(900, dims.x);          // 800 + 100 мм
        Assert.AreEqual(0.05f, center.x, 0.001f); // центр сместился на полделты
    }

    [Test]
    public void Resize_SnapsToOpposingBoard_Flush()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        // B: -X-грань на x=0.45 (зазор 0.05 от +X-грани A на x=0.4).
        var b = Make(new Vector3(0.85f, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, 0.03f, new List<KitchenElement> { b }, 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsTrue(snapped);
        Assert.AreEqual(850, dims.x);
        // +X-грань A встаёт заподлицо с -X-гранью B (x=0.45).
        Assert.AreEqual(0.45f, center.x + dims.x * AppConstants.MM_TO_UNITS * 0.5f, 0.001f);
    }

    [Test]
    public void Resize_LCornerPartialOverlap_Snaps()
    {
        // «Г»: грань A растёт к грани B, но перекрываются они лишь частично (угол).
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        // B смещён по Y → -X-грань B (Y∈[0.3,0.7]) перекрывает +X-грань A (Y∈[0,0.4])
        // только на участке Y∈[0.3,0.4] (≈25% — старый порог 0.3 это отбрасывал).
        var b = Make(new Vector3(0.85f, 0.5f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, 0.03f, new List<KitchenElement> { b }, 0.05f,
            out var dims, out _, out bool snapped);

        Assert.IsTrue(snapped, "частичное угловое перекрытие тоже должно прилипать");
        Assert.AreEqual(850, dims.x);
    }

    [Test]
    public void Resize_WallBottom_SnapsToFloor()
    {
        var floor = Make(new Vector3(0, -0.009f, 0), new Vector3Int(3000, 18, 3000), basePlate: true);
        // Стена парит на 0.25 м над полом (низ на y=0.25).
        var wall = Make(new Vector3(0, 1.5f, 0), new Vector3Int(2000, 2500, 100));

        // Тянем нижнюю (-Y) грань вниз на 0.22 → она в 0.03 от пола (порог 0.05).
        Resize(wall, 3, 0.22f, new List<KitchenElement> { floor }, 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsTrue(snapped);
        Assert.AreEqual(2750, dims.y);
        // Низ стены встаёт ровно на пол (y=0).
        Assert.AreEqual(0f, center.y - dims.y * AppConstants.MM_TO_UNITS * 0.5f, 0.001f);
    }

    [Test]
    public void Resize_SnapToOffMmNeighbour_NeverOvershootsIntoIt()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        // B: -X-грань на x=0.44963 — НЕ на целом мм от противоположной грани A.
        // Заподлицо требует ширины 849.63 мм; Round дал бы 850, и грань A зашла
        // бы на 0.37 мм ВНУТРЬ B — невидимое глазу пересечение (красная
        // подсветка сразу после сработавшего снэпа).
        var b = Make(new Vector3(0.84963f, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, 0.03f, new List<KitchenElement> { b }, 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsTrue(snapped);
        Assert.AreEqual(849, dims.x, "округление не должно перехлёстывать за грань соседа");
        float face = center.x + dims.x * AppConstants.MM_TO_UNITS * 0.5f;
        Assert.LessOrEqual(face, 0.44963f + 1e-4f, "грань не заходит за снэп-плоскость");

        a.DimensionsMM = dims;
        a.transform.position = center;
        Assert.IsFalse(SnapSystem.ElementsIntersect(a, b),
            "после снэп-ресайза детали не должны пересекаться");
    }

    [Test]
    public void ToggleMode_FlipsResizeAndMove()
    {
        var start = ResizeHandleManager.Mode;
        ResizeHandleManager.ToggleMode();
        Assert.AreNotEqual(start, ResizeHandleManager.Mode);
        ResizeHandleManager.ToggleMode();
        Assert.AreEqual(start, ResizeHandleManager.Mode); // вернулись в исходный режим
    }

    [Test]
    public void Resize_WallBottom_BeyondThreshold_NoSnap()
    {
        var floor = Make(new Vector3(0, -0.009f, 0), new Vector3Int(3000, 18, 3000), basePlate: true);
        var wall = Make(new Vector3(0, 2f, 0), new Vector3Int(2000, 2500, 100)); // низ на y=0.75

        Resize(wall, 3, 0.1f, new List<KitchenElement> { floor }, 0.05f,
            out var dims, out _, out bool snapped);

        Assert.IsFalse(snapped, "пол слишком далеко — свободное растягивание");
        Assert.AreEqual(2600, dims.y); // 2500 + 100 мм
    }
}
