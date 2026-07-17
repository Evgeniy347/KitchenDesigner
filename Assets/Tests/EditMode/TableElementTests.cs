using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class TableElementTests
{
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

    private static Vector3 AabbCenter(KitchenElement e)
    {
        var v = e.GetVertices();
        Vector3 min = v[0], max = v[0];
        for (int i = 1; i < v.Length; i++)
        {
            min = Vector3.Min(min, v[i]);
            max = Vector3.Max(max, v[i]);
        }
        return (min + max) * 0.5f;
    }

    [Test]
    public void Table_BoundingBox_MatchesDimensions()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateTable(dims, "TestTable", Vector3.zero);
        var table = go.GetComponent<TableElement>();
        Assert.IsNotNull(table);

        var extent = AabbExtent(table);
        Assert.AreEqual(2.0f, extent.x, 0.001f, "ширина 2000мм = 2м");
        Assert.AreEqual(0.75f, extent.y, 0.001f, "высота 750мм = 0.75м");
        Assert.AreEqual(1.0f, extent.z, 0.001f, "глубина 1000мм = 1м");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_BoundingBox_MatchesDimensions()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateRadiusTable(dims, "TestRadiusTable", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        var extent = AabbExtent(table);
        Assert.AreEqual(2.0f, extent.x, 0.001f, "ширина 2000мм = 2м");
        Assert.AreEqual(0.75f, extent.y, 0.001f, "высота 750мм = 0.75м");
        Assert.AreEqual(1.0f, extent.z, 0.001f, "глубина 1000мм = 1м");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_FloorSnap_CenterAboveFloor()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        float posY = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreateRadiusTable(dims, "TestRadiusTable", new Vector3(0, posY, 0));
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        var center = AabbCenter(table);
        Assert.AreEqual(posY, center.y, 0.001f, "центр bounding box на posY (пол)");

        var extent = AabbExtent(table);
        var min = center - extent * 0.5f;
        Assert.AreEqual(0f, min.y, 0.001f, "низ bounding box на y=0 (пол)");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Table_FloorSnap_CenterAboveFloor()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        float posY = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreateTable(dims, "TestTable", new Vector3(0, posY, 0));
        var table = go.GetComponent<TableElement>();
        Assert.IsNotNull(table);

        var center = AabbCenter(table);
        Assert.AreEqual(posY, center.y, 0.001f, "центр bounding box на posY (пол)");

        var extent = AabbExtent(table);
        var min = center - extent * 0.5f;
        Assert.AreEqual(0f, min.y, 0.001f, "низ bounding box на y=0 (пол)");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_GetFaces_ReturnsSixFaces()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateRadiusTable(dims, "TestRadiusTable", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        var faces = table.GetFaces();
        Assert.AreEqual(6, faces.Length, "должно быть 6 граней");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_LegInset_Default100()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateRadiusTable(dims, "TestRadiusTable", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);
        Assert.AreEqual(100, table.LegInsetMM);

        Object.DestroyImmediate(go);
    }
}
