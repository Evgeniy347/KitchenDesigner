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
