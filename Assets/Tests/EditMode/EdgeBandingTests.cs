using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>Кромкование торцов: какие детали его поддерживают, как считается
/// перекрытие торца соседями, колонки кромок в CSV и ошибка о частичном
/// перекрытии.</summary>
public class EdgeBandingTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    // Полка 800×18×400: тонкая ось — Y, длина L = 800 (X), ширина W = 400 (Z).
    private static readonly Vector3Int ShelfDims = new Vector3Int(800, 18, 400);

    private KitchenElement CreatePart(string name, Vector3Int dims, Vector3 pos = default)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = dims;
        go.transform.position = pos;
        return element;
    }

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        PartRegistry.Clear();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    // ── Кто поддерживает кромкование ───────────────────────────────

    [Test]
    public void Sheet_WithExactlyOneThinSide_SupportsEdges()
    {
        var part = CreatePart("Board", ShelfDims);
        Assert.IsTrue(part.SupportsEdges);
        Assert.IsTrue(part.EdgeBandingEnabled, "по умолчанию кромкование включено");
        Assert.AreEqual(AppConstants.EDGE_THICKNESS_DEFAULT_MM, part.EdgeThicknessMM, 1e-4f);
    }

    [Test]
    public void Bar_WithTwoThinSides_DoesNotSupportEdges()
    {
        // Брусок 18×18×800: где у него торец под кромку — не определено.
        var part = CreatePart("Bar", new Vector3Int(18, 18, 800));
        Assert.IsFalse(part.SupportsEdges);
        Assert.IsFalse(part.EdgeBandingEnabled);
    }

    [Test]
    public void ThickBlock_WithNoThinSide_DoesNotSupportEdges()
    {
        var part = CreatePart("Block", new Vector3Int(800, 400, 60));
        Assert.IsFalse(part.SupportsEdges);
    }

    [Test]
    public void Facade_DoesNotSupportEdges()
    {
        var go = new GameObject("Facade");
        _spawned.Add(go);
        var facade = go.AddComponent<FacadeElement>();
        facade.DimensionsMM = new Vector3Int(600, 700, 18);
        Assert.IsFalse(facade.SupportsEdges);
    }

    // ── Разбор габарита на L/W и грани ─────────────────────────────

    [Test]
    public void LayoutOf_SplitsIntoLengthWidthThickness()
    {
        var layout = EdgeBanding.LayoutOf(ShelfDims);

        Assert.IsTrue(layout.IsValid);
        Assert.AreEqual(800, layout.LengthMM);
        Assert.AreEqual(400, layout.WidthMM);
        Assert.AreEqual(18, layout.ThicknessMM);
        Assert.AreEqual(1, layout.ThicknessAxis, "тонкая ось — Y");
        Assert.AreEqual(0, layout.LengthAxis);
        Assert.AreEqual(2, layout.WidthAxis);
    }

    [Test]
    public void FaceIndex_LSidesAreFacesOfLengthTimesThickness()
    {
        var layout = EdgeBanding.LayoutOf(ShelfDims);
        var part = CreatePart("Board", ShelfDims);
        var faces = part.GetFaces();

        // Полоса кромки L лежит на грани 800×18, полоса W — на грани 400×18.
        var l1 = faces[layout.FaceIndex(EdgeSide.L1)];
        Assert.AreEqual(0.8f, Mathf.Max(l1.size.x, l1.size.y), 1e-4f);
        Assert.AreEqual(0.018f, Mathf.Min(l1.size.x, l1.size.y), 1e-4f);

        var w1 = faces[layout.FaceIndex(EdgeSide.W1)];
        Assert.AreEqual(0.4f, Mathf.Max(w1.size.x, w1.size.y), 1e-4f);
        Assert.AreEqual(0.018f, Mathf.Min(w1.size.x, w1.size.y), 1e-4f);
    }

    [Test]
    public void LayoutOf_SquareSheet_IsDeterministic()
    {
        var layout = EdgeBanding.LayoutOf(new Vector3Int(400, 400, 18));
        Assert.IsTrue(layout.IsValid);
        Assert.AreEqual(0, layout.LengthAxis, "при равных сторонах длинная — ось с меньшим индексом");
        Assert.AreEqual(1, layout.WidthAxis);
    }

    // ── Перекрытие торцов ──────────────────────────────────────────

    /// <summary>Стойка, приставленная к торцу W1 полки (грань +X).</summary>
    private KitchenElement SidePanelAtW1(int depthMM, float zOffset = 0f)
    {
        // Толщина полки 18 → её грань +X стоит на x = 0.4; стойка 18 мм
        // становится центром на 0.4 + 0.009.
        return CreatePart($"Side{depthMM}", new Vector3Int(18, 700, depthMM),
            new Vector3(0.409f, 0f, zOffset));
    }

    [Test]
    public void LonePart_AllFourEndsAreOpen()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var coverage = EdgeBanding.Coverage(shelf, new List<KitchenElement> { shelf });

        foreach (EdgeSide side in System.Enum.GetValues(typeof(EdgeSide)))
        {
            Assert.IsTrue(coverage.HasEdge(side), $"{side}: торец открыт — кромка есть");
            Assert.IsFalse(coverage.IsPartial(side));
        }
    }

    [Test]
    public void EndCoveredByNeighbour_HasNoEdge()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var side = SidePanelAtW1(400);

        var coverage = EdgeBanding.Coverage(shelf, new List<KitchenElement> { shelf, side });

        Assert.IsFalse(coverage.HasEdge(EdgeSide.W1), "торец упирается в стойку — кромки нет");
        Assert.IsFalse(coverage.IsPartial(EdgeSide.W1), "перекрыт целиком — это не ошибка");
        Assert.IsTrue(coverage.HasEdge(EdgeSide.W2));
        Assert.IsTrue(coverage.HasEdge(EdgeSide.L1));
        Assert.IsTrue(coverage.HasEdge(EdgeSide.L2));
    }

    [Test]
    public void EndCoveredPartially_KeepsEdgeAndIsReportedAsPartial()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var side = SidePanelAtW1(200, zOffset: 0.1f); // половина глубины торца

        var coverage = EdgeBanding.Coverage(shelf, new List<KitchenElement> { shelf, side });

        Assert.IsTrue(coverage.HasEdge(EdgeSide.W1), "торец открыт наполовину — кромка нужна");
        Assert.IsTrue(coverage.IsPartial(EdgeSide.W1));
        Assert.AreEqual(0.5f, coverage.Ratio(EdgeSide.W1), 0.01f);
    }

    /// <summary>Ошибка EDG-01 обязана называть ВИНОВНИКА и подсвечивать его:
    /// без второй детали непонятно, что именно наезжает на кромку. Соседей может
    /// быть несколько — берём того, кто закрыл торец большей площадью.</summary>
    [Test]
    public void PartialCover_ReportsBiggestCovererAsSecondary()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var small = SidePanelAtW1(60, zOffset: 0.17f);   // узкий сосед
        var big = SidePanelAtW1(160, zOffset: -0.12f);   // широкий сосед
        foreach (var el in new[] { shelf, small, big }) PartRegistry.Register(el);

        var issues = SceneAnalyzer.Analyze();
        var edge = issues.Find(i => i.Code == IssueCatalog.CodeEdgePartialCover
                                    && i.Target == shelf);

        Assert.AreNotEqual(default(AnalysisIssue), edge, "торец перекрыт частично — ждём EDG-01");
        Assert.AreSame(big, edge.Secondary, "виновник — сосед с наибольшей площадью перекрытия");
        StringAssert.Contains(big.PartName, edge.Detail, "пара в колонке «Деталь» — как у GAP-01");
        StringAssert.Contains(shelf.PartName, edge.Detail);
    }

    [Test]
    public void EndCoveredByTwoNeighbours_CountsAsFullyCovered()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var lower = SidePanelAtW1(200, zOffset: -0.1f);
        var upper = SidePanelAtW1(200, zOffset: 0.1f);

        var coverage = EdgeBanding.Coverage(shelf,
            new List<KitchenElement> { shelf, lower, upper });

        Assert.IsFalse(coverage.HasEdge(EdgeSide.W1), "две детали закрывают торец целиком");
        Assert.IsFalse(coverage.IsPartial(EdgeSide.W1));
    }

    [Test]
    public void EndAgainstWall_HasNoEdge()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var wallPart = CreatePart("Wall", new Vector3Int(100, 2500, 3000),
            new Vector3(0.45f, 0f, 0f));
        wallPart.gameObject.AddComponent<Wall>();

        var coverage = EdgeBanding.Coverage(shelf, new List<KitchenElement> { shelf, wallPart });

        Assert.IsFalse(coverage.HasEdge(EdgeSide.W1), "торец у стены — кромки нет");
    }

    [Test]
    public void NeighbourWithGap_LeavesEdge()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        // Стойка отодвинута на 5 мм — это уже не касание.
        CreatePart("Side", new Vector3Int(18, 700, 400), new Vector3(0.414f, 0f, 0f));

        var coverage = EdgeBanding.Coverage(shelf, PartRegistryList());

        Assert.IsTrue(coverage.HasEdge(EdgeSide.W1), "зазор 5 мм — торец открыт");
    }

    private List<KitchenElement> PartRegistryList()
    {
        var list = new List<KitchenElement>();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var e = go.GetComponent<KitchenElement>();
            if (e != null) list.Add(e);
        }
        return list;
    }

    // ── Спецификация и CSV ─────────────────────────────────────────

    private static string[] CsvRow(SpecResult result, int index)
    {
        var rows = SpecificationExport.ToCsv(result).Replace("\r\n", "\n").Trim().Split('\n');
        return rows[index].Split(';');
    }

    private static int Column(SpecResult result, string header)
    {
        int col = System.Array.IndexOf(CsvRow(result, 0), header);
        Assert.AreNotEqual(-1, col, $"в шапке есть колонка {header}");
        return col;
    }

    [Test]
    public void ToCsv_OpenEndsCarryThicknessCoveredOnesAreEmpty()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        shelf.EdgeThicknessMM = 0.5f;
        var side = SidePanelAtW1(400);
        side.EdgeBandingEnabled = false; // стойка в этом тесте не интересна

        var result = SpecificationManager.Build(new List<KitchenElement> { shelf, side });
        var row = CsvRow(result, 1);

        Assert.AreEqual("0.5", row[Column(result, "Кромка L1")]);
        Assert.AreEqual("0.5", row[Column(result, "Кромка L2")]);
        Assert.AreEqual("", row[Column(result, "Кромка W1")], "перекрытый торец — пустая колонка, не ноль");
        Assert.AreEqual("0.5", row[Column(result, "Кромка W2")]);
    }

    [Test]
    public void ToCsv_EdgeBandingOff_LeavesAllFourColumnsEmpty()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        shelf.EdgeBandingEnabled = false;

        var result = SpecificationManager.Build(new List<KitchenElement> { shelf });
        var row = CsvRow(result, 1);

        Assert.AreEqual("", row[Column(result, "Кромка L1")]);
        Assert.AreEqual("", row[Column(result, "Кромка L2")]);
        Assert.AreEqual("", row[Column(result, "Кромка W1")]);
        Assert.AreEqual("", row[Column(result, "Кромка W2")]);
    }

    [Test]
    public void ToCsv_TotalRowHasSameColumnCountAsHeader()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var result = SpecificationManager.Build(new List<KitchenElement> { shelf });
        var rows = SpecificationExport.ToCsv(result).Replace("\r\n", "\n").Trim().Split('\n');

        Assert.AreEqual(rows[0].Split(';').Length, rows[rows.Length - 1].Split(';').Length);
    }

    [Test]
    public void Build_PartsWithDifferentEdges_AreSeparateLines()
    {
        // Одинаковые полки, но у одной торец W1 закрыт стойкой — это разные
        // позиции раскроя.
        var open = CreatePart("ShelfA", ShelfDims, new Vector3(0f, 1f, 0f));
        var closed = CreatePart("ShelfB", ShelfDims);
        var side = SidePanelAtW1(400);
        side.EdgeBandingEnabled = false;

        var result = SpecificationManager.Build(
            new List<KitchenElement> { open, closed, side });

        Assert.AreEqual(3, result.lines.Count);
    }

    // ── Валидация ──────────────────────────────────────────────────

    [Test]
    public void Analyze_PartiallyCoveredEnd_ReportsError()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var side = SidePanelAtW1(200, zOffset: 0.1f);
        PartRegistry.Register(shelf);
        PartRegistry.Register(side);

        var issues = SceneAnalyzer.Analyze()
            .FindAll(i => i.Code == IssueCatalog.CodeEdgePartialCover && i.Target == shelf);

        Assert.IsNotEmpty(issues, "частично перекрытый торец даёт EDG-01");
        Assert.AreEqual(IssueLevel.Error, issues[0].Level);
        StringAssert.Contains("W1", issues[0].Message);
    }

    [Test]
    public void Analyze_SkipValidationFlag_SuppressesError()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        shelf.EdgeSkipValidation = true;
        var side = SidePanelAtW1(200, zOffset: 0.1f);
        PartRegistry.Register(shelf);
        PartRegistry.Register(side);

        var issues = SceneAnalyzer.Analyze();

        Assert.IsFalse(issues.Exists(i => i.Code == IssueCatalog.CodeEdgePartialCover
                                          && i.Target == shelf));
    }

    [Test]
    public void Analyze_FullyCoveredEnd_IsNotAnError()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var side = SidePanelAtW1(400);
        PartRegistry.Register(shelf);
        PartRegistry.Register(side);

        var issues = SceneAnalyzer.Analyze();

        Assert.IsFalse(issues.Exists(i => i.Code == IssueCatalog.CodeEdgePartialCover
                                          && i.Target == shelf));
    }

    // ── Сериализация и откат ───────────────────────────────────────

    [Test]
    public void ElementData_RoundTripsEdgeSettings()
    {
        var part = CreatePart("Board", ShelfDims);
        part.EdgeBandingEnabled = false;
        part.EdgeThicknessMM = 2.0f;
        part.EdgeSkipValidation = true;

        var json = JsonUtility.ToJson(ElementData.FromElement(part));
        var restored = JsonUtility.FromJson<ElementData>(json);

        Assert.IsFalse(restored.edgeBanding);
        Assert.AreEqual(2.0f, restored.edgeThicknessMM, 1e-4f);
        Assert.IsTrue(restored.edgeSkipValidation);
    }

    [Test]
    public void ElementData_LegacyFileWithoutEdges_KeepsBandingOn()
    {
        var legacy = JsonUtility.FromJson<ElementData>(
            "{\"name\":\"Board\",\"dimensionsMM\":[800,400,18]}");

        Assert.IsTrue(legacy.edgeBanding, "старый проект не теряет кромки");
        Assert.AreEqual(AppConstants.EDGE_THICKNESS_DEFAULT_MM, legacy.edgeThicknessMM, 1e-4f);
        Assert.IsFalse(legacy.edgeSkipValidation);
    }

    [Test]
    public void SetEdgeBandingCommand_UndoRestoresAllThreeFields()
    {
        var part = CreatePart("Board", ShelfDims);
        var before = EdgeBandingState.Of(part);
        var after = new EdgeBandingState(false, 2.0f, true);

        var command = new SetEdgeBandingCommand(part, before, after);
        command.Execute();
        Assert.IsFalse(part.EdgeBandingEnabled);
        Assert.AreEqual(2.0f, part.EdgeThicknessMM, 1e-4f);
        Assert.IsTrue(part.EdgeSkipValidation);

        command.Undo();
        Assert.IsTrue(part.EdgeBandingEnabled);
        Assert.AreEqual(AppConstants.EDGE_THICKNESS_DEFAULT_MM, part.EdgeThicknessMM, 1e-4f);
        Assert.IsFalse(part.EdgeSkipValidation);
    }

    [Test]
    public void EdgeThickness_IsClampedToTapeRange()
    {
        var part = CreatePart("Board", ShelfDims);

        part.EdgeThicknessMM = 100f;
        Assert.AreEqual(AppConstants.EDGE_THICKNESS_MAX_MM, part.EdgeThicknessMM, 1e-4f);

        part.EdgeThicknessMM = -1f;
        Assert.AreEqual(AppConstants.EDGE_THICKNESS_MIN_MM, part.EdgeThicknessMM, 1e-4f);
    }

    [Test]
    public void FormatThickness_UsesDotAndOneDecimal()
    {
        Assert.AreEqual("0.5", EdgeBanding.FormatThickness(0.5f));
        Assert.AreEqual("1.0", EdgeBanding.FormatThickness(1f));
        Assert.AreEqual("2.0", EdgeBanding.FormatThickness(1.96f));
    }
}
