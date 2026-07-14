using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class AlignDistributeToolTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(Vector3 pos)
    {
        var go = new GameObject("E");
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = new Vector3Int(200, 200, 18);
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void Align_X_Min_MovesAllToMinX()
    {
        var list = new List<KitchenElement>
        {
            Make(new Vector3(0.0f, 0, 0)),
            Make(new Vector3(0.5f, 0, 0)),
            Make(new Vector3(1.0f, 0, 0)),
        };

        AlignDistributeTool.Align(list, Axis.X, AlignmentMode.Min);

        foreach (var e in list)
            Assert.AreEqual(0f, e.transform.position.x, 0.001f);
    }

    [Test]
    public void Align_Y_Max_MovesAllToMaxY()
    {
        var list = new List<KitchenElement>
        {
            Make(new Vector3(0, 0.2f, 0)),
            Make(new Vector3(0, 0.8f, 0)),
        };

        AlignDistributeTool.Align(list, Axis.Y, AlignmentMode.Max);

        foreach (var e in list)
            Assert.AreEqual(0.8f, e.transform.position.y, 0.001f);
    }

    [Test]
    public void Align_Z_Center_MovesAllToCenterZ()
    {
        var list = new List<KitchenElement>
        {
            Make(new Vector3(0, 0, 0.0f)),
            Make(new Vector3(0, 0, 1.0f)),
        };

        AlignDistributeTool.Align(list, Axis.Z, AlignmentMode.Center);

        foreach (var e in list)
            Assert.AreEqual(0.5f, e.transform.position.z, 0.001f);
    }

    [Test]
    public void Align_LessThanTwo_NoThrow_NoChange()
    {
        var single = new List<KitchenElement> { Make(new Vector3(0.3f, 0, 0)) };
        Assert.DoesNotThrow(() => AlignDistributeTool.Align(single, Axis.X, AlignmentMode.Min));
        Assert.AreEqual(0.3f, single[0].transform.position.x, 0.001f);
        Assert.DoesNotThrow(() => AlignDistributeTool.Align(null!, Axis.X, AlignmentMode.Min));
    }

    [Test]
    public void Distribute_X_EvenlySpacesMiddle()
    {
        var list = new List<KitchenElement>
        {
            Make(new Vector3(0.0f, 0, 0)),
            Make(new Vector3(0.3f, 0, 0)),
            Make(new Vector3(1.0f, 0, 0)),
        };

        AlignDistributeTool.Distribute(list, Axis.X);

        // Крайние не двигаются, средний — ровно по центру (0.5).
        list.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        Assert.AreEqual(0.0f, list[0].transform.position.x, 0.001f);
        Assert.AreEqual(0.5f, list[1].transform.position.x, 0.001f);
        Assert.AreEqual(1.0f, list[2].transform.position.x, 0.001f);
    }

    [Test]
    public void Distribute_LessThanThree_NoChange()
    {
        var list = new List<KitchenElement>
        {
            Make(new Vector3(0.0f, 0, 0)),
            Make(new Vector3(0.4f, 0, 0)),
        };

        Assert.DoesNotThrow(() => AlignDistributeTool.Distribute(list, Axis.X));
        Assert.AreEqual(0.4f, list[1].transform.position.x, 0.001f);
    }
}
