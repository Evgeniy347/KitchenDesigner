using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Геометрия пазов детали: обозначение «Паз (16*4*7)» — смещение от
/// кромки 16 мм, ширина прорези 4 мм, глубина 7 мм. Сквозной идёт во всю длину
/// стороны, глухой не доходит до торцов на 7 мм с каждой стороны.</summary>
public class GrooveMeshTests
{
    // Деталь 800×400×18: доли считаются от этих размеров.
    private static readonly Vector3Int Dims = new Vector3Int(800, 400, 18);

    private static GrooveMesh.Rect2 Rect(GrooveKind kind, GrooveSide side)
        => GrooveMesh.ComputeRect(Dims, new GrooveSpec(kind, side));

    [Test]
    public void ThroughGroove_SpansFullSide()
    {
        var r = Rect(GrooveKind.Through, GrooveSide.Top);
        Assert.AreEqual(-0.5f, r.xMin, 1e-5f, "сквозной паз доходит до левого торца");
        Assert.AreEqual(0.5f, r.xMax, 1e-5f, "сквозной паз доходит до правого торца");
    }

    [Test]
    public void BlindGroove_StopsShortOfBothEnds()
    {
        var r = Rect(GrooveKind.Blind, GrooveSide.Top);
        float end = (float)AppConstants.GROOVE_BLIND_END_MM / Dims.x;

        Assert.AreEqual(-0.5f + end, r.xMin, 1e-5f);
        Assert.AreEqual(0.5f - end, r.xMax, 1e-5f);
        // Длина глухого паза = сторона − 2×7 мм.
        Assert.AreEqual(Dims.x - 2 * AppConstants.GROOVE_BLIND_END_MM,
            (r.xMax - r.xMin) * Dims.x, 1e-2f);
    }

    [Test]
    public void Groove_OffsetMeasuredFromNamedEdge()
    {
        float off = (float)AppConstants.GROOVE_OFFSET_MM / Dims.y;
        float wid = (float)AppConstants.GROOVE_WIDTH_MM / Dims.y;

        var top = Rect(GrooveKind.Through, GrooveSide.Top);
        Assert.AreEqual(0.5f - off, top.yMax, 1e-5f, "16 мм от ВЕРХНЕЙ кромки");
        Assert.AreEqual(0.5f - off - wid, top.yMin, 1e-5f);

        var bottom = Rect(GrooveKind.Through, GrooveSide.Bottom);
        Assert.AreEqual(-0.5f + off, bottom.yMin, 1e-5f, "16 мм от НИЖНЕЙ кромки");
        Assert.AreEqual(-0.5f + off + wid, bottom.yMax, 1e-5f);
    }

    [Test]
    public void LeftAndRightGrooves_AreVerticalAndMirrored()
    {
        float off = (float)AppConstants.GROOVE_OFFSET_MM / Dims.x;
        float wid = (float)AppConstants.GROOVE_WIDTH_MM / Dims.x;

        var left = Rect(GrooveKind.Through, GrooveSide.Left);
        Assert.AreEqual(-0.5f + off, left.xMin, 1e-5f);
        Assert.AreEqual(-0.5f + off + wid, left.xMax, 1e-5f);
        Assert.AreEqual(-0.5f, left.yMin, 1e-5f, "сквозной по вертикали — во всю высоту");

        var right = Rect(GrooveKind.Through, GrooveSide.Right);
        Assert.AreEqual(0.5f - off, right.xMax, 1e-5f);
        Assert.AreEqual(0.5f - off - wid, right.xMin, 1e-5f);
    }

