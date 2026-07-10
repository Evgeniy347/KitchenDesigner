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
        Assert.AreEqual(24, BoxWireframe.EdgeIndices.Length, "12 рёбер * 2 индекса");
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
    public void CreateLinesMesh_HasLinesTopology()
    {
        var mesh = BoxWireframe.CreateLinesMesh();
        try
        {
            Assert.AreEqual(8, mesh.vertexCount);
            Assert.AreEqual(MeshTopology.Lines, mesh.GetTopology(0));
            Assert.AreEqual(24, mesh.GetIndices(0).Length);
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }
}
