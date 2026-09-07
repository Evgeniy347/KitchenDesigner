using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Воспроизведение РЕАЛЬНОЙ сцены, где прилипание к пазу не сработало.
/// Дощечка зажата между двумя стойками: её пласти уже лежат заподлицо на обеих
/// (два «нулевых» контакта по ±Z), и в этом виде она двигается по X.
///
/// Координаты сняты из приложения как есть.</summary>
public class GrooveSceneReproTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = dims;
        el.transform.position = pos;
        el.transform.rotation = rot;
        return el;
    }

    /// <summary>Стойка спереди: поворот Y 180°, паз "left" = мировая правая кромка.</summary>
    private KitchenElement MakeFrontSide()
    {
        var el = Make("A34K1_upper_side_L", new Vector3Int(332, 900, 18),
            new Vector3(1.419f, 1.81f, -1.871f), ManagedRotation.Euler(0f, 180f, 0f));
        el.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Left));
        return el;
    }

    /// <summary>Стойка сзади: поворот 0, паз "right" = та же мировая правая кромка.</summary>
    private KitchenElement MakeBackSide()
    {
        var el = Make("A12_upper_A_side (copy)", new Vector3Int(332, 900, 18),
            new Vector3(1.419f, 1.81f, -3.261f), ManagedRotation.Euler(0f, 0f, 0f));
        el.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));
        return el;
    }

    private KitchenElement MakeShelf()
        => Make("A34K1_upper_bottom (copy) (copy)", new Vector3Int(254, 1372, 18),
            new Vector3(1.380f, 1.81f, -2.566f), ManagedRotation.Euler(90f, 0f, 0f));

    /// <summary>Стена, к которой прижат весь ряд: её внутренняя грань лежит в той
    /// же плоскости X=1.585, что и правая кромка стоек. Именно она перетягивала
    /// дощечку с детента паза на себя.</summary>
    private KitchenElement MakeWall()
        => Make("A", new Vector3Int(100, 2700, 7240),
            new Vector3(1.635f, 1.35f, 0f), Quaternion.identity);

    [SetUp]
    public void SetUp() => KitchenSettings.Instance.SnapEnabled = true;

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    [Test]
    public void BothSides_PutGrooveWallsAtSameWorldX()
    {
        var front = MakeFrontSide();
        var back = MakeBackSide();

        foreach (var (name, el) in new[] { ("передняя", front), ("задняя", back) })
        {
            var xs = new List<float>();
            foreach (var w in el.GetGrooveWallFaces()) xs.Add(w.center.x);
            xs.Sort();
            Assert.AreEqual(2, xs.Count, $"{name}: две стенки");
            Assert.AreEqual(1.565f, xs[0], 1e-3f, $"{name}: дальняя стенка");
            Assert.AreEqual(1.569f, xs[1], 1e-3f, $"{name}: ближняя стенка");
        }
    }

    [Test]
    public void ShelfBetweenTwoSides_SnapsToNearGrooveWall()
    {
        var front = MakeFrontSide();
        var back = MakeBackSide();
        var shelf = MakeShelf();
        var others = new List<KitchenElement> { front, back };

        var result = SnapSystem.TrySnap(shelf, others, new Vector3(1.4405f, 1.81f, -2.566f));

        Assert.IsTrue(result.snapped, "зажатая между стойками дощечка должна ловить паз");
        Assert.AreEqual(1.442f, result.position.x, 1e-3f);
    }

    [Test]
    public void ShelfBetweenTwoSides_SnapsToFarGrooveWall()
    {
        var front = MakeFrontSide();
        var back = MakeBackSide();
        var shelf = MakeShelf();
        var others = new List<KitchenElement> { front, back };

        var result = SnapSystem.TrySnap(shelf, others, new Vector3(1.4365f, 1.81f, -2.566f));

        Assert.IsTrue(result.snapped);
        Assert.AreEqual(1.438f, result.position.x, 1e-3f);
    }

    [Test]
    public void ShelfBetweenTwoSides_StillSnapsToBoardEdge()
    {
        var front = MakeFrontSide();
        var back = MakeBackSide();
        var shelf = MakeShelf();
        var others = new List<KitchenElement> { front, back };

        var result = SnapSystem.TrySnap(shelf, others, new Vector3(1.4565f, 1.81f, -2.566f));

        Assert.IsTrue(result.snapped);
        Assert.AreEqual(1.458f, result.position.x, 1e-3f);
    }

    /// <summary>РЕГРЕССИЯ. Стена стоит внутренней гранью ровно в плоскости правой
    /// кромки стоек (X=1.585) и на 17.5 мм от дощечки. Проход «добора» применял её
    /// заподлицо ПО ТОЙ ЖЕ оси X, по которой деталь уже выровняли по стенке паза,
    /// и утаскивал с 1.442 на 1.458. Изолированный тест этого не ловил — нужна
    /// именно стена рядом.</summary>
    [Test]
    public void WallInSamePlane_DoesNotDragShelfOffGrooveDetent()
    {
        var front = MakeFrontSide();
        var back = MakeBackSide();
        var wall = MakeWall();
        var shelf = MakeShelf();
        var others = new List<KitchenElement> { front, back, wall };

        var result = SnapSystem.TrySnap(shelf, others, new Vector3(1.4405f, 1.81f, -2.566f));

        Assert.IsTrue(result.snapped);
        Assert.AreEqual(1.442f, result.position.x, 1e-3f,
            "стена не должна перебивать уже сделанное выравнивание по стенке паза");
    }

    [Test]
    public void WallInSamePlane_StillAllowsSnappingToWallItself()
    {
        // Обратная проверка: когда дощечка идёт к самой стене, прилипание к ней
        // обязано работать по-прежнему.
        var front = MakeFrontSide();
        var back = MakeBackSide();
        var wall = MakeWall();
        var shelf = MakeShelf();
        var others = new List<KitchenElement> { front, back, wall };

        var result = SnapSystem.TrySnap(shelf, others, new Vector3(1.4565f, 1.81f, -2.566f));

        Assert.IsTrue(result.snapped);
        Assert.AreEqual(1.458f, result.position.x, 1e-3f, "кромка дощечки заподлицо со стеной");
    }

    /// <summary>Драг квантует позицию по сетке (1 мм) ПЕРЕД снэпом — детент
    /// должен ловиться и из целых миллиметров, а не только из «удобных» проб.</summary>
    [Test]
    public void SnapsFromGridQuantisedPositions()
    {
        var front = MakeFrontSide();
        var back = MakeBackSide();
        var shelf = MakeShelf();
        var others = new List<KitchenElement> { front, back };

        foreach (var (probe, expected) in new[]
        {
            (1.440f, 1.442f), (1.441f, 1.442f), (1.443f, 1.442f),
            (1.437f, 1.438f), (1.439f, 1.438f),
        })
        {
            var r = SnapSystem.TrySnap(shelf, others, new Vector3(probe, 1.81f, -2.566f));
            Assert.IsTrue(r.snapped, $"проба {probe} должна прилипнуть");
            Assert.AreEqual(expected, r.position.x, 1e-3f, $"из {probe} ожидали {expected}");
        }
    }
}
