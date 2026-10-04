using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>EditMode-тесты переключают язык (<c>Loc.SetLanguage("ar-TN")</c>, «ja», «zh-Hans») без
/// <see cref="UIManager"/>, а системные шрифты-запаски к главному шрифту подключает именно он
/// (<c>OsFontFallback.ApplyFor</c> на смене языка). Без них каждая арабская или японская буква —
/// ПРОМАХ: TMP заново ищет её в динамическом LiberationSans, не находит и не запоминает промах, так что
/// одна сборка окна «Настройки» в ar-TN стоила 2,6 с против 0,4 с в ru (замер 2026-10-04,
/// SettingsNavGuardTests — 19 с на 24 теста). Хуже скорости — смысл: сторожа «подпись влезает»
/// мерили ширину квадратиков «нет глифа», а не настоящих букв, которые увидит человек.
/// Здесь тесты получают ту же проводку, что и приложение: язык сменился — запаски подключены.</summary>
[SetUpFixture]
public sealed class ScriptFallbackFontsForEditMode
{
    [OneTimeSetUp]
    public void WireLikeTheApp()
    {
        Loc.LanguageChanged += Apply;
        Apply();
    }

    [OneTimeTearDown]
    public void Unwire()
    {
        Loc.LanguageChanged -= Apply;
        OsFontFallback.ApplyFor(Localizer.SourceLanguage, UIFactory.FontAsset);
    }

    private static void Apply() => OsFontFallback.ApplyFor(Loc.Language, UIFactory.FontAsset);
}
