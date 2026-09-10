using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Правило само по себе — «любой из четырёх путей засчитывает покрытие,
/// ни одного пути нет ⇒ не засчитывает». Каждая ветка проверена отдельно, и есть
/// противоположный вход: все флаги ложны.</summary>
public class ElementSpecCoverageTests
{
    [Test]
    public void IsCovered_SelfQuantifies_ReportsCovered()
        => Assert.IsTrue(ElementSpecCoverage.IsCovered(
            selfQuantifies: true, isSpecificationParts: false,
            isFlatBoardElement: false, isKnownExclusion: false));

    [Test]
    public void IsCovered_IsSpecificationParts_ReportsCovered()
        => Assert.IsTrue(ElementSpecCoverage.IsCovered(
            selfQuantifies: false, isSpecificationParts: true,
            isFlatBoardElement: false, isKnownExclusion: false));

    [Test]
    public void IsCovered_IsFlatBoardElement_ReportsCovered()
        => Assert.IsTrue(ElementSpecCoverage.IsCovered(
            selfQuantifies: false, isSpecificationParts: false,
            isFlatBoardElement: true, isKnownExclusion: false));

    [Test]
    public void IsCovered_KnownExclusion_ReportsCovered()
        => Assert.IsTrue(ElementSpecCoverage.IsCovered(
            selfQuantifies: false, isSpecificationParts: false,
            isFlatBoardElement: false, isKnownExclusion: true));

    /// <summary>Противоположный вход: ни одного пути покрытия и не исключение —
    /// именно так десять радиусных полок пропали из ведомости молча.</summary>
    [Test]
    public void IsCovered_NoPathAndNotExcluded_ReportsNotCovered()
        => Assert.IsFalse(ElementSpecCoverage.IsCovered(
            selfQuantifies: false, isSpecificationParts: false,
            isFlatBoardElement: false, isKnownExclusion: false));
}
