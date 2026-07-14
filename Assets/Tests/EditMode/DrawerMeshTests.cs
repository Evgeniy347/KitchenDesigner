using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Раскладка короба ящика GTV AXIS PRO: зазоры и размеры панелей
/// по каталогу (брошюра «Преимущества», стр. 8, версия 1).</summary>
public class DrawerMeshTests
{
    private const int LW = 400;
    private const int NL = 500;

    private static DrawerMesh.Box Find(List<DrawerMesh.Box> boxes, string name)
    {
        foreach (var b in boxes)
            if (b.name.StartsWith(name)) return b;
        Assert.Fail($"Панель «{name}» не найдена");
        return default;
    }

    [Test]
    public void ComputeBoxes_ReturnsFourPanels()
    {
        var boxes = DrawerMesh.ComputeBoxes(LW, DrawerType.A, NL);
        Assert.AreEqual(4, boxes.Count);
    }

    [Test]
    public void SideWalls_14mm_InnerFaceAtSlideClearance()
    {
        var boxes = DrawerMesh.ComputeBoxes(LW, DrawerType.A, NL);
        var left = Find(boxes, "Боковина L");
        var right = Find(boxes, "Боковина R");

        Assert.AreEqual(14f, left.sizeMM.x, 1e-3f);
        Assert.AreEqual(14f, right.sizeMM.x, 1e-3f);
        // Внутренняя грань боковины на 37.5 от стенки проёма.
        Assert.AreEqual(37.5f, left.minMM.x + left.sizeMM.x, 1e-3f);
        Assert.AreEqual(LW - 37.5f, right.minMM.x, 1e-3f);
        // Высота = высота боковины, длина = NL.
        Assert.AreEqual(86f, left.sizeMM.y, 1e-3f);
        Assert.AreEqual(NL, left.sizeMM.z, 1e-3f);
    }

    [Test]
    public void Bottom_LWminus75_NLminus24_16mm()
    {
        var boxes = DrawerMesh.ComputeBoxes(LW, DrawerType.A, NL);
        var bottom = Find(boxes, "Дно");

        Assert.AreEqual(LW - 75f, bottom.sizeMM.x, 1e-3f);
        Assert.AreEqual(16f, bottom.sizeMM.y, 1e-3f);
        Assert.AreEqual(NL - 24f, bottom.sizeMM.z, 1e-3f);
        // Дно лежит между боковинами и заподлицо с передним торцом (z = NL).
        Assert.AreEqual(37.5f, bottom.minMM.x, 1e-3f);
        Assert.AreEqual(NL, bottom.minMM.z + bottom.sizeMM.z, 1e-3f);
    }

    [Test]
    public void Back_LWminus87_FullHeight_RearOffset8()
    {
        var boxes = DrawerMesh.ComputeBoxes(LW, DrawerType.A, NL);
        var back = Find(boxes, "Задник");

        Assert.AreEqual(LW - 87f, back.sizeMM.x, 1e-3f);
        Assert.AreEqual(84f, back.sizeMM.y, 1e-3f); // версия 1: задник до низа
        Assert.AreEqual(16f, back.sizeMM.z, 1e-3f);
        // Задняя грань на 8 мм от заднего торца, по X — центрован.
        Assert.AreEqual(8f, back.minMM.z, 1e-3f);
        Assert.AreEqual(43.5f, back.minMM.x, 1e-3f);
    }

    [Test]
    public void BottomButtsAgainstBack()
    {
        var boxes = DrawerMesh.ComputeBoxes(LW, DrawerType.A, NL);
        var bottom = Find(boxes, "Дно");
        var back = Find(boxes, "Задник");
        // Версия 1: задний торец дна совпадает с передней гранью задника.
        Assert.AreEqual(back.minMM.z + back.sizeMM.z, bottom.minMM.z, 1e-3f);
    }

    [Test]
    public void AllPanels_InsideOutlineBox()
    {
        foreach (DrawerType type in System.Enum.GetValues(typeof(DrawerType)))
        {
            float h = DrawerConstants.GetMinOpeningHeight(type);
            foreach (var b in DrawerMesh.ComputeBoxes(LW, type, NL))
            {
                Assert.GreaterOrEqual(b.minMM.x, -1e-3f, $"{type} {b.name} X");
                Assert.LessOrEqual(b.minMM.x + b.sizeMM.x, LW + 1e-3f, $"{type} {b.name} X");
                Assert.GreaterOrEqual(b.minMM.y, -1e-3f, $"{type} {b.name} Y");
                Assert.LessOrEqual(b.minMM.y + b.sizeMM.y, h + 1e-3f, $"{type} {b.name} Y");
                Assert.GreaterOrEqual(b.minMM.z, -1e-3f, $"{type} {b.name} Z");
                Assert.LessOrEqual(b.minMM.z + b.sizeMM.z, NL + 1e-3f, $"{type} {b.name} Z");
            }
        }
    }

    [Test]
    public void PanelsLifted_BySlideMount()
    {
        var boxes = DrawerMesh.ComputeBoxes(LW, DrawerType.A, NL);
        float lift = DrawerConstants.GetBottomLift(DrawerType.A); // 21
        foreach (var b in boxes)
            Assert.AreEqual(lift, b.minMM.y, 1e-3f, $"{b.name} должен стоять на подъёме направляющих");
    }

    [Test]
    public void Build_ProducesMeshWithFourBoxes()
    {
        var mesh = DrawerMesh.Build(LW, DrawerType.B, NL);
        Assert.AreEqual(4 * 24, mesh.vertexCount); // 4 коробки × 24 вершины
        Assert.AreEqual(4 * 12 * 3, mesh.triangles.Length);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Build_NormalizedWithinUnitCube()
    {
        var mesh = DrawerMesh.Build(LW, DrawerType.D, NL);
        var bounds = mesh.bounds;
        Assert.LessOrEqual(bounds.max.x, 0.5f + 1e-4f);
        Assert.GreaterOrEqual(bounds.min.x, -0.5f - 1e-4f);
        Assert.LessOrEqual(bounds.max.y, 0.5f + 1e-4f);
        Assert.GreaterOrEqual(bounds.min.y, -0.5f - 1e-4f);
        Assert.LessOrEqual(bounds.max.z, 0.5f + 1e-4f);
        Assert.GreaterOrEqual(bounds.min.z, -0.5f - 1e-4f);
        Object.DestroyImmediate(mesh);
    }
}
