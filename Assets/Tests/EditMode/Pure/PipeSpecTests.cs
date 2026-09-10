using NUnit.Framework;
using KitchenDesigner.Core.Plumbing;

/// <summary>Каталог диаметров по ГОСТ 3262-75 (труба водогазопроводная).
///
/// Числа взяты из ряда и не выводятся ни из чего: наружный диаметр и стенка —
/// справочные, внутренний считается как наружный минус две стенки. Если кто-то
/// «округлит» 21,3 до 21 или решит, что ДУ 15 и есть наружные 15 мм, ломается
/// именно этот файл.</summary>
public class PipeSpecTests
{
    [Test]
    public void PipeSpec_Table_MatchesTheGostRow()
    {
        var expected = new[]
        {
            (PipeSpec.Dn15, "1/2\"", 15, 21.3f, 2.8f),
            (PipeSpec.Dn20, "3/4\"", 20, 26.8f, 2.8f),
            (PipeSpec.Dn25, "1\"", 25, 33.5f, 3.2f),
            (PipeSpec.Dn32, "1 1/4\"", 32, 42.3f, 3.2f),
            (PipeSpec.Dn40, "1 1/2\"", 40, 48.0f, 3.5f),
            (PipeSpec.Dn50, "2\"", 50, 60.0f, 3.5f),
        };

        Assert.AreEqual(expected.Length, PipeSpec.Table.Length,
            "в ряду ГОСТ 3262-75 для этого каталога ровно шесть размеров");

        for (int i = 0; i < expected.Length; i++)
        {
            var size = PipeSpec.Table[i];
            Assert.AreEqual(expected[i].Item1, size.Id, "порядок размеров задаёт порядок в списке выбора");
            Assert.AreEqual(expected[i].Item2, size.Designation);
            Assert.AreEqual(expected[i].Item3, size.NominalBoreMm, "ДУ, мм");
            Assert.AreEqual(expected[i].Item4, size.OuterDiameterMm, 0.001f, "наружный диаметр, мм");
            Assert.AreEqual(expected[i].Item5, size.WallThicknessMm, 0.001f, "толщина стенки, мм");
        }
    }

    [Test]
    public void PipeSpec_InnerDiameter_IsOuterMinusTwoWalls()
    {
        Assert.AreEqual(15.7f, PipeSpec.Get(PipeSpec.Dn15).InnerDiameterMm, 0.001f,
            "21,3 − 2×2,8 = 15,7 мм: внутренний проход, а не ДУ");
        Assert.AreEqual(53.0f, PipeSpec.Get(PipeSpec.Dn50).InnerDiameterMm, 0.001f,
            "60,0 − 2×3,5 = 53,0 мм");
    }

    [Test]
    public void PipeSpec_NominalBore_IsNotTheOuterDiameter()
    {
        var size = PipeSpec.Get(PipeSpec.Dn15);
        Assert.AreNotEqual(size.NominalBoreMm, size.OuterDiameterMm,
            "ДУ 15 — условный проход, наружный у этой трубы 21,3 мм; путать их нельзя");
    }

    [Test]
    public void PipeSpec_Default_IsThreeQuarterInch()
    {
        Assert.AreEqual(PipeSpec.Dn20, PipeSpec.DEFAULT_SIZE);
        Assert.AreEqual("3/4\"", PipeSpec.Get(PipeSpec.DEFAULT_SIZE).Designation);
    }

    [Test]
    public void PipeSpec_NormalizeSize_FallsBackToDefault_OnAnUnknownId()
    {
        Assert.AreEqual(PipeSpec.DEFAULT_SIZE, PipeSpec.NormalizeSize("dn13"));
        Assert.AreEqual(PipeSpec.DEFAULT_SIZE, PipeSpec.NormalizeSize(null));
        Assert.AreEqual(PipeSpec.Dn50, PipeSpec.NormalizeSize("DN50"),
            "регистр идентификатора не должен терять размер");
    }

    [Test]
    public void PipeSpec_DesignationOrDash_GivesADash_WhenNothingIsKnown()
    {
        Assert.AreEqual(PipeSpec.NoValue, PipeSpec.DesignationOrDash(null),
            "неподключённый порт показывает прочерк, а не диаметр по умолчанию");
        Assert.AreEqual(PipeSpec.NoValue, PipeSpec.DesignationOrDash("dn13"));
        Assert.AreEqual("1\"", PipeSpec.DesignationOrDash(PipeSpec.Dn25));
    }

    [Test]
    public void PipeSpec_NominalOrDash_GivesADash_WhenNothingIsKnown()
    {
        Assert.AreEqual(PipeSpec.NoValue, PipeSpec.NominalOrDash(null),
            "отвод без известного диаметра показывает прочерк, а не ДУ по умолчанию — "
            + "иначе непристыкованный фитинг ушёл бы в ведомость с выдуманным числом");
        Assert.AreEqual(PipeSpec.NoValue, PipeSpec.NominalOrDash("dn13"));
        Assert.AreEqual("25", PipeSpec.NominalOrDash(PipeSpec.Dn25));
    }

    [Test]
    public void PipeSpec_Sizes_AreDerivedFromTheTable()
    {
        Assert.AreEqual(PipeSpec.Table.Length, PipeSpec.Sizes.Length);
        for (int i = 0; i < PipeSpec.Table.Length; i++)
            Assert.AreEqual(PipeSpec.Table[i].Id, PipeSpec.Sizes[i],
                "список идентификаторов выводится из таблицы, а не пишется вторым списком");
    }

    [Test]
    public void PipeSpec_TryFind_ReportsFailure_AndSucceedsOnAKnownId()
    {
        Assert.IsFalse(PipeSpec.TryFind("dn13", out _));
        Assert.IsTrue(PipeSpec.TryFind(PipeSpec.Dn32, out var found));
        Assert.AreEqual(32, found.NominalBoreMm);
    }
}
