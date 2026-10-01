using System;
using System.Collections.Generic;
using System.IO;
using KitchenDesigner.Core;
using NUnit.Framework;

public class ScriptFallbackFontsTests
{
    private static readonly string WindowsFonts = Path.Combine("C:", "W", "Fonts");
    private static readonly string UserFonts = Path.Combine("C:", "U", "Fonts");
    private static readonly string[] Directories = { WindowsFonts, UserFonts };

    private static Func<string, bool> Present(params string[] paths)
    {
        var set = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        return set.Contains;
    }

    [TestCase("ar-TN", ScriptFallbackFonts.Script.Arabic)]
    [TestCase("ar", ScriptFallbackFonts.Script.Arabic)]
    [TestCase("zh-Hans", ScriptFallbackFonts.Script.SimplifiedChinese)]
    [TestCase("ja", ScriptFallbackFonts.Script.Japanese)]
    [TestCase("ru", ScriptFallbackFonts.Script.None)]
    [TestCase("de", ScriptFallbackFonts.Script.None)]
    [TestCase(null, ScriptFallbackFonts.Script.None)]
    public void ScriptFallbackFonts_For_PicksTheScriptByThePrimaryTag(string? language, ScriptFallbackFonts.Script expected)
    {
        Assert.AreEqual(expected, ScriptFallbackFonts.For(language));
    }

    [Test]
    public void ScriptFallbackFonts_Resolve_Japanese_PrefersJapaneseFaces_InOrder_UpToTheLimit()
    {
        var found = ScriptFallbackFonts.Resolve("ja", Directories, Present(
            Path.Combine(WindowsFonts, "msyh.ttc"),
            Path.Combine(WindowsFonts, "msgothic.ttc"),
            Path.Combine(WindowsFonts, "YuGothR.ttc")));

        CollectionAssert.AreEqual(new[] { Path.Combine(WindowsFonts, "YuGothR.ttc"), Path.Combine(WindowsFonts, "msgothic.ttc") }, found,
            "иероглифы у японского и китайского общие, а начертания разные: японский интерфейс берёт японскую гарнитуру, "
            + "китайскую — только когда японских нет");
    }

    [Test]
    public void ScriptFallbackFonts_Resolve_Chinese_TakesYaHeiFirst()
    {
        var found = ScriptFallbackFonts.Resolve("zh-Hans", Directories, Present(
            Path.Combine(WindowsFonts, "simsun.ttc"), Path.Combine(WindowsFonts, "msyh.ttc")));
        Assert.AreEqual(Path.Combine(WindowsFonts, "msyh.ttc"), found[0]);
    }

    [Test]
    public void ScriptFallbackFonts_Resolve_FontInstalledForTheUserOnly_IsFound()
    {
        var found = ScriptFallbackFonts.Resolve("ar-TN", Directories, Present(Path.Combine(UserFonts, "tahoma.ttf")));
        CollectionAssert.AreEqual(new[] { Path.Combine(UserFonts, "tahoma.ttf") }, found,
            "шрифт, поставленный без прав администратора, лежит в профиле, а не в Windows\\Fonts");
    }

    [Test]
    public void ScriptFallbackFonts_Resolve_NothingInstalled_IsEmpty_NotAnError()
    {
        Assert.IsEmpty(ScriptFallbackFonts.Resolve("ja", Directories, _ => false),
            "нет шрифта — значит квадраты и запись в журнал, но не исключение при сборке интерфейса");
    }

    [Test]
    public void ScriptFallbackFonts_Resolve_LatinOrCyrillic_DoesNotTouchTheDisk()
    {
        Assert.IsEmpty(ScriptFallbackFonts.Resolve("ru", Directories, _ => throw new AssertionException("диск не спрашивается")),
            "русскому и английскому хватает LiberationSans — системные шрифты не грузятся вовсе");
    }

    [Test]
    public void ScriptFallbackFonts_WindowsFontDirectories_MissingVariables_AreSkippedByResolve()
    {
        var directories = ScriptFallbackFonts.WindowsFontDirectories(_ => null);
        Assert.IsEmpty(ScriptFallbackFonts.Resolve("ar", directories, _ => true),
            "без WINDIR (WebGL, Linux) искать негде — пустой список, а не путь от корня");
    }

    [Test]
    public void ScriptFallbackFonts_WindowsFontDirectories_SystemThenUser()
    {
        var env = new Dictionary<string, string> { ["WINDIR"] = "C:\\Windows", ["LOCALAPPDATA"] = "C:\\Users\\a\\AppData\\Local" };
        CollectionAssert.AreEqual(
            new[] { Path.Combine("C:\\Windows", "Fonts"), Path.Combine("C:\\Users\\a\\AppData\\Local", "Microsoft", "Windows", "Fonts") },
            ScriptFallbackFonts.WindowsFontDirectories(name => env.TryGetValue(name, out var v) ? v : null));
    }
}
