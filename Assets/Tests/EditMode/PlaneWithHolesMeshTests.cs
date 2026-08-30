using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Вырожденные входы построителя накладки текстуры.
///
/// Здесь живут причины, которые раньше были комментариями в PlaneWithHolesMesh.cs:
/// рисовать нечего — меша нет вовсе; чисто цветовой декор без физического размера
/// плитки не должен уносить UV в бесконечность; почти совпадающие границы проёмов
/// сливаются до нарезки, иначе между ними остаётся ячейка-волосок.
/// Основные случаи (одна плита, UV от начала грани, дыра под проёмом) уже держит
/// TextureOverlayTests.</summary>
public class PlaneWithHolesMeshTests
{
    private static readonly Vector2Int Tile = new Vector2Int(600, 600);

    private readonly List<Mesh> _meshes = new List<Mesh>();

    [TearDown]
    public void TearDown()
    {
        foreach (var m in _meshes) if (m != null) Object.DestroyImmediate(m);
        _meshes.Clear();
    }

    private Mesh? Build(RectInt area, IReadOnlyList<RectInt>? holes = null, Vector2Int? tile = null)
    {
        var mesh = PlaneWithHolesMesh.Build(area, holes, tile ?? Tile);
        if (mesh != null) _meshes.Add(mesh);
        return mesh;
    }

    [Test]
    public void AreaThinnerThanOneCell_BuildsNothing()
    {
        Assert.IsNull(Build(new RectInt(0, 0, 0, 2500)), "нулевая ширина — рисовать нечего");
        Assert.IsNull(Build(new RectInt(0, 0, 3000, 0)), "нулевая высота — тоже");
    }

    [Test]
    public void AreaFullyCoveredByAnOpening_BuildsNothing()
    {
        var holes = new List<RectInt> { new RectInt(-100, -100, 2000, 2000) };

        Assert.IsNull(Build(new RectInt(0, 0, 800, 800), holes),
            "накладка целиком в проёме: пустой меш — это невидимый объект-носитель, "
            + "а не плёнка, затягивающая проём");
    }

    [Test]
    public void AreaWithAnOpening_StillBuildsTheRestOfIt()
    {
        var holes = new List<RectInt> { new RectInt(200, 200, 400, 400) };

        var mesh = Build(new RectInt(0, 0, 800, 800), holes);

        Assert.IsNotNull(mesh,
            "положительный контроль: проём меньше области — остаток обязан остаться");
        Assert.Greater(mesh!.vertexCount, 0);
    }

    [Test]
    public void ColourOnlyDecor_WithoutAPhysicalTile_KeepsItsUvsFinite()
    {
        var mesh = Build(new RectInt(0, 0, 1200, 800), null, new Vector2Int(0, 0));

        Assert.IsNotNull(mesh);
        foreach (var uv in mesh!.uv)
        {
            Assert.IsFalse(float.IsInfinity(uv.x) || float.IsNaN(uv.x),
                "плитка 0 мм означала бы деление на ноль — UV улетели бы в бесконечность");
            Assert.IsFalse(float.IsInfinity(uv.y) || float.IsNaN(uv.y));
        }
    }

    [Test]
    public void TwoAbuttingOpenings_CoverTheWholeArea_AndLeaveNoSliverBetweenThem()
    {
        var holes = new List<RectInt>
        {
            new RectInt(0, 0, 1000, 800),
            new RectInt(1000, 0, 1000, 800),
        };

        var mesh = Build(new RectInt(0, 0, 2000, 800), holes);

        Assert.IsNull(mesh,
            "два проёма встык накрывают область целиком: на их общей границе не должно "
            + "остаться ни одной ячейки — иначе между окнами висела бы полоска плёнки");
    }
}
