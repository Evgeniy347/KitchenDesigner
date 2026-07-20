using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Прилипание к стенкам паза при РАСТЯГИВАНИИ ручкой. Зеркало
/// GrooveSceneReproTests, но вместо перемещения — ресайз: тянем правую грань
/// дощечки к стойке с пазом.
///
/// Геометрия та же, из реальной сцены: левая кромка дощечки стоит на 1.253 и не
/// двигается, поэтому новый размер = целевая координата правой грани − 1.253.</summary>
public class GrooveResizeSnapTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private static readonly Vector3Int ShelfDims = new Vector3Int(254, 1372, 18);
    private static readonly Vector3 ShelfCenter = new Vector3(1.380f, 1.81f, -2.566f);
    private const float ShelfLeftEdgeX = 1.253f;
    private const float ShelfRightEdgeX = 1.507f;

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

    private KitchenElement MakeFrontSide()
    {
        var el = Make("A34K1_upper_side_L", new Vector3Int(332, 900, 18),
            new Vector3(1.419f, 1.81f, -1.871f), Quaternion.Euler(0f, 180f, 0f));
        el.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Left));
        return el;
    }

    private KitchenElement MakeBackSide()
    {
        var el = Make("A12_upper_A_side (copy)", new Vector3Int(332, 900, 18),
            new Vector3(1.419f, 1.81f, -3.261f), Quaternion.Euler(0f, 0f, 0f));
        el.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));
        return el;
    }

    private KitchenElement MakeWall()
        => Make("A", new Vector3Int(100, 2700, 7240),
            new Vector3(1.635f, 1.35f, 0f), Quaternion.identity);

    private KitchenElement MakeShelf()
        => Make("A34K1_upper_bottom (copy) (copy)", ShelfDims, ShelfCenter,
            Quaternion.Euler(90f, 0f, 0f));

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    /// <summary>Тянем ПРАВУЮ грань дощечки (нормаль +X) на rawDelta и возвращаем
    /// получившуюся ширину в мм.</summary>
    private int ResizeRightFace(float rawDelta, List<KitchenElement> others, KitchenElement shelf)
    {
        // Грань с нормалью +X: её оси в плоскости — локальные Y и Z детали,
        // повёрнутой на 90° вокруг X, то есть мировые +Z и −Y.
        var normal = Vector3.right;
        var uAxis = Vector3.forward;
        var vAxis = Vector3.down;
        var faceCenter0 = new Vector3(ShelfRightEdgeX, ShelfCenter.y, ShelfCenter.z);
        var faceSize = new Vector2(ShelfDims.y * AppConstants.MM_TO_UNITS,
                                   ShelfDims.z * AppConstants.MM_TO_UNITS);
        float sizeStartUnits = ShelfDims.x * AppConstants.MM_TO_UNITS;

        ResizeMath.Compute(ShelfDims, 0, normal, faceCenter0, uAxis, vAxis, faceSize,
            ShelfCenter, sizeStartUnits, rawDelta, others, shelf,
            snapEnabled: true, threshold: 0.05f,
            out Vector3Int newDims, out _, out _);
        return newDims.x;
    }

    /// <summary>Ширина, при которой правая грань встаёт на targetX.</summary>
    private static int WidthFor(float targetX)
        => Mathf.RoundToInt((targetX - ShelfLeftEdgeX) / AppConstants.MM_TO_UNITS);

    [Test]
    public void Resize_SnapsToNearGrooveWall()
    {
        var others = new List<KitchenElement> { MakeFrontSide(), MakeBackSide(), MakeWall() };
        var shelf = MakeShelf();

        // Не дотянули 1.5 мм до ближней стенки паза (1.569).
        int width = ResizeRightFace(0.0605f, others, shelf);

        Assert.AreEqual(WidthFor(1.569f), width,
            "правая грань должна встать на ближнюю стенку паза (16 мм от кромки стойки)");
    }

    [Test]
    public void Resize_SnapsToFarGrooveWall()
    {
        var others = new List<KitchenElement> { MakeFrontSide(), MakeBackSide(), MakeWall() };
        var shelf = MakeShelf();

        // Не дотянули 1.5 мм до дальней стенки паза (1.565).
        int width = ResizeRightFace(0.0565f, others, shelf);

        Assert.AreEqual(WidthFor(1.565f), width,
            "правая грань должна встать на дальнюю стенку паза (20 мм от кромки)");
    }

    [Test]
    public void Resize_StillSnapsToBoardEdgeAndWall()
    {
        // Регрессия: прежний детент по кромке стойки/стене (1.585) обязан работать.
        var others = new List<KitchenElement> { MakeFrontSide(), MakeBackSide(), MakeWall() };
        var shelf = MakeShelf();

        int width = ResizeRightFace(0.0765f, others, shelf);

        Assert.AreEqual(WidthFor(1.585f), width, "правая грань заподлицо с кромкой стойки и стеной");
    }

    [Test]
    public void Resize_WithoutGrooves_IgnoresGrooveDetents()
    {
        // Те же стойки, но без пазов — детентов на 1.565/1.569 быть не должно.
        var front = Make("front", new Vector3Int(332, 900, 18),
            new Vector3(1.419f, 1.81f, -1.871f), Quaternion.Euler(0f, 180f, 0f));
        var back = Make("back", new Vector3Int(332, 900, 18),
            new Vector3(1.419f, 1.81f, -3.261f), Quaternion.identity);
        var others = new List<KitchenElement> { front, back, MakeWall() };
        var shelf = MakeShelf();

        int width = ResizeRightFace(0.0605f, others, shelf);

        Assert.AreNotEqual(WidthFor(1.569f), width, "без паза стенок паза не существует");
    }

    [Test]
    public void Resize_FarFromGroove_DoesNotSnap()
    {
        var others = new List<KitchenElement> { MakeFrontSide(), MakeBackSide(), MakeWall() };
        var shelf = MakeShelf();

        // Тянем всего на 5 мм — до ближайшего детента (1.565) ещё 53 мм, порог 50.
        int width = ResizeRightFace(0.005f, others, shelf);

        Assert.AreEqual(ShelfDims.x + 5, width, "вне порога размер меняется как есть");
    }
}
