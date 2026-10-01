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

    private static readonly string[] AllTen =
        { "ru", "en", "ar-TN", "de", "es", "fr", "it", "ja", "pt", "zh-Hans" };

    [TestCase("ru-RU", "ru")]
    [TestCase("en-US", "en")]
    [TestCase("en-GB", "en")]
    [TestCase("zh-CN", "zh-Hans")]
    [TestCase("zh-SG", "zh-Hans")]
    [TestCase("zh-Hans-CN", "zh-Hans")]
    [TestCase("ar-TN", "ar-TN")]
    [TestCase("ar-SA", "ar-TN")]
    [TestCase("ar-EG", "ar-TN")]
    [TestCase("ar", "ar-TN")]
    [TestCase("pt-PT", "pt")]
    [TestCase("pt-BR", "pt")]
    [TestCase("pt", "pt")]
    [TestCase("es-ES", "es")]
    [TestCase("es-MX", "es")]
    [TestCase("es-419", "es")]
    [TestCase("de-DE", "de")]
    [TestCase("de-AT", "de")]
    [TestCase("de-CH", "de")]
    [TestCase("fr-FR", "fr")]
    [TestCase("fr-CA", "fr")]
    [TestCase("fr-BE", "fr")]
    [TestCase("fr-CH", "fr")]
    [TestCase("it-IT", "it")]
    [TestCase("it-CH", "it")]
    [TestCase("ja-JP", "ja")]
    [TestCase("ko-KR", "en")]
    [TestCase("tr-TR", "en")]
    [TestCase("", "en")]
    public void Decide_EveryOsVariantOfTheTenLanguages_FindsItsFile(string os, string expected)
    {
        Assert.AreEqual(expected, LanguageChoice.Decide(null, new[] { os }, AllTen, testRun: false),
            "язык ОС с регионом обязан попасть в свой файл; без перевода — английский");
    }

    [Test]
    public void Decide_InstallerLanguage_WinsOverTheOsLanguage_WhenNothingIsStored()
    {
        Assert.AreEqual("de", LanguageChoice.Decide(null, "de", new[] { "fr-FR" }, AllTen, testRun: false),
            "человек выбрал язык в установщике — это осознанный выбор, он сильнее языка ОС");
    }

    [Test]
    public void Decide_StoredLanguage_WinsOverTheInstallerLanguage()
    {
        Assert.AreEqual("ja", LanguageChoice.Decide("ja", "de", new[] { "fr-FR" }, AllTen, testRun: false),
            "язык, выбранный внутри приложения, не должен перетираться ни установщиком, ни обновлением");
    }

    [Test]
    public void Decide_InstallerLanguageNotShipped_FallsThroughToTheOs()
    {
        Assert.AreEqual("fr", LanguageChoice.Decide(null, "xx", new[] { "fr-FR" }, AllTen, testRun: false));
    }

    [Test]
    public void Decide_NothingAtAll_IsEnglish()
    {
        Assert.AreEqual("en", LanguageChoice.Decide(null, null, new string?[] { null }, AllTen, testRun: false));
    }

    [Test]
    public void Decide_TestRun_IgnoresTheInstallerLanguage()
    {
        Assert.AreEqual("ru", LanguageChoice.Decide(null, "de", new[] { "fr" }, AllTen, testRun: true));
    }

    [TestCase(null, "de", false, "de")]
    [TestCase("", "de", false, "de")]
    [TestCase("ja", "de", false, null)]
    [TestCase("zh-Hans", "de", false, null)]
    [TestCase("klingon", "de", false, "de")]
    [TestCase(null, "klingon", false, null)]
    [TestCase(null, null, false, null)]
    [TestCase(null, "de", true, null)]
    [TestCase(null, "zh-CN", false, "zh-Hans")]
    public void InstallerLanguageToKeep_IsTakenOnlyWhenThePersonHasNoLanguageYet(
        string? stored, string? installer, bool testRun, string? expected)
    {
        Assert.AreEqual(expected, LanguageChoice.InstallerLanguageToKeep(stored, installer, AllTen, testRun),
            "приложение переписывает язык установщика в PlayerPrefs только при пустом выборе; "
            + "иначе обновление затирало бы выбор человека, а прогон тестов писал бы в реестр пользователя");
    }

    [Test]
    public void InstallLanguageRead_NeverThrows_AndYieldsNothingOrACode()
    {
        var value = InstallLanguage.Read();

        Assert.That(value == null || value.Length is > 0 and <= 16, Is.True,
            "чтение реестра при запуске не имеет права ронять приложение, а значение — быть мусором: "
            + (value ?? "<нет>"));
    }
}
