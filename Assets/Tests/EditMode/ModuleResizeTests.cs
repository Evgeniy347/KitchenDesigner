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

    [Test]
    public void FractionalDelta_KeepsTheStretchAndTheShiftInAgreement()
    {
        var sideL = Make("side_L", new Vector3(0f, 0.36f, 0f), new Vector3Int(18, 720, 540));
        var sideR = Make("side_R", new Vector3(0.582f, 0.36f, 0f), new Vector3Int(18, 720, 540));
        var bottom = Make("bottom", new Vector3(0.291f, 0.05f, 0f), new Vector3Int(564, 32, 540));

        var map = ByName(ModuleResize.Plan(new List<KitchenElement> { sideL, sideR, bottom }, 'x', 101.4f));

        int grewMM = map["bottom"].newDimensions.x - 564;
        Assert.AreEqual(101, grewMM, "дельта округляется ОДИН раз, до целых миллиметров");

        Assert.AreEqual(grewMM * AppConstants.MM_TO_UNITS,
            map["side_R"].newPosition.x - 0.582f, 1e-5f,
            "дальняя боковина обязана уехать ровно на столько, на сколько выросло дно; "
            + "пока размер брал округлённую дельту, а позиция — исходную дробную, грань "
            + "модуля уезжала с миллиметровой сетки");

        Assert.AreEqual(grewMM * AppConstants.MM_TO_UNITS * 0.5f,
            map["bottom"].newPosition.x - 0.291f, 1e-5f,
            "у пролётной доски ближний край стоит, поэтому центр едет ровно на половину роста");
    }

    [Test]
    public void RotatedBoard_GrowsAlongTheLocalAxisThatFacesTheWorldAxis()
    {
        var go = new GameObject("rotated");
        go.transform.position = new Vector3(0.3f, 0.36f, 0f);
        go.transform.rotation = ManagedRotation.Euler(0f, 90f, 0f);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = "rotated";
        e.DimensionsMM = new Vector3Int(600, 720, 18);
        _spawned.Add(go);

        var map = ByName(ModuleResize.Plan(new List<KitchenElement> { e }, 'x', 100f));

        Assert.AreEqual(new Vector3Int(600, 720, 118), map["rotated"].newDimensions,
            "после поворота на 90° мировая ось X смотрит вдоль ЛОКАЛЬНОЙ глубины: "
            + "растёт dimZ, а не dimX — иначе повёрнутый модуль расширялся бы поперёк себя");
    }

    [Test]
    public void SpanFraction_SeparatesBoardsThatAreStretchedFromBoardsThatAreShifted()
    {
        var big = Make("big", new Vector3(0.3f, 0.05f, 0f), new Vector3Int(600, 32, 540));
        var wide = Make("wide", new Vector3(0.41f, 0.30f, 0f), new Vector3Int(380, 32, 540));
        var narrow = Make("narrow", new Vector3(0.43f, 0.60f, 0f), new Vector3Int(340, 32, 540));

        Assert.AreEqual(360f, ModuleResize.DefaultSpanFraction * 600f, 1e-3f,
            "предусловие: порог пролётности для модуля 600 мм — 360 мм, 380 выше, 340 ниже");

        var map = ByName(ModuleResize.Plan(new List<KitchenElement> { big, wide, narrow }, 'x', 100f));

        Assert.AreEqual(700, map["big"].newDimensions.x, "пролётная доска растягивается");
        Assert.AreEqual(480, map["wide"].newDimensions.x,
            "доска длиннее порога — тоже пролётная, её растягивают, а не двигают");
        Assert.AreEqual(new Vector3Int(340, 32, 540), map["narrow"].newDimensions,
            "короткая доска дальней половины едет целиком");
        Assert.AreEqual(0.53f, map["narrow"].newPosition.x, 0.001f);
    }
}
