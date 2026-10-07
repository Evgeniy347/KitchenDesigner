using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary><c>Loc.Use</c> меняет ГЛОБАЛЬНЫЙ локализатор, а <c>Loc.Scope</c> даёт потоку
/// собственный. Вопрос «изменился ли язык» (событие <c>LanguageChanged</c>) относится к
/// глобальному языку, и область его не заслоняет: изнутри области нельзя ни пропустить
/// настоящую смену, ни выдать смену, которой не было.
///
/// Эти тесты МЕНЯЮТ глобальный <c>Loc</c> и поэтому лежат вне <c>Pure/</c>: фикстуры
/// чистого слоя идут параллельно под dotnet, и чужой тест увидел бы подменённый
/// локализатор и скачок <c>Loc.Revision</c> (его сверяет <c>AboutRowsTests</c>). EditMode в
/// Unity однопоточный. Область и её потокобезопасность — в <c>LocScopeTests</c>.</summary>
public class LocUseWhileScopedTests
{
    private Localizer _global = null!;
    private int _languageChanges;

    private void CountChange() => _languageChanges++;

    [SetUp]
    public void SetUp()
    {
        _global = Loc.Current;
        _languageChanges = 0;
        Loc.LanguageChanged += CountChange;
    }

    [TearDown]
    public void TearDown()
    {
        Loc.LanguageChanged -= CountChange;
        Loc.Use(_global);
    }

    private string OtherLanguage() => _global.Language == "en" ? "ru" : "en";

    [Test]
    public void Use_WhileScoped_ChangesTheGlobalLocalizer_ButNotTheScope()
    {
        var scoped = _global.WithLanguage(OtherLanguage());
        var replacement = _global.WithLanguage(_global.Language);

        using (Loc.Scope(scoped))
        {
            Loc.Use(replacement);

            Assert.AreSame(scoped, Loc.Current,
                "поток внутри области видит свой локализатор: Use не имеет права его подменить");
        }

        Assert.AreSame(replacement, Loc.Current,
            "а глобальный локализатор Use заменил: после области язык приложения — новый");
    }

    [Test]
    public void Use_WhileScopedToTheNewLanguage_StillReportsTheGlobalChange()
    {
        var other = OtherLanguage();

        using (Loc.Scope(_global.WithLanguage(other)))
            Loc.Use(_global.WithLanguage(other));

        Assert.AreEqual(1, _languageChanges,
            "глобальный язык сменился (" + _global.Language + " → " + other + "), а событие молчит, "
            + "потому что «прежним» посчитали язык области: окна, подписанные на LanguageChanged, "
            + "остались бы на старом языке");
    }

    [Test]
    public void Use_WhileScopedToAnotherLanguage_ReportsNothing_WhenTheGlobalLanguageIsTheSame()
    {
        using (Loc.Scope(_global.WithLanguage(OtherLanguage())))
            Loc.Use(_global.WithLanguage(_global.Language));

        Assert.AreEqual(0, _languageChanges,
            "глобальный язык остался прежним, а событие прозвучало, потому что «прежним» посчитали "
            + "язык области: каждая замена локализатора изнутри области перестраивала бы все окна");
    }
}
