using System.Reflection;
using NUnit.Framework;
using KitchenDesigner.Core;

public class KitchenSettingsTests
{
    [SetUp]
    public void Setup()
    {
        ResetToAssetDefaults();
    }

    [TearDown]
    public void Teardown()
    {
        ResetToAssetDefaults();
    }

    private static void ResetToAssetDefaults()
    {
        var field = typeof(KitchenSettings).GetField("_instance",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (field != null) field.SetValue(null, null);
    }

    [Test]
    public void InputSpeeds_AreClampedToRange()
    {
        var s = KitchenSettings.Instance;
        var backup = s.ToData();

        s.MouseSensitivity = 99f;
        Assert.AreEqual(KitchenSettings.MAX_INPUT_SPEED, s.MouseSensitivity, 0.001f);

        s.WasdSpeed = 0f;
        Assert.AreEqual(KitchenSettings.MIN_INPUT_SPEED, s.WasdSpeed, 0.001f);

        s.ArrowSpeed = -3f;
        Assert.AreEqual(KitchenSettings.MIN_INPUT_SPEED, s.ArrowSpeed, 0.001f);

        s.ApplyFrom(backup);
    }

    [Test]
    public void Instance_IsNotNull()
    {
        var instance = KitchenSettings.Instance;
        Assert.NotNull(instance);
    }

    [Test]
    public void GridStep_LoadedFromAsset()
    {
        int step = KitchenSettings.Instance.GridStep;
        Assert.GreaterOrEqual(step, 1, "GridStep must be at least 1");
        Assert.LessOrEqual(step, 100, "GridStep must be reasonable");
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

        var data = gs.ToData();
        Assert.IsNotNull(data);

        gs.GridStep = 16;
        gs.GridEnabled = true;
        gs.SnapEnabled = true;
        gs.SnapThreshold = 50f;
        gs.BlockOnViolation = false;
        gs.AutoSave = false;
        gs.AutoSaveInterval = 60;

        gs.ApplyFrom(data);

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
    }

    [Test]
    public void EdgeOutline_SavesAndLoads()
    {
        var gs = KitchenSettings.Instance;
        bool prev = gs.NormalView.edgeOutline;

        gs.NormalView.edgeOutline = true;
        var data = gs.ToData();
        gs.NormalView.edgeOutline = false;
        gs.ApplyFrom(data);
        Assert.IsTrue(gs.NormalView.edgeOutline);

        gs.NormalView.edgeOutline = prev;
    }

    [Test]
    public void WallSettings_SaveAndLoad()
    {
        var gs = KitchenSettings.Instance;
        bool prevWalls = gs.NormalView.wallsEnabled;
        bool prevLower = gs.NormalView.lowerNearWalls;

        gs.NormalView.wallsEnabled = false;
        gs.NormalView.lowerNearWalls = true;
        var data = gs.ToData();
        gs.NormalView.wallsEnabled = true;
        gs.NormalView.lowerNearWalls = false;
        gs.ApplyFrom(data);
        Assert.IsFalse(gs.NormalView.wallsEnabled);
        Assert.IsTrue(gs.NormalView.lowerNearWalls);

        gs.NormalView.wallsEnabled = prevWalls;
        gs.NormalView.lowerNearWalls = prevLower;
    }

    [Test]
    public void CameraPanFree_SavesAndLoads()
    {
        var gs = KitchenSettings.Instance;
        bool prev = gs.CameraPanFree;

        gs.CameraPanFree = true;
        var data = gs.ToData();
        gs.CameraPanFree = false;
        gs.ApplyFrom(data);
        Assert.IsTrue(gs.CameraPanFree);

        gs.CameraPanFree = prev;
    }

    [Test]
    public void SpatialGridAndWindowedMode_SaveAndLoad()
    {
        var gs = KitchenSettings.Instance;
        bool prevGrid = gs.SpatialGrid;
        bool prevWindow = gs.WindowedMode;

        gs.SpatialGrid = true;
        gs.WindowedMode = false;
        var data = gs.ToData();
        gs.SpatialGrid = false;
        gs.WindowedMode = true;
        gs.ApplyFrom(data);
        Assert.IsTrue(gs.SpatialGrid);
        Assert.IsFalse(gs.WindowedMode);

        gs.SpatialGrid = prevGrid;
        gs.WindowedMode = prevWindow;
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
    public void ApplyFrom_Null_DoesNotThrow_KeepsValues()
    {
        var gs = KitchenSettings.Instance;
        int prevStep = gs.GridStep;

        gs.GridStep = 24;
        Assert.DoesNotThrow(() => gs.ApplyFrom(null));
        Assert.AreEqual(24, gs.GridStep, "ApplyFrom(null) не меняет значения");

        gs.GridStep = prevStep;
    }

    [Test]
    public void Instance_ExistsWithoutAnAsset_AndInputSpeedsStartAtOne()
    {
        var s = KitchenSettings.Instance;

        Assert.NotNull(s, "настройки больше не ассет из Resources: Instance строит объект сам, "
            + "поэтому вызывающему некуда падать и запасная ветка «нет ассета» не нужна");
        Assert.AreEqual(1f, s.MouseSensitivity, 0.001f,
            "CameraController умножал ввод на 1, когда ассета настроек не было; "
            + "то же значение теперь обязан давать сам инициализатор поля");
        Assert.AreEqual(1f, s.WasdSpeed, 0.001f);
        Assert.AreEqual(1f, s.ArrowSpeed, 0.001f);
    }
}
