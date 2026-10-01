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

    [Test]
    public void SilentUpdate_WaitsForTheOldCopyToReleaseItsFiles_BeforeTheInUseCheck()
    {
        var iss = Iss();
        var code = Section(iss, "Code");
        var prepare = Regex.Match(code,
            @"function\s+PrepareToInstall\b.*?^end;", RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.IsTrue(prepare.Success, "в [Code] нет PrepareToInstall — ждать выхода старой копии негде");

        StringAssert.Contains("if RelaunchRequested then WaitForTheUpdatingAppToExit;", prepare.Value,
            "автообновление стартует setup ДО того, как Unity-плеер успел выйти. Restart Manager "
            + "закрыть плеер и UnityCrashHandler64 не может («Some applications could not be shut "
            + "down»), под /SUPPRESSMSGBOXES ответ по умолчанию — Abort, и setup пишет «Rolling back "
            + "changes». PrepareToInstall — последняя точка ДО проверки занятых файлов: ждать надо в ней");

        StringAssert.Contains(@"FileHeldByRunningProcess(Dir + '\{#AppExe}')", code,
            "ждать надо именно исполняемый файл приложения: его держит старая копия до самого выхода");

        StringAssert.Contains("CloseApplications=yes", Section(iss, "Setup"),
            "ожидание не отменяет страховку Restart Manager: без неё зависшая копия "
            + "уронит установку на занятых файлах без всякой попытки её закрыть");
    }

    [Test]
    public void RelaunchWait_IsBoundedSoAHungCopyCannotHangTheInstallerForever()
    {
        var m = Regex.Match(Iss(), @"#define\s+RelaunchWaitMs\s+(?<ms>\d+)");
        Assert.IsTrue(m.Success, "срок ожидания выхода старой копии обязан быть именованным");
        var ms = int.Parse(m.Groups["ms"].Value);
        Assert.That(ms, Is.InRange(10000, 120000),
            "меньше 10 с — Unity-плеер с D3D12 не успевает выйти (замер: ~4 с, бывает дольше); "
            + "больше 2 мин — пользователь смотрит на невидимый setup и решает, что всё зависло");
    }
}
