using NUnit.Framework;
using KitchenDesigner.Core.Update;

public class RunningInstanceMutexTests
{
    [Test]
    public void Resolve_ReturnsTheSharedName_WhenArgsIsNull()
    {
        Assert.AreEqual(RunningInstanceMutex.Name, RunningInstanceMutex.Resolve(null));
    }

    [Test]
    public void Resolve_ReturnsTheSharedName_ForAnOrdinaryLaunch()
    {
        Assert.AreEqual(RunningInstanceMutex.Name,
            RunningInstanceMutex.Resolve(new[] { "KitchenDesigner.exe", "C:/p/a.kdproj" }),
            "обычный запуск и запуск по двойному щелчку на .kdproj держат ТО ЖЕ имя, что ждёт установщик");
    }

    [Test]
    public void Resolve_TakesTheNameFollowingTheSwitch()
    {
        Assert.AreEqual("KitchenDesigner.RunningInstance.smoke-1",
            RunningInstanceMutex.Resolve(new[] { "-mcpPort", "19883", "-mutex", "KitchenDesigner.RunningInstance.smoke-1", "-muteAudio" }),
            "дымовой прогон установщика подменяет имя, чтобы его setup и его приложение видели только друг друга, "
            + "а не открытое приложение пользователя");
    }

    [Test]
    public void Resolve_IsCaseInsensitive_OnTheSwitch()
    {
        Assert.AreEqual("x", RunningInstanceMutex.Resolve(new[] { "-MUTEX", "x" }));
    }

    [Test]
    public void Resolve_FallsBackToTheSharedName_WhenTheSwitchHasNoValue()
    {
        Assert.AreEqual(RunningInstanceMutex.Name, RunningInstanceMutex.Resolve(new[] { "-mcpPort", "1", "-mutex" }),
            "ключ без значения не должен давать мьютекс с пустым именем: тот безымянный и установщик его не увидит");
        Assert.AreEqual(RunningInstanceMutex.Name, RunningInstanceMutex.Resolve(new[] { "-mutex", " " }));
    }

    [Test]
    public void ArgumentName_MatchesTheSpellingTheInstallerSmokePasses()
    {
        Assert.AreEqual("-mutex", RunningInstanceMutex.ArgumentName,
            "имя ключа — часть договора с tools/installer-smoke.ps1 и KitchenDesigner.iss (/APPARGS): переименование ломает прогон молча");
    }
}
