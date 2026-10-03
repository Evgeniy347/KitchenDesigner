using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using KitchenDesigner.Core;
using KitchenDesigner.Tests.Geometry;
using NUnit.Framework;

// Перенос DefaultCompany → Evgeniy347 — одноразовый код с датой годности: вышел вместе со сменой
// companyName (2026-10-03), а через сто дней обновились все, кто вообще обновляется. Дальше он
// только читает реестр на каждом запуске. Дата — константа, а не «сборка + 100 дней»: такая
// арифметика сдвигалась бы с каждой сборкой и не сработала бы никогда.
public class PublisherMoveLifetimeTests
{
    private static readonly DateTime RemoveFrom = new DateTime(2027, 1, 11);

    private static readonly string[] MigrationFiles =
    {
        "Assets/Scripts/Core/Pure/Persistence/PublisherMove",
        "Assets/Scripts/Core/Persistence/PublisherMigrationStartup.cs",
        "Assets/Tests/EditMode/Pure/PublisherMoveDecisionTests.cs",
        "Assets/Tests/EditMode/Pure/RegistryPublisherStoreTests.cs",
        "Assets/Tests/EditMode/Pure/FolderPublisherStoreTests.cs",
        "Assets/Tests/EditMode/Pure/PublisherMoveLifetimeTests.cs",
        "Assets/Tests/EditMode/PlayerPrefsLiveRegistryTests.cs",
    };

    private static string Repo(string relative) =>
        Path.Combine(RepoPaths.Subdir("Assets"), "..", relative.Replace('/', Path.DirectorySeparatorChar));

    [Test]
    public void Migration_IsDeletedHundredDaysAfterRelease()
    {
        Assert.Less(DateTime.Today, RemoveFrom,
            "Удалите перенос DefaultCompany → Evgeniy347 (" + string.Join(", ", MigrationFiles)
            + "): прошло 100 дней с выпуска, все пользователи уже обновились.");
    }

    [Test]
    public void TheListInTheDeletionMessage_NamesFilesThatExist()
    {
        var missing = MigrationFiles.Where(f => !File.Exists(Repo(f)) && !Directory.Exists(Repo(f))).ToList();
        Assert.IsEmpty(missing, "сообщение об удалении перечисляло бы несуществующие файлы: " + string.Join(", ", missing));
    }

    [Test]
    public void ProjectSettingsCompany_IsWhereTheMigrationMovesTo()
    {
        var settings = File.ReadAllText(Repo("ProjectSettings/ProjectSettings.asset"));
        Assert.AreEqual(PublisherRename.NewCompany, Regex.Match(settings, @"^\s*companyName:\s*(.+?)\s*$", RegexOptions.Multiline).Groups[1].Value,
            "PlayerPrefs и persistentDataPath живут под companyName — перенос должен вести туда же");
        Assert.AreEqual("KitchenDesigner2", Regex.Match(settings, @"^\s*productName:\s*(.+?)\s*$", RegexOptions.Multiline).Groups[1].Value,
            "productName — второй сегмент обоих путей; его смена потеряла бы данные так же, как смена компании");
    }

    [Test]
    public void StartupHook_RunsBeforeAnythingReadsTheStores()
    {
        var hook = File.ReadAllText(Repo("Assets/Scripts/Core/Persistence/PublisherMigrationStartup.cs"));
        StringAssert.Contains("RuntimeInitializeLoadType.SubsystemRegistration", hook,
            "раньше SubsystemRegistration управляемый код не запускается; позже — Bootstrap уже прочёл настройки и открыл автосейв");
        StringAssert.Contains("#if UNITY_STANDALONE_WIN && !UNITY_EDITOR", hook,
            "в редакторе (и в его PlayMode-тестах) перенос не запускается: настоящие данные пользователя переносит только плеер");
    }
}
