using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Эти тесты — не столько защита от регрессии, сколько зафиксированный контракт:
/// коды, уровни и русские тексты, которые координатор переносит в ConstructionIssueCatalog
/// (файл этого агента не трогает — он в чужом владении на время кампании, agents/FLEET.md →
/// «Два воркера не должны одновременно править общие реестры»). Уровни взяты из
/// docs/todo_evolution.md §3.3: FND-01/02/05 — ошибка, FND-03/04 — предупреждение.</summary>
public class FoundationFindingsTests
{
    [Test]
    public void DepthBelowFrost_IsError_WithCodeFnd01_AndNamesTheSoilAndBothDepths()
    {
        var finding = FoundationFindings.DepthBelowFrost("wall-1", SoilKind.Clay, 1200f, 900f);

        Assert.AreEqual(ConstructionFindingLevel.Error, finding.Level);
        Assert.AreEqual("FND-01", finding.Code);
        Assert.AreEqual("wall-1", finding.ElementId);
        StringAssert.Contains("900", finding.Message);
        StringAssert.Contains("1200", finding.Message);
        StringAssert.Contains("Глина", finding.Message);
    }

    [Test]
    public void SoleTooNarrow_IsError_WithCodeFnd02_AndNamesTheMinimumAndTheActualWidth()
    {
        var finding = FoundationFindings.SoleTooNarrow("wall-2", SoilKind.Loam, 250f, 650f, 600f);

        Assert.AreEqual(ConstructionFindingLevel.Error, finding.Level);
        Assert.AreEqual("FND-02", finding.Code);
        StringAssert.Contains("600", finding.Message);
        StringAssert.Contains("650", finding.Message);
        StringAssert.Contains("Суглинок", finding.Message);
    }

    [Test]
    public void CushionTooThin_IsWarning_WithCodeFnd03_AndNamesBothLayers()
    {
        var finding = FoundationFindings.CushionTooThin("fnd-1", 80f, 100f);

        Assert.AreEqual(ConstructionFindingLevel.Warning, finding.Level);
        Assert.AreEqual("FND-03", finding.Code);
        StringAssert.Contains("80", finding.Message);
        StringAssert.Contains("100", finding.Message);
    }

    [Test]
    public void RebarCoverTooThin_IsWarning_WithCodeFnd04_AndNamesTheCoverAndDiameter()
    {
        var finding = FoundationFindings.RebarCoverTooThin("fnd-1", 20f, 12f);

        Assert.AreEqual(ConstructionFindingLevel.Warning, finding.Level);
        Assert.AreEqual("FND-04", finding.Code);
        StringAssert.Contains("20", finding.Message);
        StringAssert.Contains("12", finding.Message);
    }

    [Test]
    public void RebarStepTooWide_IsWarning_WithCodeFnd04_AndNamesTheStepAndWidth()
    {
        var finding = FoundationFindings.RebarStepTooWide("fnd-1", 700f, 600f);

        Assert.AreEqual(ConstructionFindingLevel.Warning, finding.Level);
        Assert.AreEqual("FND-04", finding.Code);
        StringAssert.Contains("700", finding.Message);
        StringAssert.Contains("600", finding.Message);
    }

    [Test]
    public void WallNotCovered_IsError_WithCodeFnd05_AndNamesTheWall()
    {
        var finding = FoundationFindings.WallNotCovered("wall-3");

        Assert.AreEqual(ConstructionFindingLevel.Error, finding.Level);
        Assert.AreEqual("FND-05", finding.Code);
        Assert.AreEqual("wall-3", finding.ElementId);
    }

    [Test]
    public void EveryFoundationCode_MatchesTheFndPrefixAndItsOwnNumber()
    {
        Assert.AreEqual("FND-01", FoundationFindings.CodeDepthBelowFrost);
        Assert.AreEqual("FND-02", FoundationFindings.CodeSoleTooNarrow);
        Assert.AreEqual("FND-03", FoundationFindings.CodeCushionTooThin);
        Assert.AreEqual("FND-04", FoundationFindings.CodeRebarProtection);
        Assert.AreEqual("FND-05", FoundationFindings.CodeWallNotCovered);
    }
}
