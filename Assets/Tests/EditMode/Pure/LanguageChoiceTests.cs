using KitchenDesigner.Core;
using NUnit.Framework;

public class LanguageChoiceTests
{
    private static readonly string[] RuEn = { "ru", "en" };
    private static readonly string[] RuEnAr = { "ru", "en", "ar" };

    [Test]
    public void Decide_StoredChoice_WinsOverTheSystemLanguage()
    {
        Assert.AreEqual("ru", LanguageChoice.Decide("ru", new[] { "en-US" }, RuEn, testRun: false));
    }

    [Test]
    public void Decide_NothingStored_TakesTheSystemLanguage_WhenTranslated()
    {
        Assert.AreEqual("ar", LanguageChoice.Decide(null, new[] { "ar-SA" }, RuEnAr, testRun: false));
    }

    [Test]
    public void Decide_SystemLanguageWithoutTranslation_FallsBackToEnglish()
    {
        Assert.AreEqual("en", LanguageChoice.Decide(null, new[] { "de-DE" }, RuEn, testRun: false),
            "немцу без перевода английский понятнее исходного русского");
    }

    [Test]
    public void Decide_SecondSystemSignal_IsTried_WhenTheFirstHasNoTranslation()
    {
        Assert.AreEqual("ru", LanguageChoice.Decide(null, new[] { "de", "ru" }, RuEn, testRun: false));
    }

    [Test]
    public void Decide_StoredLanguageNoLongerShipped_IsIgnored()
    {
        Assert.AreEqual("ru", LanguageChoice.Decide("fr", new[] { "ru-RU" }, RuEn, testRun: false),
            "удалённый из поставки язык не должен оставлять человека с ключами вместо текста");
    }

    [Test]
    public void Decide_TestRun_IsPinnedToTheSourceLanguage()
    {
        Assert.AreEqual("ru", LanguageChoice.Decide("en", new[] { "en-US" }, RuEn, testRun: true),
            "снапшоты UI сняты по-русски: прогон на английской машине или после выбора English "
            + "в редакторе не должен перекрасить все эталоны");
    }

    [Test]
    public void Decide_NoEnglishShipped_FallsBackToTheSource()
    {
        Assert.AreEqual("ru", LanguageChoice.Decide(null, new[] { "de" }, new[] { "ru" }, testRun: false));
    }

    private static readonly string[] WithRegionalFiles = { "ru", "en", "zh-Hans", "ar-TN", "ja" };

    [Test]
    public void Decide_StoredScriptTaggedLanguage_IsRestoredAfterRestart()
    {
        Assert.AreEqual("zh-Hans", LanguageChoice.Decide("zh-Hans", new[] { "en-US" }, WithRegionalFiles, testRun: false),
            "файл называется zh-Hans.json: сведение к первичному тегу «zh» теряло выбор человека при каждом запуске");
    }

    [TestCase("zh-CN", "zh-Hans")]
    [TestCase("ar-SA", "ar-TN")]
    [TestCase("AR_tn", "ar-TN")]
    [TestCase("ja-JP", "ja")]
    public void Decide_SystemLanguage_FindsTheRegionalFile_ByThePrimaryTag(string system, string expected)
    {
        Assert.AreEqual(expected, LanguageChoice.Decide(null, new[] { system }, WithRegionalFiles, testRun: false));
    }

    [Test]
    public void Match_PlainPrimaryFile_WinsOverARegionalOne()
    {
        Assert.AreEqual("ar", LanguageChoice.Match("ar-EG", new[] { "ar-TN", "ar" }),
            "общий арабский точнее чужого диалекта для египтянина");
    }

    [TestCase("en-US", "en")]
    [TestCase("EN", "en")]
    [TestCase("pt_BR", "pt")]
    [TestCase(" ru ", "ru")]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void Normalize_KeepsThePrimaryTagInLowerCase(string? raw, string? expected)
    {
        Assert.AreEqual(expected, LanguageChoice.Normalize(raw));
    }
}
