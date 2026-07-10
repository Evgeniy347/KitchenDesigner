using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class KitchenElementTests
{
    private static KitchenElement CreateElement(Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject("E");
        go.transform.position = pos;
        var element = go.AddComponent<KitchenElement>();
        element.DimensionsMM = dims;
        return element;
    }

    private static Vector3 AabbExtent(KitchenElement e)
    {
        var v = e.GetVertices();
        Vector3 min = v[0], max = v[0];
        for (int i = 1; i < v.Length; i++)
        {
            min = Vector3.Min(min, v[i]);
            max = Vector3.Max(max, v[i]);
        }
        return max - min;
    }

    [Test]
    public void RotateAroundY90_SwapsXZExtents()
    {
        var e = CreateElement(new Vector3Int(800, 400, 18), Vector3.zero);
        var before = AabbExtent(e);
        Assert.AreEqual(0.8f, before.x, 0.001f);
        Assert.AreEqual(0.018f, before.z, 0.001f);

        e.RotateAroundAxis(Vector3.up, 90f);

        var after = AabbExtent(e);
        Assert.AreEqual(0.018f, after.x, 0.001f);
        Assert.AreEqual(0.8f, after.z, 0.001f);
        Assert.AreEqual(0.4f, after.y, 0.001f, "высота по Y не меняется при повороте вокруг Y");

        Object.DestroyImmediate(e.gameObject);
    }

    [Test]
    public void RotateAroundX90_SwapsYZExtents()
    {
        var e = CreateElement(new Vector3Int(800, 400, 18), Vector3.zero);
        var before = AabbExtent(e);
        Assert.AreEqual(0.4f, before.y, 0.001f);
        Assert.AreEqual(0.018f, before.z, 0.001f);

        e.RotateAroundAxis(Vector3.right, 90f);

        var after = AabbExtent(e);
        Assert.AreEqual(0.8f, after.x, 0.001f, "ширина по X не меняется при повороте вокруг X");
        Assert.AreEqual(0.018f, after.y, 0.001f, "Y становится бывшей толщиной Z");
        Assert.AreEqual(0.4f, after.z, 0.001f, "Z становится бывшей высотой Y");

        Object.DestroyImmediate(e.gameObject);
    }

    [Test]
    public void RotateAroundZ90_SwapsXYExtents()
    {
        var e = CreateElement(new Vector3Int(800, 400, 18), Vector3.zero);

        e.RotateAroundAxis(Vector3.forward, 90f);

        var after = AabbExtent(e);
        Assert.AreEqual(0.4f, after.x, 0.001f, "X становится бывшей высотой Y");
        Assert.AreEqual(0.8f, after.y, 0.001f, "Y становится бывшей шириной X");
        Assert.AreEqual(0.018f, after.z, 0.001f, "толщина по Z не меняется при повороте вокруг Z");

        Object.DestroyImmediate(e.gameObject);
    }

    [Test]
    public void RotateAroundX_RotatesFaceNormals()
    {
        var e = CreateElement(new Vector3Int(800, 400, 18), Vector3.zero);
        e.RotateAroundAxis(Vector3.right, 90f);
        var faces = e.GetFaces();
        // Грань 2 (локальный +Y) после поворота на 90° вокруг X смотрит вдоль +Z.
        Assert.AreEqual(0f, faces[2].normal.y, 0.001f);
        Assert.AreEqual(1f, Mathf.Abs(faces[2].normal.z), 0.001f);
        Object.DestroyImmediate(e.gameObject);
    }

    [Test]
    public void Rotate_PreservesPosition()
    {
        var e = CreateElement(new Vector3Int(800, 400, 18), new Vector3(1, 2, 3));
        e.RotateAroundAxis(Vector3.up, 90f);
        Assert.AreEqual(new Vector3(1, 2, 3), e.transform.position);
        Object.DestroyImmediate(e.gameObject);
    }

    [Test]
    public void Rotate_ComposesWithExistingRotation()
    {
        var e = CreateElement(new Vector3Int(800, 400, 18), Vector3.zero);
        e.RotateAroundAxis(Vector3.up, 45f);
        e.RotateAroundAxis(Vector3.up, 45f);
        // Две по 45° = 90° → нормаль грани 0 (локальный +X) повернётся к -Z.
        var faces = e.GetFaces();
        Assert.AreEqual(0f, faces[0].normal.x, 0.001f);
        Assert.AreEqual(1f, Mathf.Abs(faces[0].normal.z), 0.001f);
        Object.DestroyImmediate(e.gameObject);
    }
}
