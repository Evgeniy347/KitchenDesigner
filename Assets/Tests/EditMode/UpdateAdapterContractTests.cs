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
    public void ThePlayer_HoldsTheRunningInstanceMutex_FromTheFirstMomentForItsWholeLife()
    {
        var marker = Source("RunningInstanceMarker.cs");
        Assert.AreEqual("#if UNITY_STANDALONE_WIN && !UNITY_EDITOR", FirstMeaningfulLine(marker),
            "мьютекс держит только собранный плеер: редактор с ним выглядел бы для установщика "
            + "«запущенной копией», и setup ждал бы закрытия Unity");
        StringAssert.Contains("new Mutex(false, RunningInstanceMutex.Resolve(Environment.GetCommandLineArgs()))", marker,
            "имя по умолчанию — общее с KitchenDesigner.iss (AppMutexName), сверяет InstallerScriptGuardTests; "
            + "аргумент -mutex подменяет его только у дымового прогона установщика, чтобы тот не видел "
            + "приложение пользователя (RunningInstanceMutexTests)");
        StringAssert.Contains("RuntimeInitializeLoadType.SubsystemRegistration", marker,
            "мьютекс создаётся раньше всего остального: установщик, запущенный в первые "
            + "секунды, иначе не увидит копию, которая уже держит файлы");
        StringAssert.Contains("private static Mutex? _held;", marker,
            "ссылка в статическом поле: собранный сборщиком мусора мьютекс закрылся бы задолго "
            + "до выхода процесса, и установщик перестал бы ждать");
    }
    [Test]
    public void InstallerArguments_AreUnderstoodByTheInnoScript()
    {
        StringAssert.Contains("InstallerCommandLine.ForSilentRelaunch(installerPath)",
            Source("InnoUpdateApplier.cs"),
            "адаптер обязан брать командную строку из InstallerCommandLine — ту, что проверена "
            + "InstallerCommandLineTests; своя строка в адаптере не сверяется ни с чем");

        var iss = File.ReadAllText(Path.Combine(InstallerDir(), "KitchenDesigner.iss"));
        StringAssert.Contains("CloseApplications=yes", iss,
            "приложение гасит себя само, но подстраховка обязана остаться: без неё "
            + "Inno упрётся в залоченные нашим процессом файлы");

        var builtIn = new[] { "/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/VERYSILENT" };
        foreach (var arg in KitchenDesigner.Core.Update.InstallerCommandLine.SilentRelaunchSwitches
                     .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (builtIn.Contains(arg, StringComparer.OrdinalIgnoreCase)) continue;
            StringAssert.Contains(arg.TrimStart('/'), iss,
                "ключ " + arg + " придуман нами, а разбирает его KitchenDesigner.iss. "
                + "Ключ без обработки в скрипте = тихо не сработавшее обновление: "
                + "установка пройдёт, приложение не поднимется");
        }
    }

    [Test]
    public void UpdateService_KeepsInstallersInTheOneFixedFolderUnderTheSystemTempPath()
    {
        var source = Source("UpdateService.cs");

        StringAssert.Contains("UpdateFolderLocation.RootUnder(Path.GetTempPath())", source,
            "путь папки обновлений один и считается в одном месте: %TEMP%/KitchenDesigner/Updates. "
            + "Свой путь в UpdateService разошёлся бы с чисткой и со вторым запуском");
        foreach (var file in Directory.GetFiles(UpdateDir(), "*.cs"))
        {
            if (Path.GetFileName(file) == "UpdateService.cs") continue;
            Assert.IsFalse(File.ReadAllText(file).Contains("temporaryCachePath"),
                Path.GetFileName(file) + ": установщики больше не лежат в temporaryCachePath приложения — "
                + "там их не найдёт ни повторный запуск, ни чистка");
        }
    }

    [Test]
    public void UpdateService_BuildsExactlyOneWindow_TheOfferToUpdate_AndNoDownloadWindow()
    {
        var source = Source("UpdateService.cs");

        Assert.AreEqual(1, Regex.Matches(source, @"AddComponent<\w+Dialog\w*>").Count,
            "окно одно — «Доступно обновление». Окна загрузки нет: ход загрузки идёт в консоль");
        StringAssert.Contains("AddComponent<UpdateDialogUI>", source);
        Assert.IsFalse(Directory.GetFiles(UpdateDir(), "*Progress*").Any(),
            "в Core/Update не должно остаться окна хода загрузки");
    }

    [Test]
    public void UpdateService_ShutsTheDownloaderDown_WhenItIsDestroyed()
    {
        var source = Source("UpdateService.cs");

        StringAssert.Contains("_downloader?.Dispose()", source,
            "без этого фоновая загрузка переживает сцену и пишет в .part, когда приложение уже закрывается");
    }

    [Test]
    public void UpdateService_TouchesTheOldCacheOnlyThroughTheOneTimeLegacyCleanup()
    {
        var source = Source("UpdateService.cs");

        Assert.AreEqual(1, Regex.Matches(source, "temporaryCachePath").Count,
            "прежняя папка загрузки упоминается один раз — в разовой уборке; любое другое обращение "
            + "к кэшу приложения вернуло бы установщики туда, где их никто не чистит");
        StringAssert.Contains("LegacyCacheCleanup.Run(legacy, console)", source);
        StringAssert.Contains("preferences.GetInt(LegacyCacheCleanedKey, 0) == 1) return;", source,
            "уборка идёт один раз: пометка читается раньше, чем что-либо удаляется");
        StringAssert.Contains("if (!LegacyCacheCleanup.Run(legacy, console)) return;", source,
            "пометка ставится только после полной уборки: занятый файл повторят при следующем запуске");
    }
}
