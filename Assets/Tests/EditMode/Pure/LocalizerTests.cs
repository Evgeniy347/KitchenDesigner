using System.Collections.Generic;
using KitchenDesigner.Core;
using NUnit.Framework;

public class LocalizerTests
{
    private static StringTable Table(string language, params (string key, string text)[] entries)
    {
        var map = new Dictionary<string, string>();
        foreach (var (key, text) in entries) map[key] = text;
        return new StringTable(language, map);
    }

    private static Localizer ThreeLanguages(string current) => new Localizer(new[]
    {
        Table("ru", ("a", "ру-а"), ("b", "ру-б"), ("c", "ру-в"), ("@name", "Русский")),
        Table("en", ("a", "en-a"), ("b", "en-b"), ("@name", "English")),
        Table("de", ("a", "de-a"), ("@name", "Deutsch")),
    }, current);

    [Test]
    public void Get_TakesTheCurrentLanguage_WhenItHasTheKey()
    {
        Assert.AreEqual("de-a", ThreeLanguages("de").Get("a"));
    }

    [Test]
    public void Get_FallsBackToEnglish_BeforeRussian()
    {
        Assert.AreEqual("en-b", ThreeLanguages("de").Get("b"),
            "немецкому без перевода английский понятнее русского исходника — цепочка current → en → ru");
    }

    [Test]
    public void Get_FallsBackToRussian_WhenEnglishLacksTheKey()
    {
        Assert.AreEqual("ру-в", ThreeLanguages("de").Get("c"));
    }

    [Test]
    public void Get_ReturnsTheKey_WhenNoLanguageHasIt()
    {
        Assert.AreEqual("missing.key", ThreeLanguages("en").Get("missing.key"),
            "ключ на экране виден и ищется в ru.json; пустая строка спрятала бы потерю перевода");
    }

    [Test]
    public void Constructor_UnknownLanguage_FallsBackToTheSourceLanguage()
    {
        var localizer = ThreeLanguages("xx");
        Assert.AreEqual("ru", localizer.Language);
        Assert.AreEqual("ру-а", localizer.Get("a"));
    }

    [Test]
    public void Format_SubstitutesPositionalArguments()
    {
        var localizer = new Localizer(new[] { Table("ru", ("az", "Азимут солнца: {0}°, {1}")) }, "ru");
        Assert.AreEqual("Азимут солнца: 135°, юг", localizer.Format("az", 135, "юг"));
    }

    [Test]
    public void Format_BrokenPatternInTranslation_FallsBackToTheNextLanguage()
    {
        var localizer = new Localizer(new[]
        {
            Table("ru", ("k", "Найдено: {0}")),
            Table("en", ("k", "Found: {0")),
        }, "en");

        Assert.AreEqual("Найдено: 3", localizer.Format("k", 3),
            "опечатка переводчика в фигурной скобке не должна ронять панель исключением");
    }

    [Test]
    public void Plural_Russian_PicksOneFewMany()
    {
        var localizer = new Localizer(new[]
        {
            Table("ru", ("n#one", "{0} ошибка"), ("n#few", "{0} ошибки"), ("n#many", "{0} ошибок")),
        }, "ru");

        Assert.AreEqual("1 ошибка", localizer.Plural("n", 1));
        Assert.AreEqual("3 ошибки", localizer.Plural("n", 3));
        Assert.AreEqual("5 ошибок", localizer.Plural("n", 5));
        Assert.AreEqual("11 ошибок", localizer.Plural("n", 11));
        Assert.AreEqual("21 ошибка", localizer.Plural("n", 21));
        Assert.AreEqual("22 ошибки", localizer.Plural("n", 22));
    }

    [Test]
    public void Plural_English_PicksOneOther()
    {
        var localizer = new Localizer(new[]
        {
            Table("en", ("n#one", "{0} error"), ("n#other", "{0} errors")),
        }, "en");

        Assert.AreEqual("1 error", localizer.Plural("n", 1));
        Assert.AreEqual("0 errors", localizer.Plural("n", 0));
        Assert.AreEqual("2 errors", localizer.Plural("n", 2));
    }

    [Test]
    public void Plural_ExtraArguments_FollowTheCount()
    {
        var localizer = new Localizer(new[]
        {
            Table("en", ("n#one", "{0} error in {1}"), ("n#other", "{0} errors in {1}")),
        }, "en");

        Assert.AreEqual("4 errors in Wall 1", localizer.Plural("n", 4, "Wall 1"));
    }

    [Test]
    public void Plural_MissingCategory_UsesOther()
    {
        var localizer = new Localizer(new[] { Table("ar", ("n#other", "{0} x")) }, "ar");
        Assert.AreEqual("2 x", localizer.Plural("n", 2));
    }

    [Test]
    public void Languages_ListSourceThenEnglishThenTheRest_WithNativeNames()
    {
        var languages = ThreeLanguages("ru").Languages;
        CollectionAssert.AreEqual(new[] { "ru", "en", "de" }, new[] { languages[0].Code, languages[1].Code, languages[2].Code });
        Assert.AreEqual("Deutsch", languages[2].NativeName);
    }

    [Test]
    public void IsRightToLeft_FollowsTheCurrentTable()
    {
        var tables = new[]
        {
            new StringTable("ru", new Dictionary<string, string>()),
            new StringTable("ar", new Dictionary<string, string> { ["@rtl"] = "true" }),
        };
        Assert.IsFalse(new Localizer(tables, "ru").IsRightToLeft);
        Assert.IsTrue(new Localizer(tables, "ar").IsRightToLeft);
    }

    [Test]
    public void WithLanguage_SwitchesWithoutReloadingTables()
    {
        var english = ThreeLanguages("ru").WithLanguage("en");
        Assert.AreEqual("en", english.Language);
        Assert.AreEqual("en-a", english.Get("a"));
    }
}
