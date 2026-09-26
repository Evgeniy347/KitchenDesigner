using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// L2 (план LEVELS): <see cref="ElementCapture"/>/<see cref="ElementRestorers"/> обязаны
/// пронести <see cref="KitchenElement.LevelId"/> через файл без потерь, а
/// <see cref="SceneRestorer"/> — собрать <see cref="LevelRegistry"/> из
/// <see cref="ProjectData.levels"/> при восстановлении и из <see cref="LevelRegistry"/>
/// обратно в <see cref="ProjectData.levels"/> при сохранении.
/// </summary>
public class LevelCaptureRestoreTests
{
    [TearDown]
    public void TearDown()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
        LevelRegistry.Reset();
    }

    [Test]
    public void ElementOnLevelTwo_RoundTripsThroughSaveAndLoad_KeepingItsLevelId()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Board_L2", new Vector3(1f, 0.5f, 0f));
        var element = go.GetComponent<KitchenElement>();
        element.LevelId = "2";
        PartRegistry.Register(element);

        var data = ElementCapture.FromElement(element);
        Assert.AreEqual("2", data.levelId, "levelId обязан попасть в ElementData при захвате");

        var project = new ProjectData(new[] { data });
        var created = SaveLoadManager.RestoreScene(project);
        Assert.AreEqual(1, created.Count);
        var restored = created[0].GetComponent<KitchenElement>();

        Assert.AreEqual("2", restored.LevelId,
            "levelId обязан вернуться на элемент при восстановлении, а не осесть только в файле");
    }

    [Test]
    public void ElementWithoutLevelId_RoundTrips_AsEmptyString_NotAsALiteralDefaultLevel()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Board_NoLevel", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();
        PartRegistry.Register(element);

        var data = ElementCapture.FromElement(element);
        Assert.AreEqual("", data.levelId,
            "деталь без явного уровня обязана писаться пустой строкой — так старый файл " +
            "и деталь без уровня неотличимы, а разрешение в первый уровень делает LevelResolution, не запись");
    }

    [Test]
    public void RestoringAProjectWithTwoLevels_BuildsTheLevelRegistry_WithBothLevelsInOrder()
    {
        var project = new ProjectData(System.Array.Empty<ElementData>())
        {
            levels = new[]
            {
                new Level("1", "1 этаж", 0, 3000),
                new Level("2", "2 этаж", 3000, 2800),
            }
        };

        SaveLoadManager.RestoreScene(project);

        Assert.AreEqual(2, LevelRegistry.Items.Count);
        Assert.AreEqual("1", LevelRegistry.Items[0].id);
        Assert.AreEqual("2", LevelRegistry.Items[1].id);
        Assert.AreEqual(3000, LevelRegistry.Items[1].floorElevationMm);
    }

    /// <summary>H6 (review-ui-mcp): LevelRegistry._currentId переживал загрузку — только
    /// Reset() (только в тестах) его трогал. Открыть проект A на «2 этаж», затем открыть
    /// проект B (сохранённый на первом этаже) — B открывался на втором этаже B: новый
    /// каталог спавнится на чужой высоте, а первый этаж B на время недостижим кликом.</summary>
    [Test]
    public void RestoringAProject_ResetsTheCurrentLevel_SoItDoesNotLeakFromThePreviousProject()
    {
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
        });
        LevelRegistry.CurrentId = "2";

        var project = new ProjectData(System.Array.Empty<ElementData>())
        {
            levels = new[] { new Level("1", "1 этаж", 0, 3000) }
        };
        SaveLoadManager.RestoreScene(project);

        Assert.AreEqual("1", LevelRegistry.CurrentId,
            "текущий этаж обязан сброситься на этаж, с которого открывается новый проект, " +
            "а не унаследоваться из предыдущего сеанса");
    }

    [Test]
    public void SavingTheScene_CapturesTheLevelRegistry_BackIntoProjectData()
    {
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 2800),
        });

        var captured = SaveLoadManager.CaptureScene(System.Array.Empty<KitchenElement>());

        Assert.AreEqual(2, captured.levels.Length);
        Assert.AreEqual("2", captured.levels[1].id);
        Assert.AreEqual(3000, captured.levels[1].floorElevationMm);
    }
}
