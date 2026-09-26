using System.IO;
using NUnit.Framework;
using KitchenDesigner.Core;
using UnityEngine;

/// <summary>Сенсор задачи «PlayMode-фикстуры удаляют настоящий
/// persistentDataPath/saves/autosave.json пользователя» (review-perf-tests-tooling.md #1):
/// прогон тестов обязан подменять SavesDirectory на временный каталог, никогда не
/// разрешая его в реальную папку persistentDataPath пользователя.</summary>
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
