using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ConstraintValidatorTests
{
    private KitchenElement CreateElement(string name, Vector3Int dims, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var element = go.AddComponent<KitchenElement>();
        element.BoardName = name;
        element.DimensionsMM = dims;
        return element;
    }

    [Test]
    public void Validate_TwoBoardsFaceToFace_IsValid()
    {
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a, b });

        Assert.IsTrue(result.isValid);
        Assert.AreEqual(1, result.contacts.Count);
        Assert.IsTrue(result.contacts[0].isFaceToFace);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void Validate_SingleBoardInAir_Violation()
    {
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a });

        Assert.IsFalse(result.isValid);
        Assert.AreEqual(1, result.violations.Count);

        Object.DestroyImmediate(a.gameObject);
    }

    [Test]
    public void Validate_BoardOnFloor_IsValid()
    {
        var floor = CreateElement("Floor", new Vector3Int(3000, 18, 3000), new Vector3(0, -0.009f, 0));
        var board = CreateElement("Board", new Vector3Int(800, 400, 18), new Vector3(0, 0.009f, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { floor, board });

        Assert.IsTrue(result.isValid);
        Assert.AreEqual(1, result.contacts.Count);
        Assert.IsTrue(result.contacts[0].isFaceToFace);

        Object.DestroyImmediate(floor.gameObject);
        Object.DestroyImmediate(board.gameObject);
    }

    [Test]
    public void Validate_BoardTouchingByEdge_Violation()
    {
        var floor = CreateElement("Floor", new Vector3Int(3000, 18, 3000), new Vector3(0, -0.009f, 0));
        var a = CreateElement("A", new Vector3Int(800, 400, 18), new Vector3(0, 0.009f, 0));
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0.25f, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { floor, a, b });

        Assert.IsFalse(result.isValid);
        Assert.AreEqual(1, result.violations.Count);
        Assert.AreEqual(b, result.violations[0]);

        Object.DestroyImmediate(floor.gameObject);
        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void Validate_ChainABC_IsValid()
    {
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0, 0));
        var c = CreateElement("C", new Vector3Int(800, 400, 18), new Vector3(1.6f, 0, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a, b, c });

        Assert.IsTrue(result.isValid);
        Assert.AreEqual(2, result.contacts.Count);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
        Object.DestroyImmediate(c.gameObject);
    }

    [Test]
    public void Validate_TwoIsolatedGroups_Violation()
    {
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0, 0));
        var c = CreateElement("C", new Vector3Int(800, 400, 18), new Vector3(2.0f, 0, 0));
        var d = CreateElement("D", new Vector3Int(800, 400, 18), new Vector3(2.8f, 0, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a, b, c, d });

        Assert.IsFalse(result.isValid);
        Assert.AreEqual(2, result.isolatedGroups.Count);
        Assert.AreEqual(2, result.violations.Count);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
        Object.DestroyImmediate(c.gameObject);
        Object.DestroyImmediate(d.gameObject);
    }

    [Test]
    public void Validate_EmptyList_IsValid()
    {
        var result = ConstraintValidator.Validate(new List<KitchenElement>());
        Assert.IsTrue(result.isValid);
    }
}
