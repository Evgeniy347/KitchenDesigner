#pragma warning disable CS8604 // Possible null reference argument

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Воспроизведение бага: радиальная полка не липнет к вертикальной боковине,
/// когда в той же плоскости X есть ещё горизонтальная крышка (top).
///
/// Геометрия из реальной сцены:
///   B4_upper_side_L       — вертикальная боковина 18×900×332, грань -X на x=-0.773
///   B4_upper_top          — горизонтальная крышка 564×332×18 (rotX=90), грань -X на x=-0.755
///   radial_shelf_upper_3  — радиальная полка 314×18×332 (rotY=270),
///                           грань +X на x=-0.755 (вплотную к боковине и крышке)
///
/// Ошибка: при сдвиге полки по X (чтобы «отлепить» и дать снэпу притянуть обратно)
/// снэп выбирает B4_upper_top вместо B4_upper_side_L.
/// Причина — FacesOverlap: на границе Y (interV = 2.242 касается 2.242)
/// из-за float-погрешности перекрытие либо 0% (правильно), либо ~100% (ошибка).
/// Без epsilon-допуска поведение недетерминировано.
/// </summary>
public class RadialShelfSnapBugTests : SnapTestBase
{
    private KitchenElement? _side;
    private KitchenElement? _top;

    [SetUp]
    public void SetupBugScene()
    {
        // B4_upper_side_L: вертикальная боковина 18×900×332
        _side = Make("side",
            new Vector3Int(18, 900, 332),
            new Vector3(-0.764f, 1.81f, -3.454f));

        // B4_upper_top: горизонтальная крышка 564×332×18 (rotX=90)
        _top = Make("top",
            new Vector3Int(564, 332, 18),
            new Vector3(-0.473f, 2.251f, -3.454f),
            ManagedRotation.Euler(90f, 0f, 0f));
    }

    [Test]
    public void RadialShelf_SnapsToSideWhenTopBoundaryOverlapIsMarginal()
    {
        // Радиальная полка: 314×18×332, rotY=270
        var shelf = Make("radial",
            new Vector3Int(314, 18, 332),
            new Vector3(-0.921f, 2.233f, -3.445f),
            ManagedRotation.Euler(0f, 270f, 0f));

        var others = new List<KitchenElement> { _side, _top };

        // Сдвигаем полку по X на 9 мм (gap меньше порога 50 мм) —
        // снэп должен притянуть обратно к боковине.
        var testPos = new Vector3(-0.93f, 2.233f, -3.445f);
        var result = SnapSystem.TrySnap(shelf, others, testPos);

        Assert.IsTrue(result.snapped,
            $"Полка должна прилипнуть из позиции {testPos}");
        Assert.IsTrue(result.targetName.Contains("side"),
            $"Цель снэпа должна быть боковиной (side), а не '{result.targetName}'");

        // После снэпа к боковине: X = -0.939 (центр, заподлицо грань+ к грани- боковины)
        Assert.AreEqual(-0.939f, result.position.x, 0.002f, "X — заподлицо с боковиной");
    }

    [Test]
    public void ShelfSnapsToSide_EvenWithOffsetY_AwayFromTopBoundary()
    {
        var shelf = Make("radial",
            new Vector3Int(314, 18, 332),
            new Vector3(-0.921f, 2.233f, -3.445f),
            ManagedRotation.Euler(0f, 270f, 0f));

        var others = new List<KitchenElement> { _side, _top };

        // Сдвиг 9 мм по X + чуть вниз по Y чтобы уйти от границы крышки
        var testPos = new Vector3(-0.93f, 2.231f, -3.445f);
        var result = SnapSystem.TrySnap(shelf, others, testPos);

        Assert.IsTrue(result.snapped);
        Assert.IsTrue(result.targetName.Contains("side"),
            $"Должна быть боковина, не '{result.targetName}'");
    }

    [Test]
    public void SideBoardFace_OverlapsCorrectly_WithRotatedShelf()
    {
        // Проверяем геометрию: грань +X полки (rotY=270) и грань -X боковины
        // должны быть параллельны (dot ≈ -1) и перекрываться.
        var shelf = Make("radial",
            new Vector3Int(314, 18, 332),
            new Vector3(-0.93f, 2.233f, -3.445f),
            ManagedRotation.Euler(0f, 270f, 0f));

        var result = SnapSystem.TrySnap(shelf, new List<KitchenElement> { _side }, shelf.transform.position);

        Assert.IsTrue(result.snapped,
            "Полка на одной линии с боковиной — снэп должен сработать даже с нулевым сдвигом");
        Assert.AreEqual("side", result.targetName);
    }

    [Test]
    public void TopBoardAlone_SnapBoundaryCase_Deterministic()
    {
        // Проверка, что снэп к одной только крышке (top) на границе Y
        // возвращает детерминированный результат (а не «как повезёт с float»).
        var shelf = Make("radial",
            new Vector3Int(314, 18, 332),
            new Vector3(-0.921f, 2.233f, -3.445f),
            ManagedRotation.Euler(0f, 270f, 0f));

        // 5 итераций — все должны дать одинаковый результат
        bool? firstSnapped = null;
        string? firstTarget = null;
        for (int i = 0; i < 5; i++)
        {
            var testPos = new Vector3(-0.93f, 2.233f, -3.445f);
            var result = SnapSystem.TrySnap(shelf, new List<KitchenElement> { _top }, testPos);
            if (firstSnapped == null)
            {
                firstSnapped = result.snapped;
                firstTarget = result.targetName;
            }
            Assert.AreEqual(firstSnapped, result.snapped,
                $"Итерация {i}: snapped должен быть стабильным");
            if (result.snapped)
                Assert.AreEqual(firstTarget, result.targetName,
                    $"Итерация {i}: target должен быть стабильным");
        }
    }

    [Test]
    public void SideAndTop_BothSameXFace_SideWins_Stable()
    {
        // Основной баг-кейс: боковина и крышка делят грань X=-0.755.
        // Полка у границы Y крышки — снэп должен стабильно выбирать боковину.
        var shelf = Make("radial",
            new Vector3Int(314, 18, 332),
            new Vector3(-0.921f, 2.233f, -3.445f),
            ManagedRotation.Euler(0f, 270f, 0f));

        var others = new List<KitchenElement> { _side, _top };

        bool allSnapped = true;
        for (int i = 0; i < 5; i++)
        {
            var testPos = new Vector3(-0.93f, 2.233f, -3.445f);
            var result = SnapSystem.TrySnap(shelf, others, testPos);
            if (!result.snapped)
            {
                allSnapped = false;
                Assert.Fail($"Итерация {i}: не прилипло (snapTarget={result.targetName})");
            }
            if (!result.targetName.Contains("side"))
            {
                Assert.Fail($"Итерация {i}: цель '{result.targetName}' вместо боковины");
            }
        }
        Assert.IsTrue(allSnapped);
    }
}
