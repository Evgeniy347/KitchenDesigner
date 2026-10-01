using KitchenDesigner.Core;
using NUnit.Framework;
using UnityEngine;

public class SystemLanguageCodeTests
{
    [TestCase(SystemLanguage.Russian, "ru")]
    [TestCase(SystemLanguage.English, "en")]
    [TestCase(SystemLanguage.Arabic, "ar")]
    public void Of_ShippedLanguages_MapToTheirFileNames(SystemLanguage language, string expected)
    {
        Assert.AreEqual(expected, SystemLanguageCode.Of(language),
            "код — имя файла Localization/<код>.json; другое написание оставит систему без перевода");
    }

    [Test]
    public void Of_Unknown_IsNull_SoTheChoiceFallsThrough()
    {
        Assert.IsNull(SystemLanguageCode.Of(SystemLanguage.Unknown));
    }
}