    [Test]
    public void Groove_WidthIs4mm_RegardlessOfPartSize()
    {
        foreach (var dims in new[] { new Vector3Int(400, 300, 18), new Vector3Int(2000, 900, 18) })
        {
            var r = GrooveMesh.ComputeRect(dims, new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
            Assert.AreEqual(AppConstants.GROOVE_WIDTH_MM, (r.yMax - r.yMin) * dims.y, 1e-2f,
                $"ширина прорези 4 мм при {dims.x}×{dims.y}");
        }
    }

    [Test]
    public void Groove_DoesNotFitOnNarrowSide_IsSkipped()
    {
        // Сторона 18 мм: смещение 16 + ширина 4 не помещаются.
        var dims = new Vector3Int(800, 18, 18);
        var r = GrooveMesh.ComputeRect(dims, new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        Assert.IsFalse(r.IsValid, "не помещающийся паз не строится");

        var rects = GrooveMesh.ComputeRects(dims,
            new List<GrooveSpec> { new GrooveSpec(GrooveKind.Through, GrooveSide.Top) });
        Assert.AreEqual(0, rects.Count, "невалидные пазы отсеиваются");
    }

    [Test]
    public void DepthFraction_Is7mmOfThickness_ClampedInsideBoard()
    {
        Assert.AreEqual(7f / 18f, GrooveMesh.DepthFraction(Dims), 1e-5f);
        // Тонкая деталь: паз не должен резать насквозь.
        Assert.LessOrEqual(GrooveMesh.DepthFraction(new Vector3Int(800, 400, 4)), 0.9f);
    }

    [Test]
    public void Build_WithoutGrooves_ProducesClosedBox()
    {
        var mesh = GrooveMesh.Build(Dims, null);
        Assert.AreEqual(2, mesh.subMeshCount, "сабмеши: тело + пазы");
        Assert.AreEqual(0, mesh.GetTriangles(1).Length, "без пазов тёмный сабмеш пуст");
        // 6 граней × 2 треугольника × 3 индекса.
        Assert.AreEqual(36, mesh.GetTriangles(0).Length);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Build_WithGroove_AddsGrooveSubmeshAndKeepsBodyIntact()
    {
        var grooves = new List<GrooveSpec> { new GrooveSpec(GrooveKind.Blind, GrooveSide.Top) };
        var mesh = GrooveMesh.Build(Dims, grooves);

        Assert.Greater(mesh.GetTriangles(1).Length, 0, "дно и стенки кармана — в сабмеше пазов");
        Assert.Greater(mesh.GetTriangles(0).Length, 36, "лицевая грань тесселирована вокруг паза");
        Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, "UV есть у каждой вершины — иначе поплывёт декор");
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Build_CrossingGrooves_ProduceSingleConsistentMesh()
    {
        // Сквозной сверху + сквозной слева пересекаются — решётка должна
        // обработать пересечение без дублей и дыр.
        var grooves = new List<GrooveSpec>
        {
            new GrooveSpec(GrooveKind.Through, GrooveSide.Top),
            new GrooveSpec(GrooveKind.Through, GrooveSide.Left),
        };
        var mesh = GrooveMesh.Build(Dims, grooves);

        Assert.Greater(mesh.GetTriangles(1).Length, 0);
        Assert.AreEqual(0, mesh.GetTriangles(0).Length % 3);
        Assert.AreEqual(0, mesh.GetTriangles(1).Length % 3);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Build_PocketFloor_SitsAtGrooveDepthInsideGrooveFootprint()
    {
        var spec = new GrooveSpec(GrooveKind.Blind, GrooveSide.Top);
        var mesh = GrooveMesh.Build(Dims, new List<GrooveSpec> { spec });
        var rect = GrooveMesh.ComputeRect(Dims, spec);
        float floorZ = 0.5f - GrooveMesh.DepthFraction(Dims);

        var verts = mesh.vertices;
        int onFloor = 0;
        foreach (var v in verts)
        {
            // Ни одна вершина не должна вылезать за габарит детали.
            Assert.LessOrEqual(Mathf.Abs(v.x), 0.5f + 1e-4f);
            Assert.LessOrEqual(Mathf.Abs(v.y), 0.5f + 1e-4f);
            Assert.LessOrEqual(Mathf.Abs(v.z), 0.5f + 1e-4f);

            if (Mathf.Abs(v.z - floorZ) > 1e-4f) continue;
            onFloor++;
            // Дно кармана лежит строго в границах паза.
            Assert.GreaterOrEqual(v.x, rect.xMin - 1e-4f);
            Assert.LessOrEqual(v.x, rect.xMax + 1e-4f);
            Assert.GreaterOrEqual(v.y, rect.yMin - 1e-4f);
            Assert.LessOrEqual(v.y, rect.yMax + 1e-4f);
        }
        Assert.Greater(onFloor, 0, "карман паза должен иметь дно на глубине 7 мм");

        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Build_ThroughGroove_OpensOntoBothEdges()
    {
        // У сквозного паза торец в зоне паза срезан до дна: вершин торца на
        // полной глубине пласти в этой полосе быть не должно.
        var spec = new GrooveSpec(GrooveKind.Through, GrooveSide.Top);
        var mesh = GrooveMesh.Build(Dims, new List<GrooveSpec> { spec });
        var rect = GrooveMesh.ComputeRect(Dims, spec);
        float midY = (rect.yMin + rect.yMax) * 0.5f;

        foreach (var v in mesh.vertices)
        {
            bool onLeftEdge = Mathf.Abs(v.x + 0.5f) < 1e-4f;
            bool inGrooveBand = v.y > rect.yMin + 1e-4f && v.y < rect.yMax - 1e-4f;
            if (onLeftEdge && inGrooveBand)
                Assert.Less(v.z, 0.5f - 1e-4f,
                    $"торец в полосе паза (y≈{midY:F3}) не должен доходить до пласти");
        }
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Designation_MatchesCatalogNotation()
    {
        Assert.AreEqual("Сквозной 16*4*7", GrooveSpec.Designation(GrooveKind.Through));
        Assert.AreEqual("Глухой 16*4*7", GrooveSpec.Designation(GrooveKind.Blind));
        Assert.AreEqual("Глухой 16*4*7:Лево",
            new GrooveSpec(GrooveKind.Blind, GrooveSide.Left).ToString());
    }
}
