using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>Сенсор на ЖИВОМ файле пользователя <c>docs/example.save.json</c> —
/// парный к <see cref="SaveValidationTests"/> (та же геометрия, та же
/// <see cref="SceneAnalyzer"/>), но ничего не утверждает: печатает найденное
/// через <c>TestContext.WriteLine</c> и оставляет полный список в
/// <c>test-results/save-validation-sensor.log</c>.
///
/// Живой файл переписывается автосохранением десктопа и правится пользователем
/// каждый день (agents/TESTS.md → «docs/example.save.json — NEVER TOUCH IT»),
/// так что assert на нём красит сборку чужой работой, а не багом кода — это и
/// произошло с <c>Analyze_ExampleSave_ReportsNoIssues</c> и
/// <c>Geometry_ExampleSave_NoSubToleranceJoints</c>, отсюда и разделение (тот
/// же приём, каким уже разведён <see cref="PipeGapSensorTests"/>: критерий
/// приёмки — на замороженной фикстуре в <see cref="SaveValidationTests"/>, живой
/// файл — только здесь, как наблюдение).
///
/// Тест НЕ падает даже если находок стало больше: значение этого класса — дать
/// человеку прочитать актуальную сводку, а не гейтить CI по чужим правкам сцены.</summary>
public class SaveValidationSensorTests
{
    private const string SaveFileName = "example.save.json";
    private const string ReportFileName = "save-validation-sensor.log";

    private string _json = "";
    private string _reportPath = "";
    private ProjectLoadStateGuard? _guard;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "../docs", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        _json = File.ReadAllText(fullPath);
        Assert.IsNotEmpty(_json);

        var dir = Path.Combine(Application.dataPath, "../test-results");
        Directory.CreateDirectory(dir);
        _reportPath = Path.Combine(dir, ReportFileName);
        File.WriteAllText(_reportPath, $"# Сенсор {SaveFileName} (живой файл, не критерий приёмки)\n");
    }

    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);

        _guard = ProjectLoadStateGuard.Capture();

        s.AutoSave = false;
        s.SpatialGrid = false;
        s.NormalView.edgeOutline = false;

        ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        ClearScene();
        _guard?.Restore();
        FaceCache.Clear();
    }

    private void ClearScene()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();

        ProjectInstructions.Reset();
        ProjectRooms.Reset();
        ProjectFloorplans.Reset();
    }

    private List<KitchenElement> RestoreScene()
    {
        var data = SaveLoadManager.Deserialize(_json);
        Assert.IsNotNull(data);
        var objs = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(objs);
        return objs.Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList()!;
    }

    private void Report(string section, IEnumerable<string> lines)
    {
        var all = new List<string> { "", $"## {section}" };
        all.AddRange(lines);
        File.AppendAllLines(_reportPath, all);
    }

    [Test]
    public void Analyze_LiveExampleSave_PrintsFindings()
    {
        RestoreScene();
        var issues = SceneAnalyzer.Analyze();

        var lines = issues
            .OrderBy(i => i.Code, System.StringComparer.Ordinal)
            .ThenBy(i => i.Detail, System.StringComparer.Ordinal)
            .Select(i => $"{i.Level,-7} {i.Code,-7} {i.Detail,-48} {i.Message}")
            .ToList();
        Report("Правила SceneAnalyzer", lines.Count > 0 ? lines : new List<string> { "чисто" });

        int errors = issues.Count(i => i.Level == IssueLevel.Error);
        int warnings = issues.Count(i => i.Level == IssueLevel.Warning);
        TestContext.WriteLine($"Проблемы в {SaveFileName}: {issues.Count} "
            + $"(errors={errors}, warnings={warnings}). Подробности: {_reportPath}");
    }

    [Test]
    public void Geometry_LiveExampleSave_PrintsSubToleranceJoints()
    {
        var elements = RestoreScene();
        float toMm = 1f / AppConstants.MM_TO_UNITS;
        float deadBand = Tolerance.SnapEpsilon;
        float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;

        var found = SubToleranceJointScanner.Find(elements, deadBand, contactDist);
        var report = found
            .OrderByDescending(j => Mathf.Abs(j.GapUnits))
            .Select(j => SubToleranceJointScanner.FormatLine(j, toMm))
            .ToList();
        Report("Подпороговые стыки (0.01 … 0.5 мм)",
            report.Count > 0 ? report : new List<string> { "чисто" });

        TestContext.WriteLine($"Стыков в слепой зоне валидатора: {found.Count}. "
            + $"Подробности: {_reportPath}");
    }
}
