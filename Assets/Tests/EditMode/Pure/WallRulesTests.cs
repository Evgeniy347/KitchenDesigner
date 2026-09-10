using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core.Construction;

/// <summary>WAL-01 — толщина стены не кратна формату кладки со швом. Это
/// ПРЕДУПРЕЖДЕНИЕ, а не ошибка: стена нестандартной толщины стоять будет, просто
/// кирпич придётся резать, и человек вправе так сделать сознательно. Ошибкой это
/// сделать нельзя — окно «Ошибки» красит ошибками геометрию, физически
/// недопустимую, а тут допустимая.
///
/// Ряд стандартных толщин выводится формулой «рядов × ширина формата + швы между
/// ними», а не списком литералов: для кирпича 120 мм со швом 10 это 120 / 250 /
/// 380 / 510 мм — полкирпича, кирпич, полтора, два. Список литералов пришлось бы
/// переписывать при каждой смене шва, а шов — настройка пользователя.
///
/// У каждого правила здесь есть парный отрицательный случай: правило, которое
/// умеет только срабатывать, зелёного состояния не описывает.</summary>
public class WallRulesTests
{
    private static WallSurvey Brick(float thicknessMm, float jointMm = 10f) =>
        new WallSurvey("Stena-1", MasonryTechnology.BrickSingle, thicknessMm, jointMm);

    private static List<ConstructionFinding> WithCode(
        IReadOnlyList<ConstructionFinding> findings, string code)
    {
        var picked = new List<ConstructionFinding>();
        foreach (var finding in findings)
            if (finding.Code == code) picked.Add(finding);
        return picked;
    }

    [Test]
    public void WallRules_Wal01_BrickWithJoint10_AcceptsTheStandardSeries120_250_380_510()
    {
        foreach (var thickness in new[] { 120f, 250f, 380f, 510f })
            Assert.IsTrue(WallRules.ThicknessFitsFormat(MasonryTechnology.BrickSingle, thickness, 10f),
                thickness + " мм — стандартная толщина кирпичной кладки со швом 10 "
                + "(полкирпича, кирпич, полтора, два). Задание называло 250 / 380 / 510; "
                + "120 мм добавлено формулой и оставлено намеренно — это законная "
                + "перегородка в полкирпича, и предупреждать о ней было бы ложной тревогой");
    }

    [Test]
    public void WallRules_Wal01_BrickAt300mm_IsWarned_BecauseTheBrickWouldHaveToBeCut()
    {
        var findings = WallRules.Collect(new[] { Brick(300f) });
        var wal01 = WithCode(findings, ConstructionIssueCatalog.CodeWallThicknessOffFormat);

        Assert.AreEqual(1, wal01.Count,
            "300 мм не лежит в ряду 120 / 250 / 380 / 510 — кирпич придётся резать");
        Assert.AreEqual(ConstructionFindingLevel.Warning, wal01[0].Level,
            "уровень — предупреждение: такая стена стоять БУДЕТ. Сделай это ошибкой — и "
            + "окно «Ошибки» начнёт запрещать законную нестандартную кладку");
        Assert.AreEqual("Stena-1", wal01[0].ElementId,
            "замечание обязано назвать стену: по строке пользователь находит деталь");
        StringAssert.Contains("250", wal01[0].Message,
            "в тексте обязан быть ряд стандартных толщин — иначе пользователь знает, что "
            + "не так, но не знает, что поставить вместо");
    }

    [Test]
    public void WallRules_Wal01_IsSilentOnAStandardWall_SoTheRuleDescribesGreenToo()
    {
        CollectionAssert.IsEmpty(
            WallRules.Collect(new[] { Brick(250f) }),
            "стена в один кирпич — эталонная кладка, замечаний быть не должно");
    }

    [Test]
    public void WallRules_Wal01_SeriesFollowsTheJoint_NotAHardCodedListOfThicknesses()
    {
        Assert.IsTrue(WallRules.ThicknessFitsFormat(MasonryTechnology.BrickSingle, 250f, 10f),
            "шов 10: два кирпича по 120 плюс шов = 250");
        Assert.IsFalse(WallRules.ThicknessFitsFormat(MasonryTechnology.BrickSingle, 250f, 12f),
            "шов 12: два кирпича дают 252, и 250 в ряд уже не попадает. Зашей ряд "
            + "литералами 250 / 380 / 510 — и правило перестанет замечать смену шва, "
            + "которая и есть настройка пользователя");
        Assert.IsTrue(WallRules.ThicknessFitsFormat(MasonryTechnology.BrickSingle, 252f, 12f),
            "контроль: при шве 12 ряд сдвигается на 252");
    }

