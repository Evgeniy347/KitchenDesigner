using NUnit.Framework;
using KitchenDesigner.Core;

public class EphemeralSessionArgumentTests
{
    [Test]
    public void Parse_ReturnsFalse_WhenArgsIsNull()
    {
        Assert.IsFalse(EphemeralSessionArgument.Parse(null));
    }

    [Test]
    public void Parse_ReturnsFalse_WhenArgsIsEmpty()
    {
        Assert.IsFalse(EphemeralSessionArgument.Parse(new string[0]));
    }

    [Test]
    public void Parse_ReturnsFalse_WhenTheFlagIsAbsent()
    {
        Assert.IsFalse(EphemeralSessionArgument.Parse(new[] { "-mcpPort", "9337" }),
            "обычный запуск пользователя ничего не знает о режиме прогона");
    }

    [Test]
    public void Parse_ReturnsTrue_WhenTheFlagIsPresent()
    {
        Assert.IsTrue(EphemeralSessionArgument.Parse(new[] { "-ephemeralSession" }));
    }

    [Test]
    public void Parse_ReturnsTrue_WhenTheFlagIsMixedWithOtherArguments()
    {
        Assert.IsTrue(EphemeralSessionArgument.Parse(
            new[] { "-mcpPort", "19881", "-muteAudio", "-hideWindow", "-ephemeralSession" }));
    }

    [Test]
    public void Parse_IsCaseInsensitive_OnTheFlagName()
    {
        Assert.IsTrue(EphemeralSessionArgument.Parse(new[] { "-EPHEMERALSESSION" }));
    }

    [Test]
    public void Name_MatchesTheSpellingTheSmokeScriptPasses()
    {
        Assert.AreEqual("-ephemeralSession", EphemeralSessionArgument.Name,
            "имя аргумента — часть договора с tools/smoke-test.ps1: переименование ломает прогон молча");
    }
}
