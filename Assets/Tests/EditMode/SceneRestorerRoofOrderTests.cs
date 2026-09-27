using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>review-construction.md #2: SceneRestorer.Restore built every element - including
/// RoofElement, whose ApplyDimensions reads LevelRegistry.Snapshot() for the top level's
/// elevation - BEFORE RestoreProjectState ran LevelRegistry.Set(data.levels). A roof loaded
/// right after browsing a different (or no) project computed its eave against the STALE
/// registry left over from before, and nothing rebuilt it once the real levels landed. Fixed
/// by rebuilding every RoofElement right after RestoreProjectState (SceneRestorer.
/// RebuildRoofsAfterLevelsAndSettingsAreRestored), the same ordering RoofElementTests already
/// proves for the wiring to PartRegistry/LevelRegistry within a single ApplyDimensions call.</summary>
public class SceneRestorerRoofOrderTests
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

    /// <summary>Сравнение с КОНТРОЛЬНЫМ прогоном, а не жёстким числом: высота карниза
    /// зависит от геометрии крыши (сюда же садится и известный долг #13 — крыша без стен
    /// строится по фиктивному следу 1×1 м), и дублировать эту арифметику в тесте значило бы
    /// проверить, что тест согласен сам с собой, а не что порядок восстановления не важен.
    /// Вместо этого одна и та же сцена грузится дважды — при УЖЕ верном реестре (контроль) и
    /// при заведомо устаревшем однуровневом реестре (проверяемый случай) — и сверяется, что
    /// стартовое состояние LevelRegistry не меняет результат.</summary>
    [Test]
    public void LoadingATwoStoreyProject_AfterAStaleSingleLevelRegistry_MatchesLoadingWithTheRegistryAlreadyCorrect()
    {
        var roofData = new ElementData { isRoof = true, name = "Roof" };
        var project = new ProjectData(new[] { roofData })
        {
            levels = new[]
            {
                new Level("1", "1 этаж", 0, 3000),
                new Level("2", "2 этаж", 3000, 2700),
            },
        };

        LevelRegistry.Set(project.levels);
        var control = SaveLoadManager.RestoreScene(project);
        float expectedY = control[0].GetComponent<RoofElement>().transform.position.y;
        foreach (var go in control)
            if (go != null) Object.DestroyImmediate(go);
        PartRegistry.Clear();
        CommandStack.Clear();
        LevelRegistry.Reset();

        LevelRegistry.Set(new[] { new Level("1", "1 этаж", 0, 3000) });
        var created = SaveLoadManager.RestoreScene(project);
        var roof = created[0].GetComponent<RoofElement>();

        Assert.AreEqual(expectedY, roof.transform.position.y, 0.01f,
            "проект несёт этаж «2» (пол 3000, высота 2700) - карниз обязан сесть на ту же "
            + "высоту, что и при контрольной загрузке с изначально верным реестром, а не "
            + "остаться там, где его посадил бы прежний однуровневый реестр, оставшийся от "
            + "прошлого сеанса (review-construction.md #2)");
    }
}
