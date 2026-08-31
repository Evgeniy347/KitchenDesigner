using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Развёртка пола на живом меше: у FloorPolygonMesh её не было ВООБСЕМ,
/// поэтому любой декор на полу вёл себя непредсказуемо — плитка не мостилась, а
/// доставалась от того, что осталось в буфере.
///
/// Почему оси именно (X, Z) и почему ноль в начале мира — разобрано в
/// FloorDecorUvTests (быстрый путь, dotnet). Здесь проверяется, что меш и элемент
/// эту развёртку действительно несут: 0..1 от меша, умноженные на
/// BaseMap_ST = DecorSurfaceMM / TileMM, обязаны давать шаг плитки в физических
/// миллиметрах (CONVENTIONS.md → «a decor tiles, it never stretches»).
///
/// Контур намеренно НЕсимметричный (Г-образный, 3000×2000): на квадрате и оси, и
/// габарит совпадают, и тест зеленеет против кода, который неверен везде.</summary>
public class FloorPolygonMeshUvTests
{
    private const int WidthMm = 3000;
    private const int DepthMm = 2000;
    private const int ThicknessMm = 100;
    private const int TileMm = 600;

    private static readonly Vector2 PivotWorldMm = new Vector2(5000f, 3000f);

    private static readonly Vector2Int[] LShape =
    {
        new Vector2Int(-1500, -1000), new Vector2Int(1500, -1000), new Vector2Int(1500, 0),
        new Vector2Int(0, 0), new Vector2Int(0, 1000), new Vector2Int(-1500, 1000),
    };

    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    private const float Tol = 0.5f;