    [Test]
    public void WallRules_Wal01_AeratedBlockAt250_IsWarned_ButAt300_IsNot()
    {
        Assert.IsFalse(WallRules.ThicknessFitsFormat(MasonryTechnology.AeratedBlock, 250f, 10f),
            "блок 600×300×200 не даёт стену 250 мм без распила по длине");
        Assert.IsTrue(WallRules.ThicknessFitsFormat(MasonryTechnology.AeratedBlock, 300f, 10f),
            "300 мм — ширина самого блока, кладка в один ряд");
    }

    [Test]
    public void WallRules_Wal01_DoesNotApplyToTimberOrFrame_WhichHaveNoMasonryFormat()
    {
        foreach (var technology in new[] { MasonryTechnology.Timber, MasonryTechnology.Frame })
        {
            CollectionAssert.IsEmpty(MasonryUnit.ThicknessSeries(technology, 10f),
                technology + ": формата кладки нет, значит нет и ряда толщин");
            CollectionAssert.IsEmpty(
                WallRules.Collect(new[] { new WallSurvey("Stena-1", technology, 137f, 10f) }),
                technology + ": толщина задаётся сечением бруса или стойки — резать там "
                + "нечего, и предупреждение было бы ложной тревогой на каждой стене");
        }
    }

    [Test]
    public void WallRules_Wal01_ToleranceIsHalfAMillimetre_SoFloatNoiseDoesNotRaiseIt()
    {
        Assert.IsTrue(WallRules.ThicknessFitsFormat(MasonryTechnology.BrickSingle, 250.4f, 10f),
            "0,4 мм — шум округления толщины, а не решение резать кирпич");
        Assert.IsFalse(WallRules.ThicknessFitsFormat(MasonryTechnology.BrickSingle, 251f, 10f),
            "1 мм уже за допуском 0,5 мм: допуск обязан быть МЕНЬШЕ артефакта, против "
            + "которого он написан — conventions/TEST-NAMING.md");
        Assert.AreEqual(0.5f, WallRules.ThicknessToleranceMm, 1e-6f);
    }

    [Test]
    public void WallRules_Collect_ReportsEveryOffFormatWall_AndNullSceneIsEmpty()
    {
        var findings = WallRules.Collect(new[]
        {
            new WallSurvey("A", MasonryTechnology.BrickSingle, 300f, 10f),
            new WallSurvey("B", MasonryTechnology.BrickSingle, 250f, 10f),
            new WallSurvey("C", MasonryTechnology.BrickSingle, 400f, 10f),
        });

        Assert.AreEqual(2, findings.Count,
            "две стены из трёх нестандартные — сторож обязан назвать обе, а не первую");
        CollectionAssert.IsEmpty(WallRules.Collect(null),
            "сцена без стен — не повод падать");
    }

    [Test]
    public void ConstructionIssueCatalog_Wal01_CodeIsPinned_BecauseTheErrorWindowFiltersOnIt()
    {
        Assert.AreEqual("WAL-01", ConstructionIssueCatalog.CodeWallThicknessOffFormat,
            "код только ДОБАВЛЯЕТСЯ: на него завязан фильтр по кодам в окне «Ошибки» и "
            + "поле code в ответе MCP, поэтому переименование — молчаливая поломка "
            + "чужого фильтра, а не рефакторинг");
    }

    [Test]
    public void MasonryUnit_ThicknessSeries_HasFourRows_AndIsStrictlyIncreasing()
    {
        var series = MasonryUnit.ThicknessSeries(MasonryTechnology.BrickSingle, 10f);

        Assert.AreEqual(MasonryUnit.SeriesRows, series.Count);
        for (int i = 1; i < series.Count; i++)
            Assert.Greater(series[i], series[i - 1],
                "ряд толщин обязан расти: равные соседние значения означают, что шов "
                + "или ширина формата попали в формулу с неверным знаком");
        Assert.AreEqual(120f, series[0], 1e-4f);
        Assert.AreEqual(510f, series[series.Count - 1], 1e-4f);
    }
}
