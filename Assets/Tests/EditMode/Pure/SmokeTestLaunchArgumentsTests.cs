using System.IO;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Tests.Geometry;

// Сторож договора между скриптом и приложением: сам аргумент разбирается в
// EphemeralSessionArgument, но пользователю от него польза только тогда, когда дымовой
// прогон его ПЕРЕДАЁТ. Дефект был именно такой: прогон подменял пользователю последний
// открытый проект своим временным файлом.
public class SmokeTestLaunchArgumentsTests
{
    private static string ScriptText() =>
        File.ReadAllText(Path.Combine(RepoPaths.Subdir("tools"), "smoke-test.ps1"));

    private static string LaunchLine() =>
        ScriptText().Split('\n').FirstOrDefault(line => line.Contains("-ArgumentList")) ?? "";

    [Test]
    public void SmokeScript_LaunchArguments_CarryEphemeralSession()
    {
        StringAssert.Contains(EphemeralSessionArgument.Name, LaunchLine(),
            "Прогон тестов не трогает ничего из того, что пользователь видит при обычном запуске. "
            + "Без " + EphemeralSessionArgument.Name + " плеер дымовой проверки пишет в PlayerPrefs "
            + "«последний открытый проект», и следующий ОБЫЧНЫЙ запуск открывает "
            + "smoke-roundtrip.save.json из каталога kd-smoke-<хэш> вместо проекта пользователя. "
            + "Вернуть аргумент в строку -ArgumentList в tools/smoke-test.ps1.");
    }

    [Test]
    public void SmokeScript_LaunchArguments_StillCarryTheOtherIsolationFlags()
    {
        var line = LaunchLine();
        Assert.IsNotEmpty(line, "строка -ArgumentList найдена — иначе сторож выше проверяет пустоту");
        StringAssert.Contains(MuteAudioArgument.Name, line, "прогон не шумит на машине пользователя");
        StringAssert.Contains(HideWindowArgument.Name, line, "прогон не показывает окно");
    }
}
