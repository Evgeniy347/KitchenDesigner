using System.Globalization;
using System.Linq;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class LanguageStartup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void PointAtStreamingAssets() =>
            LocalizationFiles.Directory = Application.streamingAssetsPath + "/" + LocalizationFiles.FolderName;

        public static void PinSourceLanguageForTestRun() => Loc.SetLanguage(Localizer.SourceLanguage);

        public static void Apply()
        {
            PointAtStreamingAssets();
            if (Loc.Languages.Count == 0) Loc.Reload();
            var available = Loc.Languages.Select(l => l.Code).ToList();
            var chosen = LanguageChoice.Decide(
                LanguagePreference.Load(),
                new[] { SafeUiCulture(), SystemLanguageCode.Of(Application.systemLanguage) },
                available,
                Audio.AudioOutputPolicy.UnderTestRun);
            Loc.SetLanguage(chosen);
        }

        public static void Choose(string language)
        {
            LanguagePreference.Save(language);
            Loc.SetLanguage(language);
        }

        private static string? SafeUiCulture()
        {
            try { return CultureInfo.CurrentUICulture.Name; }
            catch (CultureNotFoundException) { return null; }
        }
    }
}
