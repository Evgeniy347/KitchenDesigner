using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ResizeSnapTests
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

    // A: центр (0,0.2,0), +X-грань на x=0.4. B слева/справа варьируем.
    private static (Vector3 c, Vector3 n, Vector3 u, Vector3 v, Vector2 s) PlusXFace(KitchenElement a)
    {
        var f = a.GetFaces()[0]; // +X
        return (f.center, f.normal, f.rightAxis, f.upAxis, f.size);
    }

    [Test]
    public void SnapDelta_OpposingFaceWithinThreshold_ReturnsGap()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        // -X-грань B на x=0.45 → зазор 0.05 от +X-грани A (x=0.4).
        var b = Make(new Vector3(0.85f, 0.2f, 0), new Vector3Int(800, 400, 18));
        var fa = PlusXFace(a);

        bool snapped = ResizeSnap.SnapDelta(fa.c, fa.n, fa.u, fa.v, fa.s,
            new List<KitchenElement> { b }, a, 0.05f, out float gap);

        Assert.IsTrue(snapped);
        Assert.AreEqual(0.05f, gap, 0.0005f);
    }

    [Test]
    public void SnapDelta_BeyondThreshold_ReturnsFalse()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        var b = Make(new Vector3(1.5f, 0.2f, 0), new Vector3Int(800, 400, 18)); // -X на x=1.1
        var fa = PlusXFace(a);

        bool snapped = ResizeSnap.SnapDelta(fa.c, fa.n, fa.u, fa.v, fa.s,
            new List<KitchenElement> { b }, a, 0.05f, out _);

        Assert.IsFalse(snapped);
    }

    [Test]
    public void SnapDelta_NoInPlaneOverlap_ReturnsFalse()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        // По нормали близко, но смещена по Y → грани не перекрываются в плоскости.
        var b = Make(new Vector3(0.85f, 5f, 0), new Vector3Int(800, 400, 18));
        var fa = PlusXFace(a);

        bool snapped = ResizeSnap.SnapDelta(fa.c, fa.n, fa.u, fa.v, fa.s,
            new List<KitchenElement> { b }, a, 0.05f, out _);

        Assert.IsFalse(snapped);
    }

    [Test]
    public void SnapDelta_CoDirectionalFace_NotSnapped()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        // B слева: к +X-грани A обращена со-направленная +X-грань B (dot≈+1) — не стык.
        var b = Make(new Vector3(-0.85f, 0.2f, 0), new Vector3Int(800, 400, 18));
        var fa = PlusXFace(a);

        bool snapped = ResizeSnap.SnapDelta(fa.c, fa.n, fa.u, fa.v, fa.s,
            new List<KitchenElement> { b }, a, 0.05f, out _);

        Assert.IsFalse(snapped);
    }

    [Test]
    public void SnapDelta_NullOthers_ReturnsFalse()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        var fa = PlusXFace(a);
        Assert.IsFalse(ResizeSnap.SnapDelta(fa.c, fa.n, fa.u, fa.v, fa.s, null!, a, 0.05f, out _));
    }
}
