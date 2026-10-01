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
}