using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using KitchenDesigner.Tests.Geometry;

/// <summary>Каталог Core/Update — это адаптеры к внешнему миру: сеть, диск,
/// чужой процесс. Проверять их поведением в тестах нельзя (сеть дёргать нечем,
/// установщик запускать некуда), поэтому здесь проверяется КОНТРАКТ по
/// исходникам: где именно эти адаптеры компилируются и с чем они обязаны
/// совпадать. Знание из снятых комментариев Core/Update живёт тут.</summary>
public class UpdateAdapterContractTests
{
    private static string UpdateDir() => RepoPaths.Subdir("Assets", "Scripts", "Core", "Update");
    private static string InstallerDir() => RepoPaths.Subdir("installer");

    private static string Source(string fileName)
    {
        var path = Path.Combine(UpdateDir(), fileName);
        Assert.IsTrue(File.Exists(path),
            "скан ищет исходник, которого нет: " + path
            + " — по несуществующему пути тест зеленеет, ничего не проверив");
        var text = File.ReadAllText(path);
        Assert.IsNotEmpty(text, fileName + " пуст — сканировать нечего");
        return text;
    }

    private static string FirstMeaningfulLine(string source) =>
        source.Split('\n').Select(l => l.Trim()).First(l => l.Length > 0);

    [Test]
    public void TheScan_SeesTheUpdateSourcesAndTheInstallerScript()
    {
        var sources = Directory.GetFiles(UpdateDir(), "*.cs");
        Assert.GreaterOrEqual(sources.Length, 8,
            "скан не видит исходников Core/Update — дальше он зеленел бы впустую");
        CollectionAssert.Contains(sources.Select(Path.GetFileName).ToList(),
            "InnoUpdateApplier.cs", "адаптер установщика обязан быть в сканируемом наборе");
        Assert.IsTrue(File.Exists(Path.Combine(InstallerDir(), "KitchenDesigner.iss")),
            "скрипт Inno Setup — вторая половина контракта автообновления");
    }

    [Test]
    public void NetworkAdapters_AreCompiledOutOfTheWebGlBuild()
    {
        foreach (var file in new[]
                 {
                     "GitHubReleaseChecker.cs", "UnityWebRequestDownloader.cs", "UpdateService.cs",
                 })
            Assert.AreEqual("#if !UNITY_WEBGL", FirstMeaningfulLine(Source(file)),
                file + ": в WebGL автообновления нет вовсе — установщика там некуда ставить, "
                + "и UnityWebRequest к GitHub из браузера упёрся бы в CORS. "
                + "Класс не должен попадать в WebGL-сборку даже мёртвым кодом");
    }

    [Test]
    public void InstallerApplier_IsCompiledOnlyIntoTheWindowsPlayer()
    {
        Assert.AreEqual("#if UNITY_STANDALONE_WIN && !UNITY_EDITOR",
            FirstMeaningfulLine(Source("InnoUpdateApplier.cs")),
            "InnoUpdateApplier запускает процесс и гасит приложение. В редакторе и в "
            + "тестах он не должен даже компилироваться: иначе один случайный вызов "
            + "закрывает Unity вместе с прогоном");
    }

    [Test]
    public void UpdateService_WiresTheRealAdapters_OnlyInTheWindowsPlayer()
    {
        var source = Source("UpdateService.cs");
        int wiring = source.IndexOf("AddComponent<GitHubReleaseChecker>", StringComparison.Ordinal);
        Assert.Greater(wiring, 0, "UpdateService должен создавать сетевой чекер сам");

        bool guarded = Regex.Matches(source,
                @"#if UNITY_STANDALONE_WIN && !UNITY_EDITOR.*?#endif", RegexOptions.Singleline)
            .Cast<Match>()
            .Any(m => wiring > m.Index && wiring < m.Index + m.Length);

        Assert.IsTrue(guarded,
            "создание сетевого чекера обязано лежать ВНУТРИ "
            + "#if UNITY_STANDALONE_WIN && !UNITY_EDITOR: вне его редактор и тестовый "
            + "прогон начнут стучаться на GitHub при каждом старте сцены");
    }

    [Test]
    public void UpdateService_StartupCheck_IsOffWhileTestsRun()
    {
        StringAssert.Contains("if (!StartupCheckEnabled) return;", Source("UpdateService.cs"),
            "без этой проверки случайно созданный в прогоне UpdateService уходит в сеть");

        var config = Path.Combine(RepoPaths.Subdir("Assets", "Tests", "PlayMode"),
            "PlayModeTestConfig.cs");
        Assert.IsTrue(File.Exists(config), "PlayModeTestConfig — тот, кто гасит флаг: " + config);
        StringAssert.Contains("UpdateService.StartupCheckEnabled = false", File.ReadAllText(config),
            "флаг и тот, кто его гасит, — две половины одной договорённости: "
            + "остался только флаг — PlayMode-прогон снова начнёт стучаться на GitHub");
    }

    [Test]
    public void InstallerArguments_AreUnderstoodByTheInnoScript()
    {
        var applier = Source("InnoUpdateApplier.cs");
        var literal = Regex.Match(applier,
            @"SilentRelaunchArguments\s*=\s*""(?<args>[^""]*)""", RegexOptions.Singleline);
        Assert.IsTrue(literal.Success,
            "аргументы установщика должны быть именованной константой — иначе их не с чем сверять");

        var iss = File.ReadAllText(Path.Combine(InstallerDir(), "KitchenDesigner.iss"));
        StringAssert.Contains("CloseApplications=yes", iss,
            "приложение гасит себя само, но подстраховка обязана остаться: без неё "
            + "Inno упрётся в залоченные нашим процессом файлы");

        var builtIn = new[] { "/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/VERYSILENT" };
        foreach (var arg in literal.Groups["args"].Value
                     .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (builtIn.Contains(arg, StringComparer.OrdinalIgnoreCase)) continue;
            StringAssert.Contains(arg.TrimStart('/'), iss,
                "ключ " + arg + " придуман нами, а разбирает его KitchenDesigner.iss. "
                + "Ключ без обработки в скрипте = тихо не сработавшее обновление: "
                + "установка пройдёт, приложение не поднимется");
        }
    }
}
