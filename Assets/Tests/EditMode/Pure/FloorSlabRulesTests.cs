using NUnit.Framework;
using KitchenDesigner.Core.Construction;

/// <summary>FLR-01, docs/todo_evolution.md §3.3: перекрытие не опирается на стену по контуру
/// (щель > 0) — предупреждение. Координатор сам заводит запись в ConstructionIssueCatalog —
/// этот файл её не трогает и строит ConstructionFinding самостоятельно; код, уровень и текст
/// ниже — то, что нужно перенести туда.
///
/// Code: FLR-01. Level: Warning. Message (RU):
/// «Перекрытие «{id}» не опирается на стену по контуру: зазор {N} мм — проверьте опирание плиты».</summary>
public class FloorSlabRulesTests
{
    [Test]
    public void FloorSlabRules_RestsOnTheWall_PositiveGap_IsFalse()
    {
        Assert.IsFalse(FloorSlabRules.RestsOnTheWall(5f),
            "любой положительный зазор — это и есть FLR-01: плита не опирается по контуру");
    }

    [Test]
    public void FloorSlabRules_RestsOnTheWall_ZeroGap_IsTrue()
    {
        Assert.IsTrue(FloorSlabRules.RestsOnTheWall(0f),
            "нулевой зазор — плита ровно на стене, опирание есть. Это противоположный вход "
            + "к предыдущему тесту: одна и та же величина по разные стороны от нуля обязана "
            + "давать разные ответы, иначе правило не проверяет саму границу");
    }

    [Test]
    public void FloorSlabRules_RestsOnTheWall_NegativeGap_IsTrue()
    {
        Assert.IsTrue(FloorSlabRules.RestsOnTheWall(-10f),
            "отрицательный «зазор» — это нахлёст плиты на стену, не щель; FLR-01 про щель, "
            + "нахлёст этим правилом не наказывается");
    }

    [Test]
    public void FloorSlabRules_GapToSupportingWall_BuildsTheDocumentedFinding()
    {
        var finding = FloorSlabRules.GapToSupportingWall("Perekrytie-1", 12.5f);

        Assert.AreEqual("FLR-01", finding.Code);
        Assert.AreEqual(ConstructionFindingLevel.Warning, finding.Level,
            "щель — не аварийная ошибка, а предупреждение: сама по себе плита не рушится, "
            + "просто часть нагрузки может не передаваться на стену");
        Assert.AreEqual("Perekrytie-1", finding.ElementId);
        StringAssert.Contains("Perekrytie-1", finding.Message);
        StringAssert.Contains("12.5", finding.Message,
            "зазор форматируется через InvariantCulture: разделитель — точка, а не "
            + "запятая текущей локали, иначе строка расходится между машинами");
        StringAssert.Contains("не опирается на стену по контуру", finding.Message);
    }

    [Test]
    public void FloorSlabRules_Collect_SlabWithAGap_ReportsFlr01()
    {
        var slabs = new[] { new FloorSlabSurvey("Perekrytie-1", 8f) };

        var findings = FloorSlabRules.Collect(slabs);

        Assert.AreEqual(1, findings.Count);
        Assert.AreEqual("FLR-01", findings[0].Code);
    }

    [Test]
    public void FloorSlabRules_Collect_SlabFlushWithTheWall_ReportsNothing()
    {
        var slabs = new[] { new FloorSlabSurvey("Perekrytie-1", 0f) };

        var findings = FloorSlabRules.Collect(slabs);

        Assert.AreEqual(0, findings.Count,
            "противоположный вход к предыдущему тесту: без щели — без предупреждения");
    }

    [Test]
    public void FloorSlabRules_Collect_MixOfGoodAndBadSlabs_ReportsOnlyTheBadOne()
    {
        var slabs = new[]
        {
            new FloorSlabSurvey("Good", -2f),
            new FloorSlabSurvey("Bad", 3f),
            new FloorSlabSurvey("AlsoGood", 0f),
        };

        var findings = FloorSlabRules.Collect(slabs);

        Assert.AreEqual(1, findings.Count);
        Assert.AreEqual("Bad", findings[0].ElementId,
            "правило обязано указывать на КОНКРЕТНОЕ перекрытие, а не на первое в списке "
            + "или на все сразу");
    }

    [Test]
    public void FloorSlabRules_Collect_NullList_IsEmpty_NotACrash()
    {
        var findings = FloorSlabRules.Collect(null);

        Assert.AreEqual(0, findings.Count,
            "список перекрытий ещё не собран сценой — пустой отчёт, а не исключение");
    }
}
