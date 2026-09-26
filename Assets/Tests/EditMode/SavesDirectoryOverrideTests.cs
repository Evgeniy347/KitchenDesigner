using System.IO;
using NUnit.Framework;
using KitchenDesigner.Core;
using UnityEngine;

/// <summary>Тот же сторож, что и Assets/Tests/PlayMode/SavesDirectoryOverrideTests.cs,
/// но для EditMode — набор из review-perf-tests-tooling.md #1 полагается на подмену
/// SavesDirectory во ВСЕХ тестах, не только в PlayMode-фикстурах, которые удаляют
/// autosave.json без бэкапа: EditMode-тесты (SaveLoadManagerFileTests,
/// SaveRestoreContractTests) пишут пробные проекты и zip-бэкапы в ту же папку.</summary>
public class SavesDirectoryOverrideTests
{
    [Test]
    public void SavesDirectory_DuringATestRun_IsNeverTheRealUserFolder()
    {
        var realUserSaves = Path.Combine(Application.persistentDataPath, "saves").Replace('\\', '/');
        var actual = SaveLoadManager.SavesDirectory.Replace('\\', '/');
        Assert.AreNotEqual(realUserSaves, actual,
            "прогон тестов не должен резолвить saves/autosave.json в настоящий persistentDataPath пользователя");
    }
}
