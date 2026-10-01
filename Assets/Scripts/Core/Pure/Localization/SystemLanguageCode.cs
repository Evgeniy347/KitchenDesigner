using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SystemLanguageCode
    {
        public static string? Of(SystemLanguage language) => language switch
        {
            SystemLanguage.Russian => "ru",
            SystemLanguage.English => "en",
            SystemLanguage.Arabic => "ar",
            SystemLanguage.Ukrainian => "uk",
            SystemLanguage.Belarusian => "be",
            SystemLanguage.German => "de",
            SystemLanguage.French => "fr",
            SystemLanguage.Spanish => "es",
            SystemLanguage.Italian => "it",
            SystemLanguage.Portuguese => "pt",
            SystemLanguage.Polish => "pl",
            SystemLanguage.Czech => "cs",
            SystemLanguage.Turkish => "tr",
            SystemLanguage.Hebrew => "he",
            SystemLanguage.Chinese => "zh",
            SystemLanguage.ChineseSimplified => "zh",
            SystemLanguage.ChineseTraditional => "zh",
            SystemLanguage.Japanese => "ja",
            SystemLanguage.Korean => "ko",
            _ => null,
        };
    }
}
