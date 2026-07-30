using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Подложка некромкованного торца: декор — плёнка на пласти, торец без
/// кромки показывает голую плиту (декор «МДФ шлифованная», а без него белый).
/// Какие торцы кромкуются, решает EdgeBanding.Coverage — та же функция, что
/// печатает колонки кромок в спецификацию.</summary>
public class EdgeSubstrateTests
{
    // Деталь-лист 800×400×18: толщина по Z, торцы — ±X и ±Y.
    private static readonly Vector3Int Sheet = new Vector3Int(800, 400, 18);

    /// <summary>Все четыре торца листа толщиной по Z: грани ±X и ±Y.</summary>
    private const int EndsAcrossZ = 0b001111;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<Mesh> _meshes = new List<Mesh>();

    private Mesh Build(Vector3Int dims, int bareFaceMask,
        out GrooveMesh.SubmeshLayout layout, IReadOnlyList<GrooveSpec>? grooves = null,
        int holeAxis = 2, IReadOnlyList<GrooveMesh.Rect2>? holes = null)
    {
        var mesh = GrooveMesh.Build(dims, grooves, holes, holeAxis, bareFaceMask, out layout);
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
        PartRegistry.Clear();
        MaterialManager.ClearCache();
        MaterialCatalog.Reset();
    }

    // --- Геометрия: какие грани уходят в подложку ---

    [Test]
    public void Build_WithoutBareFaces_KeepsSingleSubmesh()
    {
        var mesh = Build(Sheet, 0, out var layout);
        Assert.AreEqual(1, mesh.subMeshCount, "кромкованная со всех сторон — одна коробка одним декором");
        Assert.AreEqual(-1, layout.BareEnds);
        Assert.AreEqual(36, mesh.GetTriangles(0).Length);
    }

    [Test]
    public void Build_BareEnds_SplitsThemOffTheDecor()
    {
        var mesh = Build(Sheet, EndsAcrossZ, out var layout);

        Assert.AreEqual(2, mesh.subMeshCount);
        Assert.AreEqual(1, layout.BareEnds, "без пазов подложка — первый добавочный сабмеш");
        // 4 торца и 2 пласти, по 2 треугольника на грань.
        Assert.AreEqual(24, mesh.GetTriangles(layout.BareEnds).Length, "в подложке ровно 4 торца");
        Assert.AreEqual(12, mesh.GetTriangles(0).Length, "на декоре остались только 2 пласти");
    }

    [Test]
    public void Build_BareEnds_NeverTakeThePlanes()
    {
        var mesh = Build(Sheet, EndsAcrossZ, out var layout);

        // Толщина по Z: пласти смотрят вдоль Z, торцы — вдоль X и Y.
        Assert.AreEqual(0f, MaxNormalAlong(mesh, layout.BareEnds, Vector3.forward), 1e-3f,
            "пласть в подложку попасть не может");
        Assert.AreEqual(1f, MaxNormalAlong(mesh, 0, Vector3.forward), 1e-3f,
            "пласти остались на декоре");
    }

    [Test]
    public void Build_SingleBareFace_LeavesTheOppositeOneOnTheDecor()
    {
        // Главный случай: кромка есть спереди и нет сзади. Грань 0 = +X, 1 = −X.
        var mesh = Build(Sheet, 1 << 1, out var layout);

        Assert.AreEqual(6, mesh.GetTriangles(layout.BareEnds).Length, "подложка ровно на одной грани");
        Assert.AreEqual(30, mesh.GetTriangles(0).Length, "остальные пять граней — декор");

        // −X и только он: у всех треугольников подложки нормаль (−1, 0, 0).
        var normals = mesh.normals;
        foreach (int i in mesh.GetTriangles(layout.BareEnds))
            Assert.AreEqual(-1f, normals[i].x, 1e-3f, "подложка ушла не на ту грань");
    }

    [Test]
    public void Build_BareEnds_ThicknessAlongY_PicksXAndZFaces()
    {
        // Столешница-короб 1200×40×600: толщина лежит по Y, торцы — ±X и ±Z.
        var dims = new Vector3Int(1200, 40, 600);
        var mesh = Build(dims, 0b110011, out var layout);

        Assert.AreEqual(24, mesh.GetTriangles(layout.BareEnds).Length);
        Assert.AreEqual(0f, MaxNormalAlong(mesh, layout.BareEnds, Vector3.up), 1e-3f,
            "верх и низ столешницы — пласть, а не торец");
    }

    [Test]
    public void Build_BareEnds_SurvivesPermutedHoleAxis()
    {
        // Тот же короб, но с вырезом под мойку: меш строится канонически и
        // переставляется по осям — маска обязана переехать вместе с ним.
        var dims = new Vector3Int(1200, 40, 600);
        var holes = new List<GrooveMesh.Rect2>
        {
            new GrooveMesh.Rect2 { xMin = -0.2f, xMax = 0.2f, yMin = -0.2f, yMax = 0.2f },
        };
        var mesh = Build(dims, 0b110011, out var layout, holeAxis: 1, holes: holes);

        Assert.Greater(mesh.GetTriangles(layout.BareEnds).Length, 0);
        Assert.AreEqual(0f, MaxNormalAlong(mesh, layout.BareEnds, Vector3.up), 1e-3f,
            "после перестановки подложка не должна съехать на пласть");
    }

