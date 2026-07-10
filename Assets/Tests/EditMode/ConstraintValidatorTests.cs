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
        // Пол: верхняя грань на y=0. Доска 400мм высотой стоит на полу → центр y=0.2.
        var floor = CreateElement("Floor", new Vector3Int(3000, 18, 3000), new Vector3(0, -0.009f, 0));
        var board = CreateElement("Board", new Vector3Int(800, 400, 18), new Vector3(0, 0.2f, 0));

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
        // A стоит на полу (валидна). B касается A только ребром (грани совпадают лишь
        // по линии y=0.4), поэтому face-to-face контакта нет и B — нарушение.
        var floor = CreateElement("Floor", new Vector3Int(3000, 18, 3000), new Vector3(0, -0.009f, 0));
        var a = CreateElement("A", new Vector3Int(800, 400, 18), new Vector3(0, 0.2f, 0));
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0.6f, 0));

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
        // Якорь связности — BasePlate. Две пары досок висят в воздухе, не касаясь пола:
        // каждая пара связна внутри себя, но изолирована от плиты → 2 группы, 4 нарушения.
        var plate = CreateElement("BasePlate", new Vector3Int(3000, 18, 3000), new Vector3(0, -0.009f, 0));
        plate.gameObject.AddComponent<BasePlate>();

        var a = CreateElement("A", new Vector3Int(800, 400, 18), new Vector3(0, 1.0f, 0));
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 1.0f, 0));
        var c = CreateElement("C", new Vector3Int(800, 400, 18), new Vector3(0, 3.0f, 0));
        var d = CreateElement("D", new Vector3Int(800, 400, 18), new Vector3(0.8f, 3.0f, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { plate, a, b, c, d });

        Assert.IsFalse(result.isValid);
        Assert.AreEqual(2, result.isolatedGroups.Count);
        Assert.AreEqual(4, result.violations.Count);

        Object.DestroyImmediate(plate.gameObject);
        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
        Object.DestroyImmediate(c.gameObject);
        Object.DestroyImmediate(d.gameObject);
    }

    [Test]
    public void Validate_PartialOverlapBelow50Percent_NotFaceToFace()
    {
        // B лежит на A, но грани перекрываются лишь на 25% → контакт есть, но не
        // face-to-face, поэтому связности нет и обе доски — нарушения.
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.6f, 0.4f, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a, b });

        Assert.AreEqual(1, result.contacts.Count);
        Assert.IsFalse(result.contacts[0].isFaceToFace);
        Assert.IsFalse(result.isValid);
        Assert.AreEqual(2, result.violations.Count);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void Validate_IntersectingBoards_NoContact()
    {
        // Пересекающиеся доски не образуют контакта (пара пропускается).
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.1f, 0, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a, b });

        Assert.AreEqual(0, result.contacts.Count);
        Assert.IsFalse(result.isValid);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void Validate_EmptyList_IsValid()
    {
        var result = ConstraintValidator.Validate(new List<KitchenElement>());
        Assert.IsTrue(result.isValid);
    }
}
