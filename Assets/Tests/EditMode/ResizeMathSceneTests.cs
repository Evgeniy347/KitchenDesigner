using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Остаток ResizeMathTests, которому НУЖНА сцена: проверка тел на
/// пересечение после ресайза и переключение режима ручек. Чистая математика
/// ресайза живёт в Assets/Tests/EditMode/Geometry/ResizeMathTests.cs и гоняется
/// ещё и под dotnet test.</summary>
public class ResizeMathSceneTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(Vector3 pos, Vector3Int dims)
    {
        var go = new GameObject("E");
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = dims;
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    /// <summary>Округление снэпа до миллиметра не должно оставлять деталь ВНУТРИ
    /// соседа: пересечение мельче допуска глазом не видно, а валидатор красит
    /// деталь сразу после «удачного» снэпа. Формулу проверяет ResizeMathTests;
    /// здесь — что тела после этого действительно не пересекаются.</summary>
    [Test]
    public void Resize_SnapToOffMmNeighbour_LeavesBodiesApart()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        var b = Make(new Vector3(0.84963f, 0.2f, 0), new Vector3Int(800, 400, 18));

        var f = a.GetFaces()[0];
        var dims = a.DimensionsMM;
        float sizeStart = dims.x * AppConstants.MM_TO_UNITS;

        ResizeMath.Compute(dims, 0, f.normal.normalized, f.center, f.rightAxis, f.upAxis, f.size,
            a.transform.position, sizeStart, 0.03f,
            new List<KitchenElement> { b }.ToGeometry(), a.ToGeometry(),
            snapEnabled: true, 0.05f, out var newDims, out var center, out bool snapped);

        Assert.IsTrue(snapped);

        a.DimensionsMM = newDims;
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
}
