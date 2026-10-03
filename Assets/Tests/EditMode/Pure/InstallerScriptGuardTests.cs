#nullable disable
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using KitchenDesigner.Tests.Geometry;

/// <summary>
/// Статические сторожа над installer/KitchenDesigner.iss. Сам установщик в тестах не
/// запускается (это делает tools/installer-smoke.ps1 на пути релиза), но две ошибки в
/// скрипте видны и по тексту — и обе кончаются одинаково: тихий /SILENT-установщик
/// автообновления пишет «Rolling back changes», закрывается, обновление не встаёт.
/// </summary>
public class InstallerScriptGuardTests
{
    private static string Iss()
    {
        var path = Path.Combine(RepoPaths.Subdir("installer"), "KitchenDesigner.iss");
        Assert.IsTrue(File.Exists(path), "скан ищет скрипт установщика, которого нет: " + path);
        return File.ReadAllText(path);
    }

    private static string Section(string iss, string name)
    {
        var m = Regex.Match(iss, @"^\[" + Regex.Escape(name) + @"\]\s*$(?<body>.*?)(?=^\[[A-Za-z]+\]\s*$|\z)",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.IsTrue(m.Success, "в KitchenDesigner.iss нет секции [" + name + "]");
        return m.Groups["body"].Value;
    }

    private static string PrivilegesRequired(string iss)
    {
        var m = Regex.Match(Section(iss, "Setup"), @"^\s*PrivilegesRequired\s*=\s*(?<v>\w+)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return m.Success ? m.Groups["v"].Value.ToLowerInvariant() : "admin";
    }

    private static string[] AllowedRegistryRoots(string privileges) =>
        privileges == "lowest"
            ? new[] { "HKCU", "HKA" }
            : new[] { "HKCU", "HKA", "HKLM", "HKCR", "HKU", "HKCC" };

    [Test]
    public void EveryRegistryRoot_IsWritableAtThePrivilegeLevelTheInstallerRunsWith()
    {
        var iss = Iss();
        var privileges = PrivilegesRequired(iss);
        var roots = Regex.Matches(Section(iss, "Registry"), @"^\s*Root:\s*(?<root>\w+)",
                RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Cast<Match>()
            .Select(m => m.Groups["root"].Value.ToUpperInvariant())
            .ToList();

        Assert.IsNotEmpty(roots,
            "в [Registry] не найдено ни одной строки Root: — сторож зеленел бы впустую "
            + "(ассоциация .kdproj обязана там быть)");

        var allowed = AllowedRegistryRoots(privileges);
        foreach (var root in roots)
        {
            var bare = Regex.Replace(root, "(32|64)$", "");
            CollectionAssert.Contains(allowed, bare,
                "Root: " + root + " при PrivilegesRequired=" + privileges + ". Установка per-user "
                + "идёт БЕЗ прав администратора: запись в HKLM/HKCR даёт «доступ запрещён», и Inno "
                + "откатывает всю установку. Для per-user — HKCU (или HKA, которое Inno сам "
                + "сводит к HKCU)");
        }
    }

    private static string WaitFunction(string code)
    {
        var m = Regex.Match(code, @"function\s+WaitForTheUpdatingAppToExit\b.*?^end;",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.IsTrue(m.Success, "в [Code] нет WaitForTheUpdatingAppToExit");
        return m.Value;
    }

    [Test]
    public void SilentUpdate_WaitsForTheOldCopy_BeforeTheInUseCheck()
    {
        var iss = Iss();
        var code = Section(iss, "Code");
        var prepare = Regex.Match(code,
            @"function\s+PrepareToInstall\b.*?^end;", RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.IsTrue(prepare.Success, "в [Code] нет PrepareToInstall — ждать выхода старой копии негде");

        StringAssert.Contains("if RelaunchRequested and not WaitForTheUpdatingAppToExit then", prepare.Value,
            "автообновление стартует setup ДО того, как Unity-плеер успел выйти. Restart Manager "
            + "закрыть плеер и UnityCrashHandler64 не может («Some applications could not be shut "
            + "down»), под /SUPPRESSMSGBOXES ответ по умолчанию — Abort, и setup пишет «Rolling back "
            + "changes». PrepareToInstall — последняя точка ДО проверки занятых файлов: ждать надо в ней");

        StringAssert.Contains("CloseApplications=yes", Section(iss, "Setup"),
            "ожидание не отменяет страховку Restart Manager: без неё зависшая копия "
            + "уронит установку на занятых файлах без всякой попытки её закрыть");
    }

    [Test]
    public void TheWait_ListensToTheMutexTheAppHolds_AndStillChecksFilesForOlderVersions()
    {
        var iss = Iss();
        var name = Regex.Match(iss, @"#define\s+AppMutexName\s+""(?<n>[^""]+)""");
        Assert.IsTrue(name.Success, "имя мьютекса приложения обязано быть #define AppMutexName");
        Assert.AreEqual(KitchenDesigner.Core.Update.RunningInstanceMutex.Name, name.Groups["n"].Value,
            "установщик ждёт мьютекс по имени: разойдись имена — ожидание видит «не запущено», "
            + "пока плеер ещё выходит, и обновление снова откатывается");

        var code = Section(iss, "Code");
        StringAssert.Contains("CheckForMutexes('{#AppMutexName}')", code,
            "мьютекс снимает Windows, когда процесс ЗАВЕРШИЛСЯ — это точный сигнал «старая копия ушла»");
        StringAssert.Contains(@"FileHeldByRunningProcess(Dir + '\{#AppExe}')", code,
            "выпущенные версии до 0.2040 мьютекса не создают, а обновляться будут именно они; "
            + "UnityCrashHandler64 ещё и переживает плеер на миг — файлы остаются вторым сигналом");
    }

    [Test]
    public void TheWait_IsVisible_InTheInstallersLanguage()
    {
        var iss = Iss();
        var messages = Section(iss, "CustomMessages");
        StringAssert.Contains("ru.AppCloseWaitStatus=Ожидаем закрытия приложения…", messages,
            "пользователь только что нажал «Обновить», окно приложения закрылось — без этой "
            + "строки он видит пустой прогресс и запускает старую копию поверх установки");
        StringAssert.Contains("en.AppCloseWaitStatus=", messages, "английский setup тоже говорит, чего ждёт");

        var wait = WaitFunction(Section(iss, "Code"));
        StringAssert.Contains("AppCloseWaitPage.Show;", wait,
            "текст показывается страницей прогресса ВО ВРЕМЯ ожидания, а не после него");
        StringAssert.Contains("CustomMessage('AppCloseWaitStatus')", wait,
            "строка берётся из [CustomMessages] — на языке установщика, а не вшитая по-русски");
    }

    [Test]
    public void TheWait_HasNoDeadline_ItAsksTheUserToCloseTheApp_AgainAndAgain()
    {
        var iss = Iss();
        var m = Regex.Match(iss, @"#define\s+RelaunchAskAfterSec\s+(?<s>\d+)");
        Assert.IsTrue(m.Success, "через сколько ожидание превращается в вопрос — именованная величина");
        Assert.That(int.Parse(m.Groups["s"].Value), Is.InRange(8, 60),
            "раньше ~8 с вопрос выскакивает поверх обычного выхода плеера (замер ~4–9 с); "
            + "позже минуты пользователь решает, что всё зависло");

        StringAssert.DoesNotContain("RelaunchWaitSec", iss,
            "срока у ожидания нет: «не закрылся за 60 секунд» пользователь видел, когда плеер "
            + "падал при выходе и висел в обработке сбоя, — обновление при этом пропадало зря");
        StringAssert.DoesNotContain("AppCloseTimeout", iss, "ошибки по истечении срока больше нет");

        var wait = WaitFunction(Section(iss, "Code"));
        StringAssert.Contains("MsgBox(CustomMessage('AppCloseAsk'), mbError, MB_OKCANCEL)", wait,
            "вопрос — обычный MsgBox: /SUPPRESSMSGBOXES глушит только SuppressibleMsgBox, а этот "
            + "вопрос пользователь обязан увидеть");
        StringAssert.Contains("until Cancelled or not AppStillRunningAfter(", wait,
            "ОК — проверить снова и спросить снова, сколько угодно раз; выйти из цикла можно "
            + "только закрыв приложение или нажав Отмена");
        StringAssert.Contains("ru.AppCloseAsk=Закройте {#AppName} и нажмите ОК.", Section(iss, "CustomMessages"));
    }

    [Test]
    public void Cancel_LeavesTheOldVersion_NothingIsCopiedSoNothingRollsBack()
    {
        var prepare = Regex.Match(Section(Iss(), "Code"),
            @"function\s+PrepareToInstall\b.*?^end;", RegexOptions.Singleline | RegexOptions.Multiline).Value;
        StringAssert.Contains("Result := CustomMessage('AppCloseCancelled');", prepare,
            "непустой результат PrepareToInstall останавливает setup ДО копирования: старая "
            + "версия на месте, откатывать нечего, а в логе остаётся причина");
    }

    [Test]
    public void ManualInstall_UsesInnosOwnAppMutexCheck_ButTheUpdateDoesNot()
    {
        var iss = Iss();
        StringAssert.Contains("AppMutex={code:AppMutexUnlessUpdating}", Section(iss, "Setup"),
            "ручная установка и деинсталляция при открытом приложении получают штатный "
            + "диалог Inno «закройте приложение — ОК / Отмена»");
        StringAssert.Contains("if RelaunchRequested then Result := '' else Result := '{#AppMutexName}';",
            Section(iss, "Code"),
            "при /RELAUNCH проверка Inno на старте ловила бы ещё закрывающееся приложение, а "
            + "под /SUPPRESSMSGBOXES ответила бы Отмена — обновление снова не вставало бы. "
            + "Его ждёт WaitForTheUpdatingAppToExit");
    }

    private static string[] ShippedAppLanguages() =>
        Directory.GetFiles(RepoPaths.Subdir("Assets", "StreamingAssets", "Localization"), "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !name!.StartsWith("_"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;

    private static string[] InstallerLanguageNames(string iss) =>
        Regex.Matches(Section(iss, "Languages"), @"^\s*Name:\s*""(?<n>\w+)""", RegexOptions.Multiline)
            .Cast<Match>().Select(m => m.Groups["n"].Value).ToArray();

    private static string[] AppCodesOfTheInstallerLanguages(string iss) =>
        Regex.Matches(Section(iss, "CustomMessages"), @"^\w+\.AppLanguageCode=(?<c>\S+)\s*$", RegexOptions.Multiline)
            .Cast<Match>().Select(m => m.Groups["c"].Value).OrderBy(c => c, StringComparer.Ordinal).ToArray();

    [Test]
    public void Languages_OfferTheSameTenTheAppShips_AndTheDialogAppearsOnlyWhenNoneMatchesTheOs()
    {
        var iss = Iss();
        CollectionAssert.AreEqual(ShippedAppLanguages(), AppCodesOfTheInstallerLanguages(iss),
            "каждому файлу Localization/<код>.json нужен язык в установщике с AppLanguageCode=<код>: "
            + "иначе новый язык приложения нельзя выбрать при установке, а выбранный не доедет до приложения");
        Assert.AreEqual(10, InstallerLanguageNames(iss).Length, "в [Languages] ровно десять языков");

        var setup = Section(iss, "Setup");
        StringAssert.Contains("ShowLanguageDialog=auto", setup,
            "auto: диалог только когда язык ОС не подошёл ни к одному из десяти; no лишал выбора, yes спрашивал бы каждого");
        StringAssert.Contains("LanguageDetectionMethod=uilanguage", setup,
            "язык по умолчанию — язык интерфейса Windows");
    }

    [Test]
    public void EveryInstallerLanguage_TranslatesEveryOwnMessage()
    {
        var messages = Section(Iss(), "CustomMessages");
        var names = InstallerLanguageNames(Iss());
        foreach (var language in names)
            foreach (var key in new[] { "AppLanguageCode", "AppCloseWaitCaption", "AppCloseWaitStatus",
                         "AppCloseAsk", "AppCloseCancelled", "ProjectFileType" })
                Assert.IsTrue(Regex.IsMatch(messages, "^" + language + @"\." + key + "=", RegexOptions.Multiline),
                    language + "." + key + " не задан: без него Inno берёт строку «первого языка, где она есть» "
                    + "— то есть русскую, и японец увидит русский вопрос о закрытии приложения");
    }

    [Test]
    public void ChineseSimplified_HasAnIdentityFile_BecauseInnoShipsNoTranslation()
    {
        var iss = Iss();
        StringAssert.Contains(@"Name: ""zhHans""; MessagesFile: ""Languages\ChineseSimplified.isl""", Section(iss, "Languages"));
        var isl = File.ReadAllText(Path.Combine(RepoPaths.Subdir("installer", "Languages"), "ChineseSimplified.isl"));
        StringAssert.Contains("LanguageID=$0804", isl,
            "LCID упрощённого китайского: по нему автоопределение по языку ОС выбирает этот язык, а не английский");
        StringAssert.Contains("[CustomMessages]", isl,
            "встроенные строки Inno (ярлык на рабочем столе и т. д.) без этой секции берутся у первого языка — русского");
    }

    [Test]
    public void InstallLanguage_IsAPlainPerUserString_ThatSilentUpdatesNeverOverwrite()
    {
        var iss = Iss();
        var line = Regex.Match(Section(iss, "Registry"), @"^Root:\s*HKCU;\s*Subkey:\s*""Software\\KitchenDesigner"";.*?(?=^Root:|\z)",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.IsTrue(line.Success, @"в [Registry] нет записи HKCU\Software\KitchenDesigner (язык установщика для приложения)");
        var entry = Regex.Replace(line.Value, @"\\\s*\r?\n\s*", " ");

        StringAssert.Contains(@"Subkey: """ + KitchenDesigner.Core.InstallLanguage.RegistryKey + @"""", entry,
            "ключ тот же, что читает приложение (InstallLanguage.RegistryKey)");
        StringAssert.Contains(@"ValueName: """ + KitchenDesigner.Core.InstallLanguage.ValueName + @"""", entry);
        StringAssert.Contains("ValueType: string", entry,
            "простая REG_SZ; бинарное значение с хэшированным именем Unity из Inno писать хрупко");
        StringAssert.Contains(@"ValueData: ""{cm:AppLanguageCode}""", entry,
            "значение — код языка приложения выбранного языка установщика");
        StringAssert.Contains("Check: ShouldWriteInstallLanguage", entry);
        StringAssert.Contains("uninsdeletevalue", entry, "деинсталляция убирает свой след");

        var code = Section(iss, "Code");
        var check = Regex.Match(code, @"function\s+ShouldWriteInstallLanguage.*?^end;",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.IsTrue(check.Success, "в [Code] нет ShouldWriteInstallLanguage");
        StringAssert.Contains("WizardSilent", check.Value,
            "тихое автообновление выбирает язык по ОС, а не человек — оно не должно решать за него");
        StringAssert.Contains("RegValueExists(HKCU", check.Value,
            "тихая установка пишет значение только если его ещё нет (первая установка)");

        StringAssert.DoesNotContain("Language_h", iss,
            "значение PlayerPrefs «Language» (REG_BINARY, имя с хэшем) установщик не пишет: приоритет и перенос делает приложение");
        StringAssert.DoesNotContain("DefaultCompany", iss);
    }

    // setup.exe и KitchenDesigner.exe обязаны показывать одну иконку. Источник один —
    // Assets/Art/Icon/shipped (пишет tools/render-icons.mjs --ship): ISCC берёт оттуда app.ico,
    // Unity — icon-<size>.png через ProjectSettings. Сторож ловит смену одной половины без
    // другой: SetupIconFile на несуществующий файл (ISCC падает только на релизе) или
    // ProjectSettings, ссылающийся на текстуру вне папки shipped.
    [Test]
    public void SetupIcon_AndTheExeIcon_ComeFromTheSameShippedFolder()
    {
        var installerDir = RepoPaths.Subdir("installer");
        var setupIcon = Regex.Match(Section(Iss(), "Setup"), @"^\s*SetupIconFile\s*=\s*(?<p>.+?)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        Assert.IsTrue(setupIcon.Success, "в [Setup] нет SetupIconFile — setup.exe ушёл бы со стандартной иконкой Inno");

        var icoPath = Path.GetFullPath(Path.Combine(installerDir, setupIcon.Groups["p"].Value.Replace('\\', Path.DirectorySeparatorChar)));
        Assert.IsTrue(File.Exists(icoPath), "SetupIconFile указывает на несуществующий файл: " + icoPath);

        var shippedDir = Path.GetDirectoryName(icoPath);
        var shippedGuids = Directory.GetFiles(shippedDir, "*.png.meta")
            .Select(meta => Regex.Match(File.ReadAllText(meta), @"^guid:\s*(?<g>\w+)", RegexOptions.Multiline).Groups["g"].Value)
            .ToArray();
        Assert.IsNotEmpty(shippedGuids, "рядом с app.ico нет растров для ProjectSettings: " + shippedDir);

        var settings = File.ReadAllText(Path.Combine(RepoPaths.Subdir("ProjectSettings"), "ProjectSettings.asset"));
        var iconBlocks = Regex.Match(settings, @"^  m_BuildTargetIcons:(?<b>.*?)^  m_BuildTargetBatching:",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.IsTrue(iconBlocks.Success, "в ProjectSettings.asset не найдены m_BuildTargetIcons/m_BuildTargetPlatformIcons");
        var referenced = Regex.Matches(iconBlocks.Groups["b"].Value, @"guid:\s*(?<g>\w+)")
            .Cast<Match>().Select(m => m.Groups["g"].Value).ToArray();
        Assert.IsNotEmpty(referenced, "в ProjectSettings не задано ни одной иконки Standalone");
        foreach (var guid in referenced)
            CollectionAssert.Contains(shippedGuids, guid,
                "иконка exe в ProjectSettings ссылается на текстуру вне " + shippedDir + " — exe и setup.exe разойдутся");
    }

    [Test]
    public void InstallLanguage_RegistryLocationIsInTheAppsOwnHiveAndKey()
    {
        Assert.AreEqual(@"Software\KitchenDesigner", KitchenDesigner.Core.InstallLanguage.RegistryKey);
        Assert.AreEqual("InstallLanguage", KitchenDesigner.Core.InstallLanguage.ValueName);
    }
}
