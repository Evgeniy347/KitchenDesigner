using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Подложка некромкованного торца: декор — плёнка на пласти, торец без
/// кромки показывает голую плиту (декор «МДФ шлифованная», а без него белый).
/// Торцы уходят в СВОЙ сабмеш, пласть остаётся на декоре.</summary>
public class EdgeSubstrateTests
{
    // Деталь-лист 800×400×18: толщина по Z, торцы — ±X и ±Y.
    private static readonly Vector3Int Sheet = new Vector3Int(800, 400, 18);

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<Mesh> _meshes = new List<Mesh>();

    private Mesh Build(Vector3Int dims, int bareThickAxis,
        out GrooveMesh.SubmeshLayout layout, IReadOnlyList<GrooveSpec>? grooves = null,
        int holeAxis = 2, IReadOnlyList<GrooveMesh.Rect2>? holes = null)
    {
        var mesh = GrooveMesh.Build(dims, grooves, holes, holeAxis, bareThickAxis, out layout);
        _meshes.Add(mesh);
        return mesh;
    }

    /// <summary>Наибольшая проекция нормалей сабмеша на ось — 0 значит «ни одна
    /// грань сабмеша не смотрит вдоль этой оси».</summary>
    private static float MaxNormalAlong(Mesh mesh, int submesh, Vector3 axis)
    {
        var normals = mesh.normals;
        float max = 0f;
        foreach (int i in mesh.GetTriangles(submesh))
            max = Mathf.Max(max, Mathf.Abs(Vector3.Dot(normals[i], axis)));
        return max;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var m in _meshes) if (m != null) Object.DestroyImmediate(m);
        _meshes.Clear();
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        MaterialManager.ClearCache();
        MaterialCatalog.Reset();
    }

    // --- Геометрия: какие грани уходят в подложку ---

    [Test]
    public void Build_WithoutBareEnds_KeepsSingleSubmesh()
    {
        var mesh = Build(Sheet, -1, out var layout);
        Assert.AreEqual(1, mesh.subMeshCount, "кромкованная деталь — одна коробка одним декором");
        Assert.AreEqual(-1, layout.BareEnds);
        Assert.AreEqual(36, mesh.GetTriangles(0).Length);
    }

    [Test]
    public void Build_BareEnds_SplitsFourEndFacesOffTheDecor()
    {
        var mesh = Build(Sheet, 2, out var layout);

        Assert.AreEqual(2, mesh.subMeshCount);
        Assert.AreEqual(1, layout.BareEnds, "без пазов подложка — первый добавочный сабмеш");
        // 4 торца и 2 пласти, по 2 треугольника на грань.
        Assert.AreEqual(24, mesh.GetTriangles(layout.BareEnds).Length, "в подложке ровно 4 торца");
        Assert.AreEqual(12, mesh.GetTriangles(0).Length, "на декоре остались только 2 пласти");
    }

    [Test]
    public void Build_BareEnds_TakesFacesAcrossThickness_NotThePlanes()
    {
        var mesh = Build(Sheet, 2, out var layout);

        // Толщина по Z: пласти смотрят вдоль Z, торцы — вдоль X и Y.
        Assert.AreEqual(0f, MaxNormalAlong(mesh, layout.BareEnds, Vector3.forward), 1e-3f,
            "пласть в подложку попасть не может");
        Assert.AreEqual(1f, MaxNormalAlong(mesh, 0, Vector3.forward), 1e-3f,
            "пласти остались на декоре");
    }

    [Test]
    public void Build_BareEnds_ThicknessAlongY_PicksXAndZFaces()
    {
        // Столешница-короб 1200×40×600: толщина лежит по Y, торцы — ±X и ±Z.
        var dims = new Vector3Int(1200, 40, 600);
        var mesh = Build(dims, 1, out var layout);

        Assert.AreEqual(24, mesh.GetTriangles(layout.BareEnds).Length);
        Assert.AreEqual(0f, MaxNormalAlong(mesh, layout.BareEnds, Vector3.up), 1e-3f,
            "верх и низ столешницы — пласть, а не торец");
    }

    [Test]
    public void Build_BareEnds_SurvivesPermutedHoleAxis()
    {
        // Тот же короб, но с вырезом под мойку: меш строится канонически и
        // переставляется по осям — ось толщины обязана переехать вместе с ним.
        var dims = new Vector3Int(1200, 40, 600);
        var holes = new List<GrooveMesh.Rect2>
        {
            new GrooveMesh.Rect2 { xMin = -0.2f, xMax = 0.2f, yMin = -0.2f, yMax = 0.2f },
        };
        var mesh = Build(dims, 1, out var layout, holeAxis: 1, holes: holes);

        Assert.Greater(mesh.GetTriangles(layout.BareEnds).Length, 0);
        Assert.AreEqual(0f, MaxNormalAlong(mesh, layout.BareEnds, Vector3.up), 1e-3f,
            "после перестановки подложка не должна съехать на пласть");
    }

    [Test]
    public void Build_BareEndsWithGrooves_KeepsBothExtraSubmeshes()
    {
        var grooves = new List<GrooveSpec> { new GrooveSpec(GrooveKind.Blind, GrooveSide.Top) };
        var mesh = Build(Sheet, 2, out var layout, grooves);

        Assert.AreEqual(3, mesh.subMeshCount);
        Assert.AreEqual(1, layout.Grooves);
        Assert.AreEqual(2, layout.BareEnds);
        Assert.Greater(mesh.GetTriangles(layout.Grooves).Length, 0, "дно кармана осталось своим");
        // Торцы у детали с пазом нарезаны решёткой на полосы, поэтому счёт
        // треугольников уже не фиксирован — важно, что это по-прежнему ТОРЦЫ.
        Assert.Greater(mesh.GetTriangles(layout.BareEnds).Length, 0);
        Assert.AreEqual(0f, MaxNormalAlong(mesh, layout.BareEnds, Vector3.forward), 1e-3f,
            "паз режет пласть — в подложку она попасть всё равно не должна");
    }

    // --- Каталог: чем именно красится торец ---

    [Test]
    public void Substrate_ProjectCatalog_HasTheSandedMdfDecor()
    {
        // Запись живёт в StreamingAssets/Textures/index.json; исчезнет она —
        // торцы молча побелеют, и заметить это будет уже не на чем.
        var def = EdgeSubstrate.Decor();
        Assert.IsNotNull(def, $"в каталоге нет декора «{EdgeSubstrate.DecorName}»");
        Assert.AreEqual(EdgeSubstrate.DecorName, def!.displayName);
    }

    [Test]
    public void Substrate_Material_ShowsTheDecorItself_NotTheWhiteFallback()
    {
        // Материал подложки — это материал декора из каталога со своей картинкой.
        // Белый тут значил бы, что декор не нашёлся и торцы молча побелели.
        var mat = EdgeSubstrate.Material();
        Assert.IsNotNull(mat);
        Assert.IsNotNull(mat!.mainTexture, "картинка «МДФ шлифованная» не доехала до материала");
        Assert.AreEqual(MaterialManager.GetSharedMaterial(EdgeSubstrate.Decor()!), mat,
            "подложка обязана делить материал с декором — иначе лишний батч на деталь");
    }

    [Test]
    public void Substrate_WithoutTheDecor_FallsBackToPlainWhite()
    {
        MaterialCatalog.Load(new[]
        {
            new MaterialDef(MaterialCatalog.DefaultId, "Серый", "ЛДСП", Color.gray),
        });

        Assert.IsNull(EdgeSubstrate.Decor());
        var mat = EdgeSubstrate.Material();
        Assert.IsNotNull(mat, "без декора подложка обязана остаться белой, а не пропасть");
        Assert.AreEqual(Color.white, mat!.color);
    }

    // --- Деталь целиком: выключатель кромки правит материалы сабмешей ---

    [Test]
    public void Part_EdgeBandingOff_PutsSubstrateOnTheEnds()
    {
        var e = MakePart(new Vector3Int(600, 18, 500));
        var renderer = e.GetComponent<MeshRenderer>();
        Assert.AreEqual(1, renderer.sharedMaterials.Length, "с кромкой деталь одноматериальная");

        e.EdgeBandingEnabled = false;

        var mats = renderer.sharedMaterials;
        Assert.AreEqual(2, mats.Length, "торцы получили свой материал");
        Assert.AreEqual(EdgeSubstrate.Material(), mats[1]);
        Assert.AreNotEqual(mats[0], mats[1], "торец и пласть красятся по-разному");
    }

    [Test]
    public void Part_EdgeBandingBackOn_ReturnsToOneMaterial()
    {
        var e = MakePart(new Vector3Int(600, 18, 500));
        e.EdgeBandingEnabled = false;
        e.EdgeBandingEnabled = true;

        Assert.AreEqual(1, e.GetComponent<MeshRenderer>().sharedMaterials.Length);
    }

    [Test]
    public void Part_NotASheet_HasNoEndsAtAll()
    {
        // Брусок: тонких сторон больше одной, торец не определён — кромковать
        // нечего, и подложке взяться неоткуда.
        var e = MakePart(new Vector3Int(40, 40, 400));
        e.EdgeBandingEnabled = false;

        Assert.AreEqual(1, e.GetComponent<MeshRenderer>().sharedMaterials.Length);
    }

    [Test]
    public void Part_ResizedIntoASheet_GetsSubstrateWithoutTouchingTheToggle()
    {
        // Кромкование выключено ещё на бруске, а листом деталь становится
        // ресайзом — пересборка меша обязана заметить это сама.
        var e = MakePart(new Vector3Int(40, 40, 400));
        e.EdgeBandingEnabled = false;

        e.DimensionsMM = new Vector3Int(600, 18, 500);
        e.ApplyDimensions();

        Assert.AreEqual(2, e.GetComponent<MeshRenderer>().sharedMaterials.Length);
    }

    private KitchenElement MakePart(Vector3Int dims)
    {
        var go = ElementFactory.CreatePart(dims, "Part", Vector3.zero);
        _spawned.Add(go);
        var e = go.GetComponent<KitchenElement>();
        e.DimensionsMM = dims;
        e.ApplyDimensions();
        return e;
    }
}
