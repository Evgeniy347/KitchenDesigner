using KitchenDesigner.Core;
using NUnit.Framework;

public class PluralRulesTests
{
    [TestCase(0, PluralCategory.Many)]
    [TestCase(1, PluralCategory.One)]
    [TestCase(2, PluralCategory.Few)]
    [TestCase(4, PluralCategory.Few)]
    [TestCase(5, PluralCategory.Many)]
    [TestCase(11, PluralCategory.Many)]
    [TestCase(12, PluralCategory.Many)]
    [TestCase(14, PluralCategory.Many)]
    [TestCase(21, PluralCategory.One)]
    [TestCase(22, PluralCategory.Few)]
    [TestCase(111, PluralCategory.Many)]
    [TestCase(101, PluralCategory.One)]
    public void For_Russian_FollowsCldr(long n, PluralCategory expected)
    {
        Assert.AreEqual(expected, PluralRules.For("ru", n));
    }

    [TestCase(0, PluralCategory.Zero)]
    [TestCase(1, PluralCategory.One)]
    [TestCase(2, PluralCategory.Two)]
    [TestCase(3, PluralCategory.Few)]
    [TestCase(10, PluralCategory.Few)]
    [TestCase(11, PluralCategory.Many)]
    [TestCase(99, PluralCategory.Many)]
    [TestCase(100, PluralCategory.Other)]
    [TestCase(102, PluralCategory.Other)]
    [TestCase(103, PluralCategory.Few)]
    public void For_Arabic_FollowsCldr(long n, PluralCategory expected)
    {
        Assert.AreEqual(expected, PluralRules.For("ar", n));
    }

    [TestCase(0, PluralCategory.Other)]
    [TestCase(1, PluralCategory.One)]
    [TestCase(2, PluralCategory.Other)]
    public void For_English_IsOneOrOther(long n, PluralCategory expected)
    {
        Assert.AreEqual(expected, PluralRules.For("en", n));
    }

    [TestCase("ar-TN")]
    [TestCase("ar-EG")]
    [TestCase("ar_SA")]
    [TestCase("AR-tn")]
    public void For_ArabicRegionalVariant_UsesTheArabicRule(string language)
    {
        Assert.AreEqual(PluralCategory.Zero, PluralRules.For(language, 0),
            "файл называется ar-TN.json: поиск правила по полному коду отдавал арабу английское «one/other»");
        Assert.AreEqual(PluralCategory.Two, PluralRules.For(language, 2));
        Assert.AreEqual(PluralCategory.Few, PluralRules.For(language, 7));
        Assert.AreEqual(PluralCategory.Many, PluralRules.For(language, 15));
    }

    [TestCase("ru-RU", 21, PluralCategory.One)]
    [TestCase("ru_UA", 3, PluralCategory.Few)]
    public void For_RussianRegionalVariant_UsesTheRussianRule(string language, long n, PluralCategory expected)
    {
        Assert.AreEqual(expected, PluralRules.For(language, n));
    }

    [TestCase("zh-Hans")]
    [TestCase("zh-CN")]
    [TestCase("zh")]
    [TestCase("ja")]
    [TestCase("ja-JP")]
    [TestCase("ko")]
    public void For_LanguagesWithoutPlurals_IsAlwaysOther(string language)
    {
        foreach (var n in new long[] { 0, 1, 2, 5, 11, 100 })
            Assert.AreEqual(PluralCategory.Other, PluralRules.For(language, n),
                "в китайском и японском нет грамматического числа: n=1 не должно давать «one», иначе ключ #one, "
                + "которого в таблице нет, роняет подстановку на «other» только по счастливой случайности (" + n + ")");
    }

    [TestCase("")]
    [TestCase(null)]
    public void For_NoLanguage_FallsBackToOneOrOther(string? language)
    {
        Assert.AreEqual(PluralCategory.One, PluralRules.For(language!, 1));
        Assert.AreEqual(PluralCategory.Other, PluralRules.For(language!, 3));
    }

    [Test]
    public void TryGetPlural_RegionalArabicTable_PicksTheTwoForm()
    {
        var table = new StringTable("ar-TN", new System.Collections.Generic.Dictionary<string, string>
        {
            ["k#two"] = "اثنان", ["k#other"] = "غيره",
        });
        Assert.IsTrue(table.TryGetPlural("k", 2, out var text));
        Assert.AreEqual("اثنان", text, "таблица ar-TN обязана получить арабское правило, а не английское");
    }

    [Test]
    public void TryGetPlural_ChineseTable_IgnoresTheOneForm()
    {
        var table = new StringTable("zh-Hans", new System.Collections.Generic.Dictionary<string, string>
        {
            ["k#one"] = "один", ["k#other"] = "项",
        });
        Assert.IsTrue(table.TryGetPlural("k", 1, out var text));
        Assert.AreEqual("项", text, "в китайском одна форма: «one» не выбирается даже при n=1");
    }

    [Test]
    public void For_NegativeCount_UsesTheMagnitude()
    {
        Assert.AreEqual(PluralCategory.One, PluralRules.For("ru", -21));
    }
}
