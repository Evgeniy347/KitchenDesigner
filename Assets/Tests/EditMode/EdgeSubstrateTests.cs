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
    public void Substrate_ByDefault_IsPlainWhite()
    {
        // Белый по умолчанию — сознательное решение: фотографическая текстура
        // плиты по тону сливалась и с бежевым декором, и с валидационной
        // заливкой, и торец перестал читаться как торец. В каталоге проекта
        // декора с именем DecorName нет — и быть не должно.
        Assert.IsNull(EdgeSubstrate.Decor(),
            $"в каталоге завёлся декор «{EdgeSubstrate.DecorName}» — торцы перестали быть белыми");

        var mat = EdgeSubstrate.Material();
        Assert.IsNotNull(mat);
        Assert.AreEqual(Color.white, mat!.color);
        Assert.IsNull(mat.mainTexture, "белая подложка картинки не носит");
    }

    [Test]
    public void Substrate_CatalogDecorWithTheName_OverridesWhite()
    {
        // Точка расширения: завёл декор с этим именем — подложкой стал он.
        // Так же включается назад картинка «МДФ шлифованная».
        var custom = new MaterialDef("edge_substrate_test", EdgeSubstrate.DecorName,
            "МДФ", new Color(0.8f, 0.7f, 0.6f));
        MaterialCatalog.Load(new[]
        {
            new MaterialDef(MaterialCatalog.DefaultId, "Серый", "ЛДСП", Color.gray),
            custom,
        });

        Assert.AreEqual(custom, EdgeSubstrate.Decor());
        Assert.AreEqual(MaterialManager.GetSharedMaterial(custom), EdgeSubstrate.Material(),
            "подложка обязана делить материал с декором — иначе лишний батч на деталь");
    }

    [Test]
    public void Substrate_SandedMdfDecor_StaysInTheCatalogAsAnOrdinaryOne()
    {
        // Картинку никто не выбрасывал: она осталась обычным декором, её можно
        // назначить детали руками и ею же переопределить подложку.
        Assert.AreEqual("МДФ шлифованная", MaterialCatalog.Get("mdf_shlifovannaya").displayName);
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

    [Test]
    public void Part_WithoutOwnDecor_KeepsTheSubstrateUnderTheValidationTint()
    {
        // Деталь на декоре «default» (так стоит Countertop_A в проекте) заливалась
        // валидационной тонировкой ЦЕЛИКОМ, вместе с сабмешем торцов, — голой
        // плиты не было видно никогда. Тонировка сообщает «деталь в порядке», а
        // не «деталь такого цвета», поэтому торец из-под неё обязан остаться.
        var e = MakePart(new Vector3Int(600, 18, 500));
        e.EdgeBandingEnabled = false;
        Assert.IsFalse(MaterialManager.HasCustomDecor(e), "декор у детали — дефолтный");

        var host = new GameObject("Highlighter");
        _spawned.Add(host);
        var highlighter = host.AddComponent<ElementHighlighter>();
        highlighter.CreateMaterials();
        ElementHighlighter.TintEnabled = true;

        highlighter.ApplyForElement(e);

        var mats = e.GetComponent<MeshRenderer>().sharedMaterials;
        Assert.AreEqual(2, mats.Length, "сабмеш торцов не должен исчезнуть под тонировкой");
        Assert.AreEqual(EdgeSubstrate.Material(), mats[1], "торец остался подложкой");
        Assert.AreNotEqual(mats[0], mats[1], "тело затонировано, торец нет");
    }

    // --- Настоящий проект ---

    [Test]
    public void RealScene_TopPanel_BareAgainstTheWall_BandedTowardsTheFacade()
    {
        // A12_upper_A_top из ЗАМОРОЖЕННОЙ Fixtures/pipe-gap-scene.save.json (не
        // из живого docs/example.save.json — тот меняется под пользователем,
        // agents/TESTS.md → «docs/example.save.json — NEVER TOUCH IT»): задний
        // торец упирается в стену (перекрыт целиком → кромки нет → голая
        // плита), передний открыт под фасад (кромка есть → декор). Именно этот
        // случай синтетика не ловит: «перекрыт» тут не сосед-деталь, а СТЕНА, и
        // увидеть торец можно — стену сносит и разрез, и режим обзора.
        var guard = ProjectLoadStateGuard.Capture();
        try
        {
            var json = File.ReadAllText(
                Path.Combine(Application.dataPath, "Tests/EditMode/Fixtures", "pipe-gap-scene.save.json"));
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

    // --- Подложка и спецификация отвечают ОДНО И ТО ЖЕ ---

    [Test]
    public void Substrate_AndTheSpecificationColumns_AgreeOnEverySide()
    {
        // Подложка рисуется ровно там, где в CSV пустая колонка кромки. Это не
        // совпадение и не удобство: расходимся с раскроем — расходимся с
        // реальностью, потому что по этому же CSV деталь и кромкуют на
        // производстве. Два вызывающих (спецификация и подложка) обязаны
        // спрашивать EdgeBanding.Coverage и получать один ответ.
        var shelf = MakePart(new Vector3Int(600, 18, 500));
        float toU = AppConstants.MM_TO_UNITS;
        var side = MakePart(new Vector3Int(18, 700, 500));
        side.transform.position = new Vector3((600 + 18) * 0.5f * toU, 0f, 0f);

        var all = PartRegistry.GetAll();
        var layout = EdgeBanding.LayoutOf(shelf.DimensionsMM);
        int mask = EdgeSubstrate.BareFaceMask(shelf, all);
        var columns = EdgeColumns.For(shelf, all);

        var pairs = new (EdgeSide side, string column)[]
        {
            (EdgeSide.L1, columns.l1), (EdgeSide.L2, columns.l2),
            (EdgeSide.W1, columns.w1), (EdgeSide.W2, columns.w2),
        };

        bool sawBoth = false;
        foreach (var (edge, column) in pairs)
        {
            bool bare = (mask & (1 << layout.FaceIndex(edge))) != 0;
            Assert.AreEqual(string.IsNullOrEmpty(column), bare,
                $"{edge}: в спецификации кромка «{column}», а подложка говорит "
                + (bare ? "«кромки нет»" : "«кромка есть»")
                + ". Экран и раскрой обязаны сходиться до торца");
            sawBoth |= bare;
        }

        Assert.IsTrue(sawBoth,
            "сцена вырождена: ни один торец не оказался закрытым, и проверка "
            + "сравнивала бы только пустые случаи");
    }

    [Test]
    public void SuppressedSide_GivesTheSpecificationOneEdgeLess()
    {
        // Красная сторона — это ЯВНОЕ «кромки нет». Проверяется по числу
        // непустых колонок, а не по конкретной стороне: колонка исчезает ровно
        // одна, соседние не задеты. Тот же ответ обязаны дать подложка в 3D и
        // поле edges в MCP — за это отвечает единственный читатель
        // EdgeBanding.HasEdgeEffective и EdgeStateSingleReaderTests.
        var shelf = MakePart(new Vector3Int(600, 18, 500));
        var all = PartRegistry.GetAll();

        int before = NonEmptyEdgeColumns(EdgeColumns.For(shelf, all));
        Assume.That(before, Is.EqualTo(4), "полка стоит одна — кромка на всех четырёх торцах");

        shelf.SetEdgeState(EdgeSide.W1, EdgeSideState.Suppressed);

        Assert.AreEqual(before - 1, NonEmptyEdgeColumns(EdgeColumns.For(shelf, all)),
            "«убрать» на открытом торце обязано убрать кромку из раскроя — иначе красный "
            + "цвет в панели не значит ничего");
        Assert.AreEqual(0, EdgeSubstrate.BareFaceMask(shelf, all)
                & ~(1 << EdgeBanding.LayoutOf(shelf.DimensionsMM).FaceIndex(EdgeSide.W1)),
            "подложка появилась ровно на том же торце и больше нигде");
    }

    [Test]
    public void ForcedSide_KeepsTheEdgeOnAnEndTheSceneCovers()
    {
        // Обратное решение: торец закрыт соседом, а человек говорит «кромка
        // здесь есть». Раскрой обязан его услышать — ради этого состояние и
        // называется присутствием кромки, а не «пропустить проверку».
        var shelf = MakePart(new Vector3Int(600, 18, 500));
        float toU = AppConstants.MM_TO_UNITS;
        var side = MakePart(new Vector3Int(18, 700, 500));
        side.transform.position = new Vector3((600 + 18) * 0.5f * toU, 0f, 0f);
        var all = PartRegistry.GetAll();

        var layout = EdgeBanding.LayoutOf(shelf.DimensionsMM);
        var covered = EdgeStates.All.First(sd =>
            (EdgeSubstrate.BareFaceMask(shelf, all) & (1 << layout.FaceIndex(sd))) != 0);

        int before = NonEmptyEdgeColumns(EdgeColumns.For(shelf, all));
        shelf.SetEdgeState(covered, EdgeSideState.Forced);

        Assert.AreEqual(before + 1, NonEmptyEdgeColumns(EdgeColumns.For(shelf, all)),
            $"{covered}: «принудительно есть» добавляет кромку в раскрой на закрытом торце");
        Assert.AreEqual(0, EdgeSubstrate.BareFaceMask(shelf, all) & (1 << layout.FaceIndex(covered)),
            "и подложка с этого торца уходит: 3D и раскрой говорят одно и то же");
    }

    private static int NonEmptyEdgeColumns(EdgeColumns c)
    {
        int n = 0;
        foreach (var column in new[] { c.l1, c.l2, c.w1, c.w2 })
            if (!string.IsNullOrEmpty(column)) n++;
        return n;
    }

    [Test]
    public void Mask_EdgeBandingOff_NeedsNoNeighbours()
    {
        // Выключатель снят — кромки нет ни на одном торце, и спрашивать соседей
        // не о чем. Это самый частый случай на загрузке проекта: если бы ответ
        // требовал слепка сцены, каждая деталь платила бы за расчёт перекрытий
        // там, где он ничего не решает.
        var e = MakePart(new Vector3Int(600, 18, 500));
        e.EdgeBandingEnabled = false;

        Assert.AreEqual(0b110011, EdgeSubstrate.BareFaceMask(e, (SceneFaces?)null),
            "без сцены ответ обязан остаться тем же: все четыре торца голые");
    }

    [Test]
    public void SyncScene_RepeatedOnTheSameScene_DoesNotRebuildTheMesh()
    {
        // Слепок граней снимается ДО пересборки мешей и переживает её: пересборка
        // меняет сабмеши, а не габаритные грани, по которым считаются перекрытия.
        // Меш пересобирается только там, где маска реально изменилась, — иначе
        // проход по устоявшейся сцене перестраивал бы всю геометрию каждый раз.
        var shelf = MakePart(new Vector3Int(600, 18, 500));
        float toU = AppConstants.MM_TO_UNITS;
        var side = MakePart(new Vector3Int(18, 700, 500));
        side.transform.position = new Vector3((600 + 18) * 0.5f * toU, 0f, 0f);

        var all = PartRegistry.GetAll();
        EdgeSubstrate.SyncScene(all);
        int maskAfterFirst = EdgeSubstrate.BareFaceMask(shelf, all);
        int meshAfterFirst = shelf.GetComponent<MeshFilter>().sharedMesh.GetInstanceID();

        EdgeSubstrate.SyncScene(all);

        Assert.AreEqual(maskAfterFirst, EdgeSubstrate.BareFaceMask(shelf, all),
            "второй проход по той же сцене обязан дать ту же маску");
        Assert.AreEqual(meshAfterFirst,
            shelf.GetComponent<MeshFilter>().sharedMesh.GetInstanceID(),
            "маска не изменилась — меш пересобирать нечего. Пересборка на каждом "
            + "проходе стоила бы полной перестройки геометрии сцены на каждую "
            + "перекраску валидации");
    }

    [Test]
    public void SyncScene_AfterTheNeighbourIsGone_UncoversTheEnd()
    {
        // Перекрытие торца зависит от СОСЕДЕЙ: сама деталь не узнаёт, что сосед
        // уехал или удалён. Поэтому подложку пересчитывает проход по всей сцене
        // — оттуда же, откуда перекрашивается валидация.
        var shelf = MakePart(new Vector3Int(600, 18, 500));
        float toU = AppConstants.MM_TO_UNITS;
        var side = MakePart(new Vector3Int(18, 700, 500));
        side.transform.position = new Vector3((600 + 18) * 0.5f * toU, 0f, 0f);

        EdgeSubstrate.SyncScene(PartRegistry.GetAll());
        var layout = EdgeBanding.LayoutOf(shelf.DimensionsMM);
        int covered = EdgeSubstrate.BareFaceMask(shelf, PartRegistry.GetAll());
        Assert.AreNotEqual(0, covered, "торец в боковину кромки не получает");

        side.transform.position = new Vector3(3f, 0f, 0f);
        EdgeSubstrate.SyncScene(PartRegistry.GetAll());

        Assert.AreEqual(0, EdgeSubstrate.BareFaceMask(shelf, PartRegistry.GetAll()),
            "сосед уехал — торец открылся и снова кромкуется, а голая плита "
            + "должна исчезнуть. Деталь об отъезде соседа не узнаёт сама");
        Assert.AreEqual(1, shelf.GetComponent<MeshRenderer>().sharedMaterials.Length,
            "сабмеш торцов ушёл вместе с маской");
    }

    [Test]
    public void Substrate_DecorName_IsMatchedByDisplayName_NotById()
    {
        // Декор-подложку заводит ЧЕЛОВЕК записью в index.json, и видит он там
        // имя, а не id. Имя набирают руками, поэтому регистр и краевые пробелы
        // значения не имеют — а вот id с таким текстом подложкой не делает.
        var byId = new MaterialDef(EdgeSubstrate.DecorName, "Совсем другой декор",
            "МДФ", Color.red);
        var byName = new MaterialDef("substrate_by_name", "  подложка ТОРЦА ",
            "МДФ", new Color(0.8f, 0.7f, 0.6f));
        MaterialCatalog.Load(new[]
        {
            new MaterialDef(MaterialCatalog.DefaultId, "Серый", "ЛДСП", Color.gray),
            byId, byName,
        });

        Assert.AreEqual(byName, EdgeSubstrate.Decor(),
            "совпадение по displayName без учёта регистра и краевых пробелов");
        Assert.AreNotEqual(byId, EdgeSubstrate.Decor(),
            "id с тем же текстом подложкой не делает: точка стыка с человеком — "
            + "то, что он видит в списке декоров");
    }

    [Test]
    public void Substrate_White_IsOneMatteMaterialForTheWholeScene()
    {
        Assume.That(EdgeSubstrate.Decor(), Is.Null, "подложка сейчас белая");

        var first = EdgeSubstrate.Material();
        var second = EdgeSubstrate.Material();

        Assert.IsNotNull(first);
        Assert.AreSame(first, second,
            "белая подложка — один материал на всю сцену. Свой на деталь означал "
            + "бы лишний батч у каждой некромкованной детали");
        Assert.Less(first!.GetFloat("_Smoothness"), 0.2f,
            "голая плита матовая: глянцевый торец на матовом щите читается как "
            + "накладка, а не как срез плиты");
    }

    [Test]
    public void Apply_OnAPartWithASubstrate_ChangesOnlyTheDecorSubmesh()
    {
        // У детали с голыми торцами два сабмеша: декор и подложка. Декор — это
        // сабмеш 0, и назначение нового декора обязано оставить всё остальное
        // на месте: паз, некромкованный торец, фрезеровки сборного фасада.
        var e = MakePart(new Vector3Int(600, 18, 500));
        e.EdgeBandingEnabled = false;
        var renderer = e.GetComponent<MeshRenderer>();
        Assume.That(renderer.sharedMaterials.Length, Is.EqualTo(2));

        var def = new MaterialDef("apply_over_substrate", "Дуб", "ЛДСП", Color.white, null, 800)
        {
            tileHeightMM = 800,
        };
        MaterialCatalog.Register(def);
        MaterialManager.Apply(e, def);

        var mats = renderer.sharedMaterials;
        Assert.AreEqual(2, mats.Length, "сабмеш торцов не должен исчезнуть под декором");
        Assert.AreEqual(MaterialManager.GetSharedMaterial(def), mats[0], "декор — сабмеш 0");
        Assert.AreEqual(EdgeSubstrate.Material(), mats[1],
            "служебные сабмеши детали живут своей жизнью: назначить декор — "
            + "не значит перекрасить ими торец и паз");
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
