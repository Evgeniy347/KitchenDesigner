using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

// Дефект пользователя: после прогона дымовой проверки обычный запуск приложения открывал
// C:\Users\...\Temp\kd-smoke-<хэш>\smoke-roundtrip.save.json вместо его проекта. Чинится
// ЗАПИСЬ, а не подчистка после прогона: проверка может упасть посередине, и тогда подчистка
// не выполнится, а ключ уже подменён — поэтому здесь нет ни одного «вернём как было».
public class SmokeRunKeepsUserPrefsReproTests
{
    private static readonly string[] SmokeRunArgs =
    {
        "KitchenDesigner.exe", "-mcpPort", "19881",
        "-mcpSaveDir", @"C:\Users\gtx54\AppData\Local\Temp\kd-smoke-dfa7ed04",
        MuteAudioArgument.Name, HideWindowArgument.Name, EphemeralSessionArgument.Name
    };

    private static readonly string[] OrdinaryRunArgs = { "KitchenDesigner.exe" };

    private const string UserProject = @"C:\Users\gtx54\Documents\kitchen\my-kitchen.save.json";

    private const string SmokeProject =
        @"C:\Users\gtx54\AppData\Local\Temp\kd-smoke-dfa7ed04\smoke-roundtrip.save.json";

    private string _prevLastPath = "";
    private int _prevFirstRun;

    [SetUp]
    public void SaveUserPrefs()
    {
        _prevLastPath = PlayerPrefs.GetString(StoredLastProject.Key, "");
        _prevFirstRun = PlayerPrefs.GetInt(FirstRunMarker.Key, 0);
    }

    [TearDown]
    public void RestoreUserPrefs()
    {
        PlayerPrefs.SetString(StoredLastProject.Key, _prevLastPath);
        PlayerPrefs.SetInt(FirstRunMarker.Key, _prevFirstRun);
        PlayerPrefs.Save();
    }

    [Test]
    public void ProjectFileStore_SmokeRunSavesItsOwnFile_UsersLastProjectStaysInPlayerPrefs()
    {
        PlayerPrefs.SetString(StoredLastProject.Key, UserProject);
        PlayerPrefs.Save();

        var smokeRun = new ProjectFileStore(SmokeRunArgs);
        smokeRun.LastPath = SmokeProject;

        Assert.AreEqual(SmokeProject, smokeRun.LastPath,
            "внутри самого прогона файл всё-таки считается открытым — иначе save/load круг теряет путь");
        Assert.AreEqual(UserProject, PlayerPrefs.GetString(StoredLastProject.Key, ""),
            "дымовой прогон не пишет «последний проект» в PlayerPrefs (на Windows это реестр)");
        Assert.AreEqual(UserProject, new ProjectFileStore(OrdinaryRunArgs).LastPath,
            "следующий ОБЫЧНЫЙ запуск пользователя открывает его проект, а не файл дымовой проверки");
    }

    [Test]
    public void ProjectFileStore_OrdinaryRunOpensAFile_LastProjectIsRemembered()
    {
        PlayerPrefs.SetString(StoredLastProject.Key, "");
        PlayerPrefs.Save();

        var ordinaryRun = new ProjectFileStore(OrdinaryRunArgs);
        ordinaryRun.LastPath = UserProject;

        Assert.AreEqual(UserProject, PlayerPrefs.GetString(StoredLastProject.Key, ""),
            "противоположный вход: без аргумента прогона «последний проект» обязан по-прежнему писаться");
        Assert.AreEqual(UserProject, new ProjectFileStore(OrdinaryRunArgs).LastPath,
            "и переживать перезапуск приложения — это и есть поведение, ради которого ключ существует");
    }

    [Test]
    public void FirstRunMarker_SmokeRun_DoesNotRecordThatTheUserHasRunBefore()
    {
        PlayerPrefs.DeleteKey(FirstRunMarker.Key);
        PlayerPrefs.Save();

        FirstRunMarker.Record(SmokeRunArgs);

        Assert.IsFalse(FirstRunMarker.Recorded,
            "иначе дымовой прогон съедает первый запуск пользователя, и демо-проект ему уже не покажут");
    }

    [Test]
    public void FirstRunMarker_OrdinaryRun_RecordsThatTheUserHasRunBefore()
    {
        PlayerPrefs.DeleteKey(FirstRunMarker.Key);
        PlayerPrefs.Save();

        FirstRunMarker.Record(OrdinaryRunArgs);

        Assert.IsTrue(FirstRunMarker.Recorded,
            "противоположный вход: обычный запуск обязан помечать, что демо уже показывали");
    }
}
