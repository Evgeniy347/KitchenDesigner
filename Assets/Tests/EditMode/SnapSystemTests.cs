using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class SnapSystemTests
{
    private KitchenElement _elementA;
    private KitchenElement _elementB;

    [SetUp]
    public void Setup()
    {
        KitchenSettings.Instance.GridStep = 16;
        KitchenSettings.Instance.GridEnabled = true;
        KitchenSettings.Instance.SnapEnabled = true;
        KitchenSettings.Instance.SnapThreshold = 50f;

        _elementA = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        _elementB = CreateElement("B", new Vector3Int(800, 400, 18), Vector3.zero);
    }

    [TearDown]
    public void Teardown()
    {
        if (_elementA != null) Object.DestroyImmediate(_elementA.gameObject);
        if (_elementB != null) Object.DestroyImmediate(_elementB.gameObject);
    }

    [Test]
    public void TrySnap_30mmGap_Snapped()
    {
        _elementA.transform.position = Vector3.zero;
        _elementB.transform.position = new Vector3(0.83f, 0, 0);

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { _elementA }, _elementB.transform.position);

        Assert.IsTrue(result.snapped, "Should snap when gap is 30mm < threshold 50mm");
        Assert.AreEqual(0.800f, result.position.x, 0.001f);
    }

    [Test]
    public void TrySnap_60mmGap_NotSnapped()
    {
        _elementA.transform.position = Vector3.zero;
        _elementB.transform.position = new Vector3(0.86f, 0, 0);

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { _elementA }, _elementB.transform.position);

        Assert.IsFalse(result.snapped, "Should NOT snap when gap is 60mm > threshold 50mm");
    }

    [Test]
    public void TrySnap_ElementAboveFloor_SnapsToFloor()
    {
        var floor = CreateElement("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        _elementB.transform.position = new Vector3(0, 0.25f, 0);

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { floor }, _elementB.transform.position);

        Assert.IsTrue(result.snapped, "Element above floor should snap down to it");
        Assert.AreEqual(0.208f, result.position.y, 0.001f);

        Object.DestroyImmediate(floor.gameObject);
    }

    [Test]
    public void TrySnap_IntersectingElements_NotSnapped()
    {
        _elementA.transform.position = Vector3.zero;
        _elementB.transform.position = Vector3.zero;

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { _elementA }, _elementB.transform.position);

        Assert.IsFalse(result.snapped, "Should NOT snap when elements intersect");
    }

    [Test]
    public void TrySnap_SnapDisabled_NotSnapped()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        _elementA.transform.position = Vector3.zero;
        _elementB.transform.position = new Vector3(0.83f, 0, 0);

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { _elementA }, _elementB.transform.position);

        Assert.IsFalse(result.snapped, "Should NOT snap when snap is disabled");
    }

    private static KitchenElement CreateElement(string name, Vector3Int dims, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var element = go.AddComponent<KitchenElement>();
        element.BoardName = name;
        element.DimensionsMM = dims;
        return element;
    }
}
