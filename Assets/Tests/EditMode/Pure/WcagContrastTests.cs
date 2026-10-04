using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

public class WcagContrastTests
{
    [Test]
    public void WcagContrast_BlackOnWhite_IsTwentyOne()
    {
        Assert.AreEqual(21f, WcagContrast.Ratio(Color.black, Color.white), 0.01f);
        Assert.AreEqual(21f, WcagContrast.Ratio(Color.white, Color.black), 0.01f, "отношение симметрично");
    }

    [Test]
    public void WcagContrast_KnownReferencePair_MatchesTheWcagFormula()
    {
        var grey = new Color(0x77 / 255f, 0x77 / 255f, 0x77 / 255f, 1f);
        Assert.AreEqual(4.48f, WcagContrast.Ratio(grey, Color.white), 0.01f,
            "#777 на белом — классический пример «чуть ниже AA»: 4,48:1");
    }

    [Test]
    public void WcagContrast_TranslucentForeground_IsBlendedOverTheBackground()
    {
        var halfWhite = new Color(1f, 1f, 1f, 0f);
        Assert.AreEqual(1f, WcagContrast.Ratio(halfWhite, Color.black), 0.001f,
            "прозрачный текст не виден вовсе: смешивание идёт по альфе, а не по RGB");
    }
}
