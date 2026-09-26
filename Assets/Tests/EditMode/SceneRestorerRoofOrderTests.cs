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

    [Test]
    public void LoadingATwoStoreyProject_AfterAStaleSingleLevelRegistry_PutsTheRoofOnTheTopLevel()
    {
        LevelRegistry.Set(new[] { new Level("1", "1 этаж", 0, 3000) });

        var roofData = new ElementData { isRoof = true, name = "Roof" };
        var project = new ProjectData(new[] { roofData })
        {
            levels = new[]
            {
                new Level("1", "1 этаж", 0, 3000),
                new Level("2", "2 этаж", 3000, 2700),
            },
        };

        var created = SaveLoadManager.RestoreScene(project);
        var roof = created[0].GetComponent<RoofElement>();

        float expectedEaveYUnits = (3000 + 2700) * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(expectedEaveYUnits, roof.transform.position.y, 0.01f,
            "проект несёт этаж «2» (пол 3000, высота 2700) - карниз обязан сесть на 5700 мм, "
            + "а не на 3000 мм от прежнего одноэтажного реестра, оставшегося от прошлого "
            + "сеанса (review-construction.md #2)");
    }
}