    private readonly List<Object> _spawned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
        _spawned.Clear();
    }

    private Mesh BuildLShape()
    {
        var mesh = FloorPolygonMesh.Build(LShape,
            new Vector3Int(WidthMm, ThicknessMm, DepthMm), PivotWorldMm);
        _spawned.Add(mesh);
        return mesh;
    }

    private static Vector2 Surface => new Vector2Int(WidthMm, DepthMm);

    private static int TopAndBottomVertexCount => LShape.Length * 2;

    [Test]
    public void Build_WritesOneUvPerVertex()
    {
        var mesh = BuildLShape();

        Assert.AreEqual(mesh.vertexCount, mesh.uv.Length,
            "меш без развёртки отдаёт шейдеру пустой массив UV: декор на полу тогда не "
            + "мостится вовсе, а это единственный смысл бесшовных текстур");
    }

    [Test]
    public void Build_TopFaceUv_TimesDecorSurface_IsTheWorldMillimetre()
    {
        var mesh = BuildLShape();
        var verts = mesh.vertices;
        var uv = mesh.uv;

        for (int i = 0; i < TopAndBottomVertexCount; i++)
        {
            float worldXMm = verts[i].x * WidthMm + PivotWorldMm.x;
            float worldZMm = verts[i].z * DepthMm + PivotWorldMm.y;

            Assert.AreEqual(worldXMm, uv[i].x * Surface.x, Tol,
                "вершина " + i + ": развёртка верхней грани обязана идти по мировым X/Z — "
                + "тем самым осям, которые называет FloorElement.DecorSurfaceMM");
            Assert.AreEqual(worldZMm, uv[i].y * Surface.y, Tol, "вершина " + i);
        }
    }

    [Test]
    public void Build_TopFaceUv_ThroughTileST_RepeatsTheTileEveryTileMillimetres()
    {
        var mesh = BuildLShape();
        var verts = mesh.vertices;
        var uv = mesh.uv;
        var st = MaterialManager.ComputeTileST(new Vector2Int(WidthMm, DepthMm), TileMm, TileMm);

        float leftU = 0f, rightU = 0f;
        float leftX = float.MaxValue, rightX = float.MinValue;
        for (int i = 0; i < TopAndBottomVertexCount; i++)
        {
            if (verts[i].x * WidthMm < leftX) { leftX = verts[i].x * WidthMm; leftU = uv[i].x; }
            if (verts[i].x * WidthMm > rightX) { rightX = verts[i].x * WidthMm; rightU = uv[i].x; }
        }

        Assert.AreEqual(3000f, rightX - leftX, Tol, "контрольная пара взята по краям плиты");
        Assert.AreEqual(5f, (rightU - leftU) * st.x, 0.001f,
            "3000 мм плиты при плитке 600 мм — это ровно 5 повторов рисунка, а не один "
            + "растянутый на всю плиту: BaseMap_ST = DecorSurfaceMM / TileMM умножает 0..1 "
            + "меша, поэтому развёртка обязана быть в тех же осях и в том же масштабе");
    }

    [Test]
    public void Build_SideFaceUv_SpansTheThickness_NotACollapsedLine()
    {
        var mesh = BuildLShape();
        var uv = mesh.uv;

        float lowest = float.MaxValue, highest = float.MinValue;
        for (int i = TopAndBottomVertexCount; i < uv.Length; i++)
        {
            float mm = uv[i].y * Surface.y;
            if (mm < lowest) lowest = mm;
            if (mm > highest) highest = mm;
        }

        Assert.AreEqual(0f, lowest, Tol, "низ торца — ноль по второй оси");
        Assert.AreEqual(ThicknessMm, highest, Tol,
            "торец плиты разворачивается по (обход контура, толщина): проекция сверху "
            + "схлопнула бы его в линию и размазала один ряд пикселей на все 100 мм");
    }

    [Test]
    public void Build_SideFaceUv_RunsAlongThePerimeterInMillimetres()
    {
        var mesh = BuildLShape();
        var uv = mesh.uv;

        float longest = 0f;
        for (int i = TopAndBottomVertexCount; i < uv.Length; i++)
        {
            float mm = uv[i].x * Surface.x;
            if (mm > longest) longest = mm;
        }

        Assert.AreEqual(10000f, longest, Tol,
            "обход Г-образного контура 3000+1000+1500+1000+1500+2000 = 10000 мм: торец "
            + "мостится по ДЛИНЕ обхода в физических миллиметрах, поэтому последняя вершина "
            + "стоит ровно на периметре");
    }

    [Test]
    public void FloorElement_DecorSurfaceMM_IsWidthAndDepth_NotWidthAndThickness()
    {
        var floor = CreateFloor(new Vector3(5f, -0.05f, 3f));

        Assert.AreEqual(new Vector2Int(WidthMm, DepthMm), floor.DecorSurfaceMM,
            "пол лежит горизонтально: вторая ось его поверхности — глубина. Базовые "
            + "(dims.x, dims.y) взяли бы ТОЛЩИНУ плиты (100 мм) и растянули плитку по "
            + "глубине в двадцать раз");
    }

    [Test]
    public void FloorElement_SetPolygonLocalMm_AnchorsTheUvToTheWorld_NotToThePivot()
    {
        var floor = CreateFloor(new Vector3(5f, -0.05f, 3f));
        floor.SetPolygonLocalMm(LShape);

        var mesh = floor.GetComponent<MeshFilter>().sharedMesh;
        Assert.IsNotNull(mesh);
        var verts = mesh!.vertices;
        var uv = mesh.uv;
        Assert.AreEqual(verts.Length, uv.Length, "меш пола обязан нести развёртку");

        for (int i = 0; i < TopAndBottomVertexCount; i++)
            Assert.AreEqual(verts[i].x * WidthMm + PivotWorldMm.x, uv[i].x * floor.DecorSurfaceMM.x,
                Tol,
                "ноль развёртки взят в начале МИРА: точка привязки пола — центр габарита "
                + "контура, она уезжает при каждой правке контура и утащила бы за собой "
                + "уже уложенную плитку по всей комнате");
    }

    [Test]
    public void FloorElement_Tiling_RepeatsTheDecorByItsPhysicalSize()
    {
        var def = MaterialCatalog.Get("oak");
        var tile = MaterialManager.TileMM(def);
        Assume.That(WidthMm, Is.Not.EqualTo(tile.x),
            "плита обязана отличаться от плитки, иначе тест зелен и на растянутом декоре");

        var floor = CreateFloor(new Vector3(5f, -0.05f, 3f));
        floor.SetPolygonLocalMm(LShape);
        MaterialManager.ApplyById(floor, def.id);

        var mpb = new MaterialPropertyBlock();
        var renderer = ((KitchenElement)floor).DecorRenderer;
        Assert.IsNotNull(renderer, "декор пола некуда положить: у элемента нет рендерера");
        renderer!.GetPropertyBlock(mpb);
        var st = mpb.GetVector(BaseMapST);

        Assert.AreEqual(WidthMm / (float)tile.x, st.x, 1e-3f,
            "декор до пола ДОХОДИТ (в контекстном меню у пола есть строка «Текстура», и она "
            + "зовёт этот же MaterialManager.ApplyById): по ширине рисунок обязан повториться "
            + "dims.x / tileWidth раз");
        Assert.AreEqual(DepthMm / (float)tile.y, st.y, 1e-3f,
            "по глубине — dims.z / tileHeight; раньше сюда шла ТОЛЩИНА плиты (100 мм)");
    }

    private FloorElement CreateFloor(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = position;
        _spawned.Add(go);
        var floor = go.AddComponent<FloorElement>();
        floor.PartName = "ПолРазвёртка";
        floor.DimensionsMM = new Vector3Int(WidthMm, ThicknessMm, DepthMm);
        return floor;
    }
}
