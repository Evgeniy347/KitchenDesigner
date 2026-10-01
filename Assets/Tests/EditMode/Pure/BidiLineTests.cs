using KitchenDesigner.Core;
using NUnit.Framework;

public class BidiLineTests
{
    [Test]
    public void BidiLine_Visual_ArabicWord_IsReversed()
    {
        Assert.AreEqual("تبا", BidiLine.Visual("ابت", rightToLeftBase: true));
    }

    [Test]
    public void BidiLine_Visual_NumberInArabic_KeepsItsDigitsLeftToRight()
    {
        Assert.AreEqual("1,200.5 تبا", BidiLine.Visual("ابت 1,200.5", rightToLeftBase: true),
            "число внутри арабской строки читается слева направо целиком, вместе с разделителями");
    }

    [Test]
    public void BidiLine_Visual_LatinNameInArabic_StaysOneLeftToRightRun()
    {
        Assert.AreEqual("GTV Box تبا", BidiLine.Visual("ابت GTV Box", rightToLeftBase: true),
            "латинское имя внутри арабской подписи — один прогон слева направо, слова не переставляются");
    }

    [Test]
    public void BidiLine_Visual_Brackets_AreMirroredInsideTheRightToLeftRun()
    {
        Assert.AreEqual("(ب) ا", BidiLine.Visual("ا (ب)", rightToLeftBase: true),
            "скобки в арабском прогоне зеркалятся, иначе они смотрят наружу");
    }

    [Test]
    public void BidiLine_Visual_FormattedPlaceholder_StaysNextToItsWord()
    {
        Assert.AreEqual("{0} تبا", BidiLine.Visual("ابت {0}", rightToLeftBase: true),
            "неподставленный {0} — скобки и цифра остаются одной группой слева");
    }

    [Test]
    public void BidiLine_Visual_PercentAfterArabic_FollowsTheArabicNumberRule()
    {
        Assert.AreEqual("%50 ا", BidiLine.Visual("ا 50%", rightToLeftBase: true),
            "после арабской буквы число становится арабским (правило W2), и знак процента к нему не прилипает — так его показывает любой браузер");
    }

    [Test]
    public void BidiLine_Visual_LeftToRightBase_ReversesOnlyTheArabicRun()
    {
        Assert.AreEqual("Width: تبا", BidiLine.Visual("Width: ابت", rightToLeftBase: false));
    }

    [Test]
    public void BidiLine_ForRightToLeftLayout_ArabicStaysLogical_NumberIsStoredReversed()
    {
        Assert.AreEqual("ابت 321", BidiLine.ForRightToLeftLayout("ابت 123", rightToLeftBase: true),
            "TMP с isRightToLeftText кладёт символы справа налево сам: арабское остаётся в логическом порядке, "
            + "а прогон слева направо подаётся задом наперёд, чтобы на экране вышло 123");
    }

    [Test]
    public void BidiLine_ForRightToLeftLayout_SurrogatePair_IsNotTornApart()
    {
        const string emoji = "\U0001F600";
        Assert.AreEqual("ا " + emoji + "x", BidiLine.ForRightToLeftLayout("ا x" + emoji, rightToLeftBase: true),
            "латинский прогон подаётся задом наперёд, но суррогатная пара переставляется целиком, иначе вместо символа два квадрата");
    }

    [TestCase("GTV ابت", false)]
    [TestCase("ابت GTV", true)]
    [TestCase("800 ابت", true)]
    [TestCase("800", null)]
    public void BidiLine_FirstStrongIsRightToLeft_ReadsTheFirstLetter(string text, bool? expected)
    {
        Assert.AreEqual(expected, BidiLine.FirstStrongIsRightToLeft(text));
    }
}
