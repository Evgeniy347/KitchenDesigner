using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>Контракт каталога проблем: набор кодов, уровень каждого кода и
/// колонка «Деталь». Эти сведения раньше были комментариями в
/// Assets/Scripts/Core/Analysis; здесь они закреплены числами и строками,
/// потому что на код завязан фильтр окна «Ошибки» и поле level в ответе MCP —
/// ошибка тут не падает, она молча меняет отчёт пользователю.</summary>
public class AnalysisContractTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private KitchenElement Board(string name)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = new Vector3Int(600, 400, 18);
        return el;
    }

    private static string[] DeclaredCodes() =>
        typeof(IssueCatalog)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, System.StringComparer.Ordinal)
            .ToArray();

    private List<AnalysisIssue> OneOfEveryIssue()
    {
        var a = Board("Detal-A");
        var b = Board("Detal-B");

        return new List<AnalysisIssue>
        {
            IssueCatalog.FromViolation(new ContactViolation(a, b, ViolationKind.Overlap)),
            IssueCatalog.FromViolation(new ContactViolation(a, null, ViolationKind.Unsupported)),
            IssueCatalog.FromViolation(new ContactViolation(a, null, ViolationKind.OutOfWallBounds)),
            IssueCatalog.FromViolation(new ContactViolation(a, null, (ViolationKind)999)),
            IssueCatalog.EdgePartialCover(a, "W1 50%", b),
            IssueCatalog.EdgePartialCover(a, "W1 50%"),
            IssueCatalog.NearContact(a, b, 1f),
            IssueCatalog.NearContactFar(a, b, 5f),
            IssueCatalog.DishwasherFacadeBackGap(a, b, 3f),
            IssueCatalog.PanelNotSeated(a, b, 1f, 8f),
            IssueCatalog.FacadeGap(a, "слева 0"),
            IssueCatalog.DrawerNoFacade(a),
            IssueCatalog.DrawerFacadeOrphaned(a, b),
            IssueCatalog.DishwasherNoFacade(a),
            IssueCatalog.DishwasherFacadeMissing(a, "Fasad"),
            IssueCatalog.DishwasherFacadeOrphaned(a, b),
            IssueCatalog.DishwasherNoSupport(a),
            IssueCatalog.DishwasherSunk(a, b, 40f),
            IssueCatalog.DishwasherFacadeHeight(a, b, 500),
            IssueCatalog.AttachDetached(a, b),
        };
    }

    [Test]
    public void IssueCatalog_CodeSet_IsStable_BecauseTheFilterAndTheReportAreKeyedOnIt()
    {
        var expected = new[]
        {
            "ATT-01",
            "COL-00", "COL-01", "COL-02", "COL-03",
            "DRW-01", "DRW-02",
            "DWH-01", "DWH-02", "DWH-03", "DWH-04", "DWH-05",
            "EDG-01",
            "FAC-01",
            "GAP-01", "GAP-02",
            "SEAT-01",
        };

        CollectionAssert.AreEqual(expected, DeclaredCodes(),
            "коды только ДОБАВЛЯЮТСЯ: на них завязан фильтр по кодам в окне «Ошибки» и поле "
            + "code в ответе MCP, поэтому переименование или удаление кода — молчаливая "
            + "поломка чужого фильтра, а не рефакторинг");
    }

    [Test]
    public void IssueCatalog_LevelOfEveryIssue_IsPinned_AndInfoStaysUnused()
    {
        var byCode = new Dictionary<string, IssueLevel>(System.StringComparer.Ordinal);
        foreach (var issue in OneOfEveryIssue())
        {
            if (byCode.TryGetValue(issue.Code, out var already))
            {
                Assert.AreEqual(already, issue.Level,
                    "один код — один уровень: " + issue.Code);
                continue;
            }
            byCode[issue.Code] = issue.Level;
        }

        var expected = new Dictionary<string, IssueLevel>(System.StringComparer.Ordinal)
        {
            ["COL-00"] = IssueLevel.Error,
            ["COL-01"] = IssueLevel.Error,
            ["COL-02"] = IssueLevel.Error,
            ["COL-03"] = IssueLevel.Error,
            ["EDG-01"] = IssueLevel.Error,
            ["ATT-01"] = IssueLevel.Error,
            ["DWH-05"] = IssueLevel.Error,
            ["GAP-01"] = IssueLevel.Warning,
            ["GAP-02"] = IssueLevel.Warning,
            ["SEAT-01"] = IssueLevel.Warning,
            ["FAC-01"] = IssueLevel.Warning,
            ["DRW-01"] = IssueLevel.Warning,
            ["DRW-02"] = IssueLevel.Warning,
            ["DWH-01"] = IssueLevel.Warning,
            ["DWH-02"] = IssueLevel.Warning,
            ["DWH-03"] = IssueLevel.Warning,
            ["DWH-04"] = IssueLevel.Warning,
        };

        CollectionAssert.AreEquivalent(expected.Keys, byCode.Keys,
            "каждый код каталога обязан быть в этой таблице: новый код без уровня — это "
            + "строка отчёта, про которую никто не решил, ошибка это или предупреждение");

        foreach (var pair in expected)
            Assert.AreEqual(pair.Value, byCode[pair.Key],
                pair.Key + ": Error — физически недопустимая геометрия, Warning — недоделанная "
                + "сборка, которую пользователь вправе доводить в любом порядке. DWH-05 "
                + "единственный DWH-код уровня Error именно поэтому");

        CollectionAssert.DoesNotContain(byCode.Values, IssueLevel.Info,
            "уровень Info зарезервирован и каталогом не выдаётся — он живёт только в фильтре "
            + "окна и в IssueDisplay");
        CollectionAssert.Contains(byCode.Values, IssueLevel.Error, "контроль: Error выдаётся");
        CollectionAssert.Contains(byCode.Values, IssueLevel.Warning, "контроль: Warning выдаётся");
    }

    [Test]
    public void IssueCatalog_EveryViolationKind_BecomesAnError_SoAWarningNeverReachesTheScenePaint()
    {
        var a = Board("Detal-A");

        foreach (ViolationKind kind in System.Enum.GetValues(typeof(ViolationKind)))
            Assert.AreEqual(IssueLevel.Error,
                IssueCatalog.FromViolation(new ContactViolation(a, null, kind)).Level,
                kind + ": сцену красит ValidationResult.violations, который наполняется только "
                + "нарушениями ViolationKind. Значит предупреждение подсветить нечем — у него "
                + "нет ViolationKind вовсе, и живёт оно только в списке окна «Ошибки»");
    }

    [Test]
    public void IssueCatalog_FromViolation_UnknownKind_FallsBackToCol00_AndNamesTheElement()
    {
        var a = Board("Detal-A");

        var issue = IssueCatalog.FromViolation(new ContactViolation(a, null, (ViolationKind)999));

        Assert.AreEqual(IssueCatalog.CodeUnknownViolation, issue.Code,
            "новый ViolationKind без своей ветки обязан попасть в отчёт под COL-00, а не "
            + "исчезнуть: неизвестное нарушение — это всё ещё нарушение");
        Assert.AreEqual("Detal-A", issue.Detail, "и деталь обязана быть названа");
        Assert.AreSame(a, issue.Target);
    }

    [Test]
    public void SceneAnalyzer_DishwasherBackGapMin_EqualsTheDishwasherFacadeMountGap()
    {
        Assert.AreEqual(DishwasherElement.FACADE_MOUNT_GAP_MM, SceneAnalyzer.DishwasherBackGapMinMm,
            1e-4f,
            "DWH-04 меряет ровно тот зазор, на котором фасад признаётся пристёгнутым: разъедься "
            + "эти два числа — и машина с законно навешенным фасадом получала бы вечное "
            + "предупреждение (или наоборот, прижатый вплотную фасад проходил бы молча)");
    }

    [Test]
    public void IssueCatalog_PairDetail_JoinsBothNames_AndKeepsOneWhenTheSecondIsMissing()
    {
        var a = Board("Detal-A");
        var b = Board("Detal-B");

        Assert.AreEqual("Detal-A ↔ Detal-B", IssueCatalog.DrawerFacadeOrphaned(a, b).Detail,
            "колонка «Деталь» показывает обе детали пары — по строке пользователь находит "
            + "виновника, а не только пострадавшего");
        Assert.AreEqual("Detal-A", IssueCatalog.DrawerFacadeOrphaned(a, null).Detail,
            "второй детали нет — колонка не превращается в «A ↔ —»");
    }
}
