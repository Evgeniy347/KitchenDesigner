#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Командная строка, с которой приложение запускает скачанный установщик Inno. Запустить
/// установщик из теста нельзя, поэтому проверяется то, что Inno из этой строки получит:
/// строка режется так же, как её режет Windows (правила CommandLineToArgvW), и каждый
/// ключ сверяется по отдельности. Ошибка здесь видна только на машине пользователя —
/// «откат» без лога, и разбирать нечего.
/// </summary>
public class InstallerCommandLineTests
{
    private const string InstallerInPathWithSpaces =
        @"C:\Users\Иван Петров\AppData\Local\Temp\DefaultCompany\KitchenDesigner2\KitchenDesigner-Setup-0.2100-x64.exe";

    private static List<string> SplitLikeWindows(string commandLine)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        bool quoted = false, any = false;
        foreach (var ch in commandLine)
        {
            if (ch == '"') { quoted = !quoted; any = true; continue; }
            if (ch == ' ' && !quoted)
            {
                if (any) args.Add(current.ToString());
                current.Clear();
                any = false;
                continue;
            }
            current.Append(ch);
            any = true;
        }
        Assert.IsFalse(quoted, "незакрытая кавычка: Windows склеит хвост строки в один аргумент");
        if (any) args.Add(current.ToString());
        return args;
    }

    private static List<string> Args() =>
        SplitLikeWindows(InstallerCommandLine.ForSilentRelaunch(InstallerInPathWithSpaces));

    [Test]
    public void Install_IsSilent_AndAnswersEveryDialogWithoutTheUser()
    {
        var args = Args();
        CollectionAssert.Contains(args, "/SILENT",
            "без /SILENT пользователь видит мастер установки посреди «автоматического» обновления");
        CollectionAssert.Contains(args, "/SUPPRESSMSGBOXES",
            "без него тихий setup повиснет на невидимом вопросе, а приложение уже закрылось");
        CollectionAssert.Contains(args, "/NORESTART",
            "обновление файлов пользователя никогда не повод перезагружать Windows");
    }

    [Test]
    public void Install_AsksTheScriptToRelaunchTheApp_AndWaitForTheOldCopy()
    {
        CollectionAssert.Contains(Args(), "/RELAUNCH",
            "по /RELAUNCH KitchenDesigner.iss ждёт выхода старой копии и поднимает новую. "
            + "Без ключа setup упрётся в ещё живой плеер, Restart Manager его не закроет, "
            + "и под /SUPPRESSMSGBOXES установка откатится");
    }

    [Test]
    public void Install_NeverDisablesTheRestartManagerSafetyNet()
    {
        var args = Args();
        CollectionAssert.DoesNotContain(args, "/NOCLOSEAPPLICATIONS",
            "без Restart Manager занятый файл сразу даёт Abort → откат");
        CollectionAssert.DoesNotContain(args, "/VERYSILENT",
            "/VERYSILENT прячет и окно прогресса: пользователь, у которого закрылось приложение, "
            + "не видит, что идёт установка, и запускает старую копию поверх неё");
    }

    [Test]
    public void Install_WritesALog_EvenWhenTheUsersPathHasSpaces()
    {
        var logArgs = Args().Where(a => a.StartsWith("/LOG=", StringComparison.Ordinal)).ToList();
        Assert.AreEqual(1, logArgs.Count,
            "тихий setup без /LOG откатывается молча: на машине пользователя «откат» остался "
            + "без единой строки причины, и её пришлось воспроизводить заново");
        Assert.AreEqual(InstallerCommandLine.LogPathFor(InstallerInPathWithSpaces),
            logArgs[0].Substring("/LOG=".Length),
            "путь лога в кавычках: профиль «Иван Петров» без них разрезается пробелом "
            + "на два аргумента, и Inno пишет лог в «C:\\Users\\Иван»");
    }

    [Test]
    public void Log_LiesNextToTheInstaller_AndNeverOverwritesIt()
    {
        var log = InstallerCommandLine.LogPathFor(InstallerInPathWithSpaces);
        Assert.AreEqual(Path.GetDirectoryName(InstallerInPathWithSpaces), Path.GetDirectoryName(log),
            "лог рядом с установщиком: оба в temporaryCachePath приложения, куда пользователь "
            + "может дойти и откуда его можно прислать");
        Assert.AreNotEqual(InstallerInPathWithSpaces, log,
            "Inno, пишущий лог поверх собственного exe, сломает себе же следующий запуск");
        StringAssert.EndsWith(".log", log);
        StringAssert.Contains("0.2100", log,
            "версия в имени лога: попытки поставить разные версии не затирают друг друга");
    }
}
