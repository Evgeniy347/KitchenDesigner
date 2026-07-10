using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class SpecificationManagerTests
{
    private static KitchenElement CreateElement(string name, Vector3Int dims)
    {
        var go = new GameObject(name);
        var element = go.AddComponent<KitchenElement>();
        element.BoardName = name;
        element.DimensionsMM = dims;
        return element;
    }

    [Test]
    public void SurfaceArea_800x400x18_Is0_6832()
    {
        float area = SpecificationManager.SurfaceAreaM2(new Vector3Int(800, 400, 18));
        Assert.AreEqual(0.6832f, area, 0.0001f);
    }

    [Test]
    public void Build_TwoIdenticalOneDifferent_TwoGroups()
    {
        var a = CreateElement("Board", new Vector3Int(800, 400, 18));
        var b = CreateElement("Board", new Vector3Int(800, 400, 18));
        var c = CreateElement("Board", new Vector3Int(600, 400, 18));

        var result = SpecificationManager.Build(new List<KitchenElement> { a, b, c });

        Assert.AreEqual(2, result.lines.Count);
        Assert.AreEqual(3, result.totalCount);
        Assert.AreEqual(2, result.lines[0].count);
        Assert.AreEqual(1, result.lines[1].count);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
        Object.DestroyImmediate(c.gameObject);
    }

    [Test]
    public void Build_EmptyList_ZeroBoardsZeroArea()
    {
        var result = SpecificationManager.Build(new List<KitchenElement>());
        Assert.AreEqual(0, result.lines.Count);
        Assert.AreEqual(0, result.totalCount);
        Assert.AreEqual(0f, result.totalAreaM2, 0.0001f);
    }

    [Test]
    public void Build_TotalArea_SumsGroups()
    {
        var a = CreateElement("Board", new Vector3Int(800, 400, 18));
        var b = CreateElement("Board", new Vector3Int(800, 400, 18));

        var result = SpecificationManager.Build(new List<KitchenElement> { a, b });

        Assert.AreEqual(2, result.totalCount);
        Assert.AreEqual(0.6832f * 2f, result.totalAreaM2, 0.0001f);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void Build_ExcludesBasePlate()
    {
        var plate = CreateElement("BasePlate", new Vector3Int(3000, 18, 3000));
        plate.gameObject.AddComponent<BasePlate>();
        var board = CreateElement("Board", new Vector3Int(800, 400, 18));

        var result = SpecificationManager.Build(new List<KitchenElement> { plate, board });

        Assert.AreEqual(1, result.lines.Count);
        Assert.AreEqual(1, result.totalCount);
        Assert.AreEqual("Board", result.lines[0].name);

        Object.DestroyImmediate(plate.gameObject);
        Object.DestroyImmediate(board.gameObject);
    }
}
