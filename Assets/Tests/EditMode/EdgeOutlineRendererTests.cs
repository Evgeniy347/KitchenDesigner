using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class EdgeOutlineRendererTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private GameObject Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go;
    }

    [Test]
    public void AabbEdges_AreTheTwelveEdgesOfABox_EachCornerMeetingThree()
    {
        var edges = EdgeOutlineRenderer.AabbEdgeVertexPairs;

        Assert.AreEqual(12, edges.GetLength(0), "у прямоугольного бокса ровно 12 рёбер");

        var seen = new HashSet<(int, int)>();
        var degree = new int[8];
        for (int i = 0; i < edges.GetLength(0); i++)
        {
            int a = edges[i, 0], b = edges[i, 1];
            Assert.AreNotEqual(a, b, "ребро не может начинаться и кончаться в одной вершине");
            Assert.IsTrue(a >= 0 && a < 8 && b >= 0 && b < 8,
                "индексы должны попадать в 8 вершин KitchenElement.GetVertices()");
            Assert.IsTrue(seen.Add((Mathf.Min(a, b), Mathf.Max(a, b))),
                "ребро продублировано: контур рисовался бы дважды по одной линии");
            degree[a]++;
            degree[b]++;
        }

        for (int v = 0; v < 8; v++)
            Assert.AreEqual(3, degree[v],
                "в каждом углу бокса сходятся ровно три ребра; вершина " + v + " выпала из контура");
    }
}
