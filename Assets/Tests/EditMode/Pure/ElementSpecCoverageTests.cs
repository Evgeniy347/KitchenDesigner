using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Правило маршрутизации в ведомость само по себе, без сцены.
///
/// Раньше здесь проверялась дизъюнкция «любой из четырёх путей засчитывает покрытие». Она
/// отвечала на вопрос «объявлен ли интерфейс», а нужен был другой: «вышла ли строка». Тип
/// с <c>GetSpecItems</c> из одного <c>yield break</c> проходил ту проверку и исчезал из
/// ведомости; и она же прятала ВТОРОЙ объявленный маршрут, который
/// <c>SpecificationManager.Build</c> не берёт никогда.
///
/// Теперь правило состоит из трёх частей: какие маршруты ОБЪЯВЛЕНЫ, какой из них Build
/// БЕРЁТ (первый по своему порядку), и какие после этого МЕРТВЫ. Порядок здесь и порядок в
/// <c>SpecificationManager.Build</c> — один и тот же код, а не два описания одного
/// контура.</summary>
public class ElementSpecCoverageTests
{
    [Test]
    public void Declared_NoPath_IsNone()
        => Assert.AreEqual(SpecRoute.None, ElementSpecCoverage.Declared(false, false, false));

    [Test]
    public void Declared_AllThreePaths_KeepsAllThreeFlags()
        => Assert.AreEqual(
            SpecRoute.Quantifies | SpecRoute.SpecificationParts | SpecRoute.FlatBoard,
            ElementSpecCoverage.Declared(true, true, true));

    [Test]
    public void Declared_FlatBoardOnly_IsFlatBoardAlone()
        => Assert.AreEqual(SpecRoute.FlatBoard, ElementSpecCoverage.Declared(false, false, true));

    [Test]
    public void Declared_SpecificationPartsOnly_IsSpecificationPartsAlone()
        => Assert.AreEqual(SpecRoute.SpecificationParts,
            ElementSpecCoverage.Declared(false, true, false));

    [Test]
    public void Taken_Nothing_IsNone()
        => Assert.AreEqual(SpecRoute.None, ElementSpecCoverage.Taken(SpecRoute.None));

    [Test]
    public void Taken_SingleRoute_IsThatRoute()
    {
        Assert.AreEqual(SpecRoute.Quantifies, ElementSpecCoverage.Taken(SpecRoute.Quantifies));
        Assert.AreEqual(SpecRoute.SpecificationParts,
            ElementSpecCoverage.Taken(SpecRoute.SpecificationParts));
        Assert.AreEqual(SpecRoute.FlatBoard, ElementSpecCoverage.Taken(SpecRoute.FlatBoard));
    }

    /// <summary>Порядок Build: IQuantifies выигрывает у обоих остальных, ISpecificationParts —
    /// у листовой детали. Это тот самый молчаливый проигрыш, ради которого правило и вынесено
    /// в отдельную функцию: «петли, шт» у фасада убьют его строку по площади ЛДСП.</summary>
    [Test]
    public void Taken_QuantifiesWinsOverEverythingElse()
    {
        Assert.AreEqual(SpecRoute.Quantifies,
            ElementSpecCoverage.Taken(SpecRoute.Quantifies | SpecRoute.FlatBoard));
        Assert.AreEqual(SpecRoute.Quantifies,
            ElementSpecCoverage.Taken(SpecRoute.Quantifies | SpecRoute.SpecificationParts));
        Assert.AreEqual(SpecRoute.Quantifies, ElementSpecCoverage.Taken(
            SpecRoute.Quantifies | SpecRoute.SpecificationParts | SpecRoute.FlatBoard));
    }

    [Test]
    public void Taken_SpecificationPartsWinsOverFlatBoard()
        => Assert.AreEqual(SpecRoute.SpecificationParts,
            ElementSpecCoverage.Taken(SpecRoute.SpecificationParts | SpecRoute.FlatBoard));

    [Test]
    public void Dead_SingleRoute_IsNone()
    {
        Assert.AreEqual(SpecRoute.None, ElementSpecCoverage.Dead(SpecRoute.Quantifies));
        Assert.AreEqual(SpecRoute.None, ElementSpecCoverage.Dead(SpecRoute.SpecificationParts));
        Assert.AreEqual(SpecRoute.None, ElementSpecCoverage.Dead(SpecRoute.FlatBoard));
        Assert.AreEqual(SpecRoute.None, ElementSpecCoverage.Dead(SpecRoute.None));
    }

    [Test]
    public void Dead_TwoRoutes_NamesTheOneBuildNeverWalks()
    {
        Assert.AreEqual(SpecRoute.FlatBoard,
            ElementSpecCoverage.Dead(SpecRoute.Quantifies | SpecRoute.FlatBoard));
        Assert.AreEqual(SpecRoute.FlatBoard,
            ElementSpecCoverage.Dead(SpecRoute.SpecificationParts | SpecRoute.FlatBoard));
        Assert.AreEqual(SpecRoute.SpecificationParts | SpecRoute.FlatBoard,
            ElementSpecCoverage.Dead(SpecRoute.Quantifies | SpecRoute.SpecificationParts
                | SpecRoute.FlatBoard));
    }

    /// <summary>Покрытие меряется ВЫДАННЫМИ строками, а не объявленным интерфейсом: ноль строк
    /// — не покрыт, чем бы тип себя ни объявил. Именно так десять радиусных полок и весь
    /// список покупных изделий пропали из ведомости молча.</summary>
    [Test]
    public void IsCovered_ZeroLines_ReportsNotCovered()
        => Assert.IsFalse(ElementSpecCoverage.IsCovered(0));

    [Test]
    public void IsCovered_AtLeastOneLine_ReportsCovered()
    {
        Assert.IsTrue(ElementSpecCoverage.IsCovered(1));
        Assert.IsTrue(ElementSpecCoverage.IsCovered(7));
    }
}
