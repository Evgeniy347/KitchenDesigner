using KitchenDesigner.Core;
using NUnit.Framework;

public class ArabicShaperTests
{
    private static string Codes(string text)
    {
        var parts = new string[text.Length];
        for (int i = 0; i < text.Length; i++) parts[i] = ((int)text[i]).ToString("X4");
        return string.Join(" ", parts);
    }

    private static void AssertShaped(string logical, string expected, string why) =>
        Assert.AreEqual(Codes(expected), Codes(ArabicShaper.Shape(logical)), why);

    [Test]
    public void ArabicShaper_Shape_Mohammed_TakesInitialMedialMedialFinal()
    {
        AssertShaped("محمد", "ﻣﺤﻤﺪ",
            "TMP не делает контекстных форм: без замены на формы представления буквы стоят оторванными");
    }

    [Test]
    public void ArabicShaper_Shape_Salam_JoinsLamAndAlefIntoOneLigature()
    {
        AssertShaped("سلام", "ﺳﻼﻡ",
            "лам перед алифом обязан стать лигатурой (конечной — лам соединён с предыдущей); две отдельные буквы читаются как ошибка");
    }

    [Test]
    public void ArabicShaper_Shape_Arabi_BreaksTheJoinAfterARightJoiningLetter()
    {
        AssertShaped("عربي", "ﻋﺮﺑﻲ",
            "ра соединяется только с предыдущей: следующая за ней ба начинает слово заново (начальная форма)");
    }

    [Test]
    public void ArabicShaper_Shape_Kitab_EndsWithAnIsolatedLetterAfterAlef()
    {
        AssertShaped("كتاب", "ﻛﺘﺎﺏ", "после алифа последняя ба отдельная");
    }

    [Test]
    public void ArabicShaper_Shape_Tunis_AlternatesAcrossTheWaw()
    {
        AssertShaped("تونس", "ﺗﻮﻧﺲ", "вав рвёт соединение так же, как алиф");
    }

    [Test]
    public void ArabicShaper_Shape_Settings_LamAlefWithHamzaBelow_StandsAloneAfterAlef()
    {
        AssertShaped("الإعدادات", "ﺍﻹﻋﺪﺍﺩﺍﺕ",
            "лам после алифа ни с чем не соединён — лигатура изолированная; дальше чередование по правым буквам");
    }

    [Test]
    public void ArabicShaper_Shape_LoneLamAlef_IsTheIsolatedLigature()
    {
        AssertShaped("لا", "ﻻ", "одиночное «ля»");
    }

    [Test]
    public void ArabicShaper_Shape_Hamza_NeverJoins()
    {
        AssertShaped("ماء", "ﻣﺎﺀ", "хамза на строке не соединяется ни с одной стороной");
    }

    [Test]
    public void ArabicShaper_Shape_Haraka_IsTransparentForJoining()
    {
        AssertShaped("بَب", "ﺑَﺐ",
            "огласовка между буквами не рвёт соединение — иначе огласованный текст рассыпается");
    }

    [Test]
    public void ArabicShaper_Shape_Tatweel_ForcesTheJoin()
    {
        AssertShaped("بـ", "ﺑـ", "татвиль тянет соединение — буква перед ним в начальной форме");
    }

    [Test]
    public void ArabicShaper_Shape_MaghrebiVe_UsesTheExtendedPresentationForms()
    {
        AssertShaped("ڤڤ", "ﭬﭫ", "ڤ пишется в тунисских заимствованиях; её формы лежат в блоке FB50");
    }

    [Test]
    public void ArabicShaper_Shape_TextWithoutArabic_IsReturnedUnchanged()
    {
        const string latin = "GTV 800 мм";
        Assert.AreSame(latin, ArabicShaper.Shape(latin), "строки без арабского не трогаются и не копируются");
    }
}
