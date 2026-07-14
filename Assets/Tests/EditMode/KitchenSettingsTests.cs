using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class KitchenSettingsTests
{
    [Test]
    public void Instance_IsNotNull()
    {
        var instance = KitchenSettings.Instance;
        Assert.NotNull(instance);
    }

    [Test]
    public void GridStep_DefaultsTo16()
    {
        Assert.AreEqual(16, KitchenSettings.Instance.GridStep);
    }

    [Test]
    public void GridStep_ClampsToMinimum1()
    {
        KitchenSettings.Instance.GridStep = -5;
        Assert.AreEqual(1, KitchenSettings.Instance.GridStep);
        KitchenSettings.Instance.GridStep = 0;
        Assert.AreEqual(1, KitchenSettings.Instance.GridStep);
        KitchenSettings.Instance.GridStep = 16;
    }

    [Test]
    public void SnapThreshold_ClampsToMinimum1()
    {
        KitchenSettings.Instance.SnapThreshold = -10f;
        Assert.AreEqual(1f, KitchenSettings.Instance.SnapThreshold);
        KitchenSettings.Instance.SnapThreshold = 50f;
    }

    [Test]
    public void SaveAndLoad_RestoresAllFields()
    {
        var gs = KitchenSettings.Instance;
        gs.GridStep = 32;
        gs.GridEnabled = false;
        gs.SnapEnabled = false;
        gs.SnapThreshold = 100f;
        gs.BlockOnViolation = true;
        gs.AutoSave = true;
        gs.AutoSaveInterval = 120;

        gs.Save();
        gs.Load();

        Assert.AreEqual(32, gs.GridStep);
        Assert.AreEqual(false, gs.GridEnabled);
        Assert.AreEqual(false, gs.SnapEnabled);
        Assert.AreEqual(100f, gs.SnapThreshold);
        Assert.AreEqual(true, gs.BlockOnViolation);
        Assert.AreEqual(true, gs.AutoSave);
        Assert.AreEqual(120, gs.AutoSaveInterval);

        gs.GridStep = 16;
        gs.GridEnabled = true;
        gs.SnapEnabled = true;
        gs.SnapThreshold = 50f;
        gs.BlockOnViolation = false;
        gs.AutoSave = false;
        gs.AutoSaveInterval = 60;
        gs.Save();
    }

    [Test]
    public void EdgeOutline_SavesAndLoads()
    {
        var gs = KitchenSettings.Instance;
        bool prev = gs.EdgeOutline;

        gs.EdgeOutline = true;
        gs.Save();
        gs.EdgeOutline = false;
        gs.Load();
        Assert.IsTrue(gs.EdgeOutline);

        gs.EdgeOutline = prev;
        gs.Save();
    }

    [Test]
    public void WallSettings_SaveAndLoad()
    {
        var gs = KitchenSettings.Instance;
        bool prevWalls = gs.WallsEnabled;
        bool prevLower = gs.LowerNearWalls;

        gs.WallsEnabled = false;
        gs.LowerNearWalls = true;
        gs.Save();
        gs.WallsEnabled = true;
        gs.LowerNearWalls = false;
        gs.Load();
        Assert.IsFalse(gs.WallsEnabled);
        Assert.IsTrue(gs.LowerNearWalls);

        gs.WallsEnabled = prevWalls;
        gs.LowerNearWalls = prevLower;
        gs.Save();
    }

    [Test]
    public void SpatialGridAndWindowedMode_SaveAndLoad()
    {
        var gs = KitchenSettings.Instance;
        bool prevGrid = gs.SpatialGrid;
        bool prevWindow = gs.WindowedMode;

        gs.SpatialGrid = true;
        gs.WindowedMode = false;
        gs.Save();
        gs.SpatialGrid = false;
        gs.WindowedMode = true;
        gs.Load();
        Assert.IsTrue(gs.SpatialGrid);
        Assert.IsFalse(gs.WindowedMode);

        gs.SpatialGrid = prevGrid;
        gs.WindowedMode = prevWindow;
        gs.Save();
    }

    [Test]
    public void AutoSaveInterval_ClampsToMinimum10()
    {
        var gs = KitchenSettings.Instance;
        int prev = gs.AutoSaveInterval;
        gs.AutoSaveInterval = 3;
        Assert.AreEqual(10, gs.AutoSaveInterval);
        gs.AutoSaveInterval = prev;
    }

    [Test]
    public void Load_WithoutSavedKey_DoesNotThrow_KeepsValues()
    {
        var gs = KitchenSettings.Instance;
        int prevStep = gs.GridStep;
        bool hadKey = PlayerPrefs.HasKey("KitchenSettings");
        string? saved = hadKey ? PlayerPrefs.GetString("KitchenSettings") : null;

        PlayerPrefs.DeleteKey("KitchenSettings");
        gs.GridStep = 24;
        Assert.DoesNotThrow(() => gs.Load());
        Assert.AreEqual(24, gs.GridStep, "без ключа Load не меняет значения");

        gs.GridStep = prevStep;
        if (hadKey) { PlayerPrefs.SetString("KitchenSettings", saved); PlayerPrefs.Save(); }
    }

    [Test]
    public void BasePlate_CreatesWithCorrectSize()
    {
        var plate = BasePlate.Create();
        Assert.NotNull(plate);
        Assert.NotNull(plate.Element);

        var element = plate.Element;
        Assert.AreEqual(new Vector3Int(3000, 18, 3000), element.DimensionsMM);

        var scale = plate.transform.localScale;
        Assert.AreEqual(3f, scale.x, 0.001f);
        Assert.AreEqual(0.018f, scale.y, 0.001f);
        Assert.AreEqual(3f, scale.z, 0.001f);

        Object.DestroyImmediate(plate.gameObject);
    }
}
