using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Bulk;

public class ModuleResizeTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    private KitchenElement Make(string name, Vector3 pos, Vector3Int dims)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        _spawned.Add(go);
        return e;
    }

    private Dictionary<string, ModuleResize.Change> ByName(List<ModuleResize.Change> changes)
    {
        var map = new Dictionary<string, ModuleResize.Change>();
        foreach (var c in changes) map[c.element.PartName] = c;
        return map;
    }

    [Test]
    public void WidenAlongX_MovesFarSide_GrowsSpan_KeepsNearSide()
    {
        // Шкаф ~600мм: боковины 18мм + пролётное дно 564мм. Ширим на +100 по X.
        var sideL = Make("side_L", new Vector3(0f, 0.36f, 0f), new Vector3Int(18, 720, 540));
        var sideR = Make("side_R", new Vector3(0.582f, 0.36f, 0f), new Vector3Int(18, 720, 540));
        var bottom = Make("bottom", new Vector3(0.291f, 0.05f, 0f), new Vector3Int(564, 32, 540));

        var changes = ModuleResize.Plan(new List<KitchenElement> { sideL, sideR, bottom }, 'x', 100f);
        var map = ByName(changes);

        // Ближняя боковина не меняется.
        Assert.IsFalse(map.ContainsKey("side_L"), "ближняя боковина остаётся на месте");

        // Дальняя боковина сдвинулась на +100мм (0.1м), размер тот же.
        Assert.IsTrue(map.ContainsKey("side_R"));
        Assert.AreEqual(0.682f, map["side_R"].newPosition.x, 0.001f);
        Assert.AreEqual(new Vector3Int(18, 720, 540), map["side_R"].newDimensions);

        // Дно растянулось на +100мм, ближний край на месте → центр сместился на +50мм.
        Assert.IsTrue(map.ContainsKey("bottom"));
        Assert.AreEqual(664, map["bottom"].newDimensions.x);
        Assert.AreEqual(0.341f, map["bottom"].newPosition.x, 0.001f);
    }

    [Test]
    public void ShrinkAlongX_NegativeDelta()
    {
        var sideL = Make("side_L", new Vector3(0f, 0.36f, 0f), new Vector3Int(18, 720, 540));
        var sideR = Make("side_R", new Vector3(0.582f, 0.36f, 0f), new Vector3Int(18, 720, 540));
        var bottom = Make("bottom", new Vector3(0.291f, 0.05f, 0f), new Vector3Int(564, 32, 540));

        var map = ByName(ModuleResize.Plan(new List<KitchenElement> { sideL, sideR, bottom }, 'x', -100f));

        Assert.AreEqual(0.482f, map["side_R"].newPosition.x, 0.001f);
        Assert.AreEqual(464, map["bottom"].newDimensions.x);
        Assert.AreEqual(0.241f, map["bottom"].newPosition.x, 0.001f);
    }
}
