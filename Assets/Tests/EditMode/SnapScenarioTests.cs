using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Обширное покрытие прилипания: кромки / вершины / плоскости, 2 и 3 доски,
/// повёрнутые доски, пол. Точные кейсы проверяют координаты; общий «оракул»
/// AssertSnappedFlush проверяет, что после снэпа доски касаются плоскостью
/// (face-to-face контакт) и не пересекаются — работает для любой геометрии.
/// </summary>
public class SnapScenarioTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _prevVerbose;

    [SetUp]
    public void Setup()
    {
        KitchenSettings.Instance.GridStep = 1;
        KitchenSettings.Instance.GridEnabled = true;
        KitchenSettings.Instance.SnapEnabled = true;
        KitchenSettings.Instance.SnapThreshold = 50f;
        _prevVerbose = SnapSystem.VerboseLog;
        SnapSystem.VerboseLog = false; // не засорять вывод тестов
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        SnapSystem.VerboseLog = _prevVerbose;
    }

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos, Quaternion? rot = null)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = rot ?? Quaternion.identity;
        var e = go.AddComponent<KitchenElement>();
        e.BoardName = name;
        e.DimensionsMM = dims;
        _spawned.Add(go);
        return e;
    }

    private static SnapResult Snap(KitchenElement moved, KitchenElement target, Vector3 testPos)
    {
        return SnapSystem.TrySnap(moved, new List<KitchenElement> { target }, testPos);
    }

    // Оракул: после снэпа доски касаются плоскостью и не пересекаются.
    private void AssertSnappedFlush(KitchenElement moved, KitchenElement target, Vector3 testPos)
    {
        var r = Snap(moved, target, testPos);
        Assert.IsTrue(r.snapped, "ожидалось прилипание");
        moved.transform.position = r.position;

        Assert.IsFalse(SnapSystem.ElementsIntersect(moved, target),
            "после снэпа доски не должны пересекаться");

        var val = ConstraintValidator.Validate(new List<KitchenElement> { moved, target });
        Assert.IsTrue(val.contacts.Exists(c => c.isFaceToFace),
            "после снэпа должен быть face-to-face контакт (касание плоскостью)");
    }

    // --- Кромки/вершины/центр на большой грани (мелкая доска не центрируется) ---

    [Test]
    public void SmallBoard_LeftEdge_AlignsLeft()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(-0.18f, 0f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(-0.20f, r.position.x, 0.001f);
        Assert.AreEqual(0.018f, r.position.z, 0.001f);
    }

    [Test]
    public void SmallBoard_RightEdge_AlignsRight()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.18f, 0f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.20f, r.position.x, 0.001f);
    }

    [Test]
    public void SmallBoard_NearCenter_AlignsCenter()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.03f, 0f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.0f, r.position.x, 0.001f);
    }

    [Test]
    public void SmallBoard_Corner_AlignsBothEdges()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 200, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(-0.18f, 0.08f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(-0.20f, r.position.x, 0.001f, "левая кромка");
        Assert.AreEqual(0.10f, r.position.y, 0.001f, "верхняя кромка");
        Assert.AreEqual(0.018f, r.position.z, 0.001f, "плоскости заподлицо");
    }

    // --- Стыки встык (равные доски) ---

    [Test]
    public void EqualBoards_ButtJointX_Flush()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.82f, 0f, 0f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.80f, r.position.x, 0.001f);
        AssertSnappedFlush(b, a, new Vector3(0.82f, 0f, 0f));
    }

    [Test]
    public void BoardOnTopOfBoard_Flush()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0f, 0.42f, 0f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.40f, r.position.y, 0.001f);
    }

    // --- Пол ---

    [Test]
    public void BoardAboveFloor_SnapsDown()
    {
        var floor = Make("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, floor, new Vector3(0f, 0.25f, 0f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.209f, r.position.y, 0.001f);
    }

    // --- Повёрнутая доска (оракул) ---

    [Test]
    public void RotatedBoard_ButtJoint_FlushContact()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero,
            Quaternion.AngleAxis(90f, Vector3.up));
        AssertSnappedFlush(b, a, new Vector3(0.43f, 0f, 0f));
    }

    // --- Три доски ---

    [Test]
    public void ThreeBoards_ThirdSnapsToNearestNeighbour()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0f, 0f));
        var c = Make("C", new Vector3Int(800, 400, 18), Vector3.zero);

        var r = SnapSystem.TrySnap(c, new List<KitchenElement> { a, b }, new Vector3(1.62f, 0f, 0f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual("B", r.targetName, "должна прилипнуть к ближайшей доске B");
        Assert.AreEqual(1.60f, r.position.x, 0.001f);
    }

    [Test]
    public void ThreeBoards_BoxCorner_AllFlush()
    {
        // Пол + две вертикальные доски, образующие угол; каждая прилипает заподлицо.
        var floor = Make("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        var wallA = Make("WallA", new Vector3Int(800, 400, 18), Vector3.zero);
        var wallB = Make("WallB", new Vector3Int(800, 400, 18), Vector3.zero);

        // Стенка A встаёт на пол.
        AssertSnappedFlush(wallA, floor, new Vector3(0f, 0.25f, 0f));
        // Стенка B встаёт на пол рядом.
        AssertSnappedFlush(wallB, floor, new Vector3(0.6f, 0.25f, 0.3f));
    }

    // --- Негативные случаи ---

    [Test]
    public void NoSnap_TooFar()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(2.0f, 0f, 0f));
        Assert.IsFalse(r.snapped);
    }

    [Test]
    public void NoSnap_WhenDisabled()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.82f, 0f, 0f));
        Assert.IsFalse(r.snapped);
    }

    [Test]
    public void NoSnap_WhenIntersecting()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.1f, 0f, 0f)); // глубоко перекрываются
        Assert.IsFalse(r.snapped);
    }
}
