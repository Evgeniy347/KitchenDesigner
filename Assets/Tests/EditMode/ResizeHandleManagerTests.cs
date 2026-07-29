using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ResizeHandleManagerTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(Vector3 pos, Vector3Int dims, bool transparent = false)
    {
        var go = new GameObject("E");
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = dims;
        if (transparent) e.Transparent = true;
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    private static void Resize(KitchenElement target, int faceIndex, float rawDelta,
        IList<KitchenElement> others, float threshold,
        out Vector3Int newDims, out Vector3 newCenter, out bool snapped)
    {
        var f = target.GetFaces()[faceIndex];
        int axisIndex = faceIndex / 2;
        var dims = target.DimensionsMM;
        int dimMM = ResizeMath.DimAlong(dims, axisIndex);
        float sizeStart = dimMM * AppConstants.MM_TO_UNITS;

        ResizeMath.Compute(dims, axisIndex, f.normal.normalized, f.center, f.rightAxis, f.upAxis, f.size,
            target.transform.position, sizeStart, rawDelta, others, target,
            threshold > 0f, threshold, out newDims, out newCenter, out snapped);
    }

    [Test]
    public void ToggleMode_CyclesBetweenResizeAndMove()
    {
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
        Assert.AreEqual(ResizeHandleManager.HandleMode.Resize, ResizeHandleManager.Mode);

        ResizeHandleManager.ToggleMode();
        Assert.AreEqual(ResizeHandleManager.HandleMode.Move, ResizeHandleManager.Mode);

        ResizeHandleManager.ToggleMode();
        Assert.AreEqual(ResizeHandleManager.HandleMode.Resize, ResizeHandleManager.Mode);
    }

    [Test]
    public void SetMode_SetsExactMode()
    {
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Move);
        Assert.AreEqual(ResizeHandleManager.HandleMode.Move, ResizeHandleManager.Mode);

        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
        Assert.AreEqual(ResizeHandleManager.HandleMode.Resize, ResizeHandleManager.Mode);

        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Move);
        Assert.AreEqual(ResizeHandleManager.HandleMode.Move, ResizeHandleManager.Mode);
    }

    [Test]
    public void IsResizing_DefaultFalse()
    {
        Assert.IsFalse(ResizeHandleManager.IsResizing);
    }

    [Test]
    public void IsResizingElement_NotResizing_ReturnsFalse()
    {
        var e = Make(Vector3.zero, new Vector3Int(800, 400, 18));
        Assert.IsFalse(ResizeHandleManager.IsResizingElement(e));
    }

    [Test]
    public void IsResizingElement_Null_ReturnsFalse()
    {
        Assert.IsFalse(ResizeHandleManager.IsResizingElement(null!));
    }

    private static ResizeHandle MakeHandle(int faceIndex, KitchenElement? parent = null)
    {
        var go = new GameObject($"H_{faceIndex}");
        go.transform.SetParent(parent != null ? parent.transform : null, false);
        var h = go.AddComponent<ResizeHandle>();
        h.faceIndex = faceIndex;
        return h;
    }

    /* [Test] — disabled: RaycastHit.collider is read-only in Unity 6
    public void PickHandleFromHits_SingleHandle_ReturnsIt()
    {
        var element = Make(Vector3.zero, new Vector3Int(800, 400, 18));
        var handle = MakeHandle(0, element);

        var go = new GameObject("Col");
        go.transform.SetParent(handle.transform, false);
        var col = go.AddComponent<BoxCollider>();
        var hits = new RaycastHit[] { new RaycastHit { collider = col } };

        var result = ResizeHandleManager.PickHandleFromHits(hits, shiftHeld: false);
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.faceIndex);
    }
    */

    /* [Test] — disabled: RaycastHit.collider is read-only in Unity 6
    public void PickHandleFromHits_NoHandleInHits_ReturnsNull()
    {
        var go = new GameObject("Go");
        var col = go.AddComponent<BoxCollider>();
        _spawned.Add(go);

        var hits = new RaycastHit[] { new RaycastHit { collider = col } };
        var result = ResizeHandleManager.PickHandleFromHits(hits, shiftHeld: false);
        Assert.IsNull(result);
    }
    */

    [Test]
    public void PickHandleFromHits_EmptyArray_ReturnsNull()
    {
        var result = ResizeHandleManager.PickHandleFromHits(
            System.Array.Empty<RaycastHit>(), shiftHeld: false);
        Assert.IsNull(result);
    }

    /* [Test] — disabled: RaycastHit.collider is read-only in Unity 6
    public void PickHandleFromHits_ShiftSkipsTransparentElement()
    {
        var transparent = Make(Vector3.zero, new Vector3Int(800, 400, 18), transparent: true);
        var solid = Make(Vector3.zero, new Vector3Int(800, 400, 18), transparent: false);

        var tHandle = MakeHandle(0, transparent);
        var sHandle = MakeHandle(1, solid);

        var tGo = new GameObject("ColT");
        tGo.transform.SetParent(tHandle.transform, false);
        var tCol = tGo.AddComponent<BoxCollider>();

        var sGo = new GameObject("ColS");
        sGo.transform.SetParent(sHandle.transform, false);
        var sCol = sGo.AddComponent<BoxCollider>();

        var hits = new RaycastHit[]
        {
            new RaycastHit { collider = tCol },
            new RaycastHit { collider = sCol },
        };

        var normal = ResizeHandleManager.PickHandleFromHits(hits, shiftHeld: false);
        Assert.IsNotNull(normal);
        Assert.AreEqual(0, normal.faceIndex);

        var shifted = ResizeHandleManager.PickHandleFromHits(hits, shiftHeld: true);
        Assert.IsNotNull(shifted);
        Assert.AreEqual(1, shifted.faceIndex);

        Object.DestroyImmediate(tGo);
        Object.DestroyImmediate(sGo);
    }
    */

    /* [Test] — disabled: RaycastHit.collider is read-only in Unity 6
    public void PickHandleFromHits_ShiftWithoutTransparent_ReturnsFirst()
    {
        var a = Make(Vector3.zero, new Vector3Int(800, 400, 18));
        var b = Make(Vector3.zero, new Vector3Int(800, 400, 18));

        var ha = MakeHandle(0, a);
        var hb = MakeHandle(1, b);

        var ga = new GameObject("ColA");
        ga.transform.SetParent(ha.transform, false);
        var ca = ga.AddComponent<BoxCollider>();

        var gb = new GameObject("ColB");
        gb.transform.SetParent(hb.transform, false);
        var cb = gb.AddComponent<BoxCollider>();

        var hits = new RaycastHit[]
        {
            new RaycastHit { collider = ca },
            new RaycastHit { collider = cb },
        };

        var result = ResizeHandleManager.PickHandleFromHits(hits, shiftHeld: true);
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.faceIndex);

        Object.DestroyImmediate(ga);
        Object.DestroyImmediate(gb);
    }
    */

    [Test]
    public void FreeResize_NegativeDelta_ShrinksElement()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, -0.1f, new List<KitchenElement>(), 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(700, dims.x);
        Assert.AreEqual(-0.05f, center.x, 0.001f);
    }

    [Test]
    public void FreeResize_LargeNegativeDelta_ClampedAt1mm()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, -10f, new List<KitchenElement>(), 0f,
            out var dims, out _, out _);

        Assert.AreEqual(1, dims.x);
    }

    [Test]
    public void FreeResize_MinClamp_OneMm()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, -0.799f, new List<KitchenElement>(), 0f,
            out var dims, out _, out _);

        Assert.GreaterOrEqual(dims.x, 1);
    }

    [Test]
    public void FreeResize_AlongY_Axis()
    {
        var a = Make(new Vector3(0, 0.5f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 2, 0.1f, new List<KitchenElement>(), 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(500, dims.y);
        Assert.AreEqual(0.55f, center.y, 0.001f);
    }

    [Test]
    public void FreeResize_AlongZ_Axis()
    {
        var a = Make(new Vector3(0, 0.2f, 0.2f), new Vector3Int(800, 400, 18));

        Resize(a, 4, 0.05f, new List<KitchenElement>(), 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(68, dims.z);
        Assert.AreEqual(0.225f, center.z, 0.001f);
    }

    [Test]
    public void FreeResize_NegativeDirection_OppositeFace()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 1, 0.1f, new List<KitchenElement>(), 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(900, dims.x);
        Assert.AreEqual(-0.05f, center.x, 0.001f);
    }

    [Test]
    public void SnapResize_OnY_Axis_FlushToFloor()
    {
        var floor = Make(new Vector3(0, 0f, 0), new Vector3Int(3000, 18, 3000));
        floor.gameObject.AddComponent<BasePlate>();
        var wall = Make(new Vector3(0, 1.5f, 0), new Vector3Int(2000, 2500, 100));

        Resize(wall, 3, 0.22f, new List<KitchenElement> { floor }, 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsTrue(snapped);
        Assert.AreEqual(2741, dims.y);
        Assert.AreEqual(0.009f, center.y - dims.y * AppConstants.MM_TO_UNITS * 0.5f, 0.001f);
    }

    [Test]
    public void SnapResize_OnZ_Axis_BetweenBoards()
    {
        var a = Make(new Vector3(0, 0.2f, 0.5f), new Vector3Int(800, 400, 18));
        var b = Make(new Vector3(0, 0.2f, 0.85f), new Vector3Int(800, 400, 18));

        Resize(a, 4, 0.32f, new List<KitchenElement> { b }, 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsTrue(snapped);

        a.DimensionsMM = dims;
        a.transform.position = center;

        Assert.AreEqual(350, dims.z);
        Assert.IsFalse(SnapSystem.ElementsIntersect(a, b));
    }

    [Test]
    public void Resize_ZeroDelta_ReturnsOriginalDims()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, 0f, new List<KitchenElement>(), 0f,
            out var dims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(800, dims.x);
        Assert.AreEqual(0f, center.x, 0.001f);
    }

    [Test]
    public void DimAlong_ReturnsCorrectAxis()
    {
        var dims = new Vector3Int(800, 400, 18);
        Assert.AreEqual(800, ResizeMath.DimAlong(dims, 0));
        Assert.AreEqual(400, ResizeMath.DimAlong(dims, 1));
        Assert.AreEqual(18, ResizeMath.DimAlong(dims, 2));
    }

    [Test]
    public void CenterForAppliedDims_SameSize_NoShift()
    {
        var center = ResizeMath.CenterForAppliedDims(
            new Vector3(1f, 0.5f, 0f), Vector3.right, 0.8f,
            new Vector3Int(800, 400, 18), 0);

        Assert.AreEqual(1f, center.x, 0.001f);
        Assert.AreEqual(0.5f, center.y, 0.001f);
    }

    [Test]
    public void CenterForAppliedDims_Grew_HalfDeltaShift()
    {
        var center = ResizeMath.CenterForAppliedDims(
            new Vector3(0f, 0.2f, 0f), Vector3.right, 0.8f,
            new Vector3Int(900, 400, 18), 0);

        Assert.AreEqual(0.05f, center.x, 0.001f);
    }

    [Test]
    public void CenterForAppliedDims_Shrank_HalfDeltaShift()
    {
        var center = ResizeMath.CenterForAppliedDims(
            new Vector3(0f, 0.2f, 0f), Vector3.right, 0.8f,
            new Vector3Int(700, 400, 18), 0);

        Assert.AreEqual(-0.05f, center.x, 0.001f);
    }

    [Test]
    public void CenterForAppliedDims_YAxis()
    {
        var center = ResizeMath.CenterForAppliedDims(
            new Vector3(0f, 0.5f, 0f), Vector3.up, 0.4f,
            new Vector3Int(800, 500, 18), 1);

        Assert.AreEqual(0.55f, center.y, 0.001f);
    }

    [Test]
    public void CenterForAppliedDims_ZAxis()
    {
        var center = ResizeMath.CenterForAppliedDims(
            new Vector3(0f, 0.2f, 0.2f), Vector3.forward, 0.018f,
            new Vector3Int(800, 400, 68), 2);

        Assert.AreEqual(0.225f, center.z, 0.001f);
    }

    [Test]
    public void Ctor_ResizeHandle_FaceIndexProperty()
    {
        var go = new GameObject("H");
        var handle = go.AddComponent<ResizeHandle>();
        handle.faceIndex = 3;

        Assert.AreEqual(3, handle.faceIndex);
        Object.DestroyImmediate(go);
    }

}