    [Test]
    public void Build_BareEndsWithGrooves_KeepsBothExtraSubmeshes()
    {
        var grooves = new List<GrooveSpec> { new GrooveSpec(GrooveKind.Blind, GrooveSide.Top) };
        var mesh = Build(Sheet, EndsAcrossZ, out var layout, grooves);

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

    // --- Маска: где кромки нет ---

    [Test]
    public void Mask_EdgeBandingOff_TakesAllFourEnds()
    {
        var e = MakePart(new Vector3Int(600, 18, 500));
        e.EdgeBandingEnabled = false;

        // Толщина по Y → торцы это ±X (0,1) и ±Z (4,5).
        Assert.AreEqual(0b110011, EdgeSubstrate.BareFaceMask(e, PartRegistry.GetAll()));
    }

    [Test]
    public void Mask_LonePart_HasEveryEndBanded()
    {
        // Деталь ни во что не упирается: все торцы открыты, кромка везде.
        var e = MakePart(new Vector3Int(600, 18, 500));

        Assert.AreEqual(0, EdgeSubstrate.BareFaceMask(e, PartRegistry.GetAll()));
    }

    [Test]
    public void Mask_EndAgainstANeighbour_LosesItsEdge()
    {
        // Полка 600×18×500 и боковина вплотную к её правому торцу: закрытый
        // торец кромки не получает — ни в спецификации, ни на экране.
        var shelf = MakePart(new Vector3Int(600, 18, 500));
        float toU = AppConstants.MM_TO_UNITS;
        var side = MakePart(new Vector3Int(18, 700, 500));
        side.transform.position = new Vector3((600 + 18) * 0.5f * toU, 0f, 0f);

        int mask = EdgeSubstrate.BareFaceMask(shelf, PartRegistry.GetAll());
        Assert.AreEqual(1 << 0, mask, "подложка ровно на правом торце (+X)");
    }

    [Test]
    public void Mask_NotASheet_HasNoEndsAtAll()
    {
        // Брусок: тонких сторон больше одной, торец не определён — кромковать
        // нечего, и подложке взяться неоткуда.
        var e = MakePart(new Vector3Int(40, 40, 400));
        e.EdgeBandingEnabled = false;

        Assert.AreEqual(0, EdgeSubstrate.BareFaceMask(e, PartRegistry.GetAll()));
        Assert.AreEqual(1, e.GetComponent<MeshRenderer>().sharedMaterials.Length);
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

    // --- Деталь целиком: маска правит сабмеши и материалы ---

    [Test]
    public void Part_BareEnds_GetTheSubstrateMaterial()
    {
        var e = MakePart(new Vector3Int(600, 18, 500));
        var renderer = e.GetComponent<MeshRenderer>();
        Assert.AreEqual(1, renderer.sharedMaterials.Length, "кромкованная деталь одноматериальная");

        EdgeSubstrate.Sync(e);
        Assert.AreEqual(1, renderer.sharedMaterials.Length, "одинокая деталь кромкуется целиком");

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

    // --- Настоящий проект ---

    [Test]
    public void RealScene_TopPanel_BareAgainstTheWall_BandedTowardsTheFacade()
    {
        // A12_upper_A_top из docs/example.save.json: задний торец упирается в
        // стену (перекрыт целиком → кромки нет → голая плита), передний открыт
        // под фасад (кромка есть → декор). Именно этот случай синтетика не
        // ловит: «перекрыт» тут не сосед-деталь, а СТЕНА, и увидеть торец можно —
        // стену сносит и разрез, и режим обзора.
        var guard = ProjectLoadStateGuard.Capture();
        try
        {
            var json = File.ReadAllText(
                Path.Combine(Application.dataPath, "../docs", "example.save.json"));
            var data = SaveLoadManager.Deserialize(json);
            var all = SaveLoadManager.RestoreScene(data!)
                .Select(g => g.GetComponent<KitchenElement>())
                .Where(e => e != null).ToList()!;

            var top = all.First(e => e!.PartName == "A12_upper_A_top");
            var layout = EdgeBanding.LayoutOf(top!.DimensionsMM);
            int mask = EdgeSubstrate.BareFaceMask(top, all);

            Assert.IsTrue(top.EdgeBandingEnabled, "кромкование у детали включено");
            Assert.AreNotEqual(0, mask & (1 << layout.FaceIndex(EdgeSide.L1)),
                "торец в стену кромки не получает — там должна быть голая плита");
            Assert.AreEqual(0, mask & (1 << layout.FaceIndex(EdgeSide.L2)),
                "торец под фасад кромкуется — там декор");
        }
        finally
        {
            guard.Restore();
            foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
                if (e != null) Object.DestroyImmediate(e.gameObject);
            PartRegistry.Clear();
            GroupManager.Clear();
            CommandStack.Clear();
        }
    }

    private KitchenElement MakePart(Vector3Int dims)
    {
        var go = ElementFactory.CreatePart(dims, $"Part{_spawned.Count}", Vector3.zero);
        _spawned.Add(go);
        var e = go.GetComponent<KitchenElement>();
        e.DimensionsMM = dims;
        e.ApplyDimensions();
        return e;
    }
}
