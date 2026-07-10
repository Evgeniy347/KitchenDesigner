using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class BoxWireframeTests
{
    [Test]
    public void Corners_AreEightUnitCubeCorners()
    {
        Assert.AreEqual(8, BoxWireframe.Corners.Length);
        foreach (var c in BoxWireframe.Corners)
        {
            Assert.AreEqual(0.5f, Mathf.Abs(c.x), 1e-6f, "угол по X = ±0.5");
            Assert.AreEqual(0.5f, Mathf.Abs(c.y), 1e-6f, "угол по Y = ±0.5");
            Assert.AreEqual(0.5f, Mathf.Abs(c.z), 1e-6f, "угол по Z = ±0.5");
        }
    }

    [Test]
    public void EdgeIndices_Describe12Edges_InRange()
    {
        Assert.AreEqual(BoxWireframe.EdgeCount * 2, BoxWireframe.EdgeIndices.Length, "12 рёбер * 2 индекса");
        foreach (var i in BoxWireframe.EdgeIndices)
            Assert.IsTrue(i >= 0 && i < 8, "индекс ссылается на существующий угол");
    }

    [Test]
    public void EdgeLengths_AreAllUnit()
    {
        var c = BoxWireframe.Corners;
        var e = BoxWireframe.EdgeIndices;
        for (int k = 0; k < e.Length; k += 2)
        {
            float len = Vector3.Distance(c[e[k]], c[e[k + 1]]);
            Assert.AreEqual(1f, len, 1e-6f, "ребро единичного куба = 1");
        }
    }

    [Test]
    public void WorldCorners_Identity_MatchLocal()
    {
        var into = new Vector3[8];
        BoxWireframe.WorldCorners(Matrix4x4.identity, into);
        for (int i = 0; i < 8; i++)
            Assert.Less(Vector3.Distance(into[i], BoxWireframe.Corners[i]), 1e-6f);
    }

    [Test]
    public void WorldCorners_ScaledAndTranslated_MapsBox()
    {
        // Доска 600×360×18 мм, смещённая и повёрнутая на 90° по Y.
        var pos = new Vector3(1f, 0.5f, -2f);
        var rot = Quaternion.Euler(0f, 90f, 0f);
        var scale = new Vector3(0.6f, 0.36f, 0.018f);
        var m = Matrix4x4.TRS(pos, rot, scale);

        var into = new Vector3[8];
        BoxWireframe.WorldCorners(m, into);

        // Габарит по мировым углам совпадает с ожидаемым после поворота (оси X↔Z).
        var min = into[0]; var max = into[0];
        foreach (var p in into) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        Assert.AreEqual(0.018f, max.x - min.x, 1e-4f, "после поворота ширина короба по X = толщина");
        Assert.AreEqual(0.36f, max.y - min.y, 1e-4f, "высота по Y сохраняется");
        Assert.AreEqual(0.6f, max.z - min.z, 1e-4f, "после поворота глубина по Z = ширина");
        // Центр короба = позиция детали.
        Assert.Less(Vector3.Distance((min + max) * 0.5f, pos), 1e-4f);
    }
}
