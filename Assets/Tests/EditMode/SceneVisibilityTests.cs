using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Настройки видимости сцены: «Объекты», «Скрыть источники света»,
/// «Скрывать окна и двери» у опущенных стен и запрет опускания в режиме
/// «помещение».</summary>
public class SceneVisibilityTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private GameObject? _cameraGo;
    private WallManager? _wallManager;
    private KitchenSettingsData? _backup;

    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s, "Resources/KitchenSettings.asset не найден");
        _backup = s.ToData();
        s.ResetToDefaults();

        _cameraGo = new GameObject("TestCamera");
        _cameraGo!.tag = "MainCamera";
        _cameraGo!.AddComponent<Camera>();

        var go = new GameObject("WallManager");
        _wallManager = go.AddComponent<WallManager>();
        _spawned.Add(go);
    }

    [TearDown]
    public void TearDown()
    {
        EditModeManager.Reset();
        PartRegistry.Clear();

        foreach (var g in _spawned)
            if (g != null) Object.DestroyImmediate(g);
        _spawned.Clear();

        if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
        _cameraGo = null;

        var s = KitchenSettings.Instance;
        if (s != null && _backup != null) s.ApplyFrom(_backup);
        _backup = null;
    }

    private GameObject Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go;
    }

    // ── «Объекты» ───────────────────────────────────────────

    [Test]
    public void ObjectsVisible_False_HidesPart()
    {
        var part = Spawn(ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Board", Vector3.zero));

        KitchenSettings.Instance.ObjectsVisible = false;
        SceneVisibilityManager.Apply();
        Assert.IsFalse(part.GetComponent<MeshRenderer>()!.enabled, "деталь должна скрыться");

        KitchenSettings.Instance.ObjectsVisible = true;
        SceneVisibilityManager.Apply();
        Assert.IsTrue(part.GetComponent<MeshRenderer>()!.enabled, "деталь должна вернуться");
    }

    [Test]
    public void ObjectsVisible_False_KeepsWallsFloorsWindowsAndDoors()
    {
        var wall = Spawn(ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), "W", new Vector3(0f, 1.25f, -1.5f)));
        var floor = Spawn(ElementFactory.CreateFloor(new Vector3Int(3000, 20, 3000), "F", Vector3.zero));
        var win = Spawn(ElementFactory.CreateWindow(new Vector3Int(900, 1200, 100), "Win", new Vector3(0.6f, 1.2f, -1.5f)));
        win.GetComponent<WindowElement>()!.SnapToWall();

        KitchenSettings.Instance.ObjectsVisible = false;
        SceneVisibilityManager.Apply();

        Assert.IsTrue(wall.GetComponent<MeshRenderer>()!.enabled, "стена — не «объект»");
        Assert.IsTrue(floor.GetComponent<MeshRenderer>()!.enabled, "пол — не «объект»");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(win.GetComponent<KitchenElement>()!),
            "окно — не «объект»");
    }

    [Test]
    public void HideLightSources_HidesOnlyLamps()
    {
        var lamp = Spawn(ElementFactory.CreateLightSource("Lamp", new Vector3(0f, 2f, 0f)));
        var part = Spawn(ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Board", Vector3.zero));

        KitchenSettings.Instance.HideLightSources = true;
        SceneVisibilityManager.Apply();

        Assert.IsFalse(lamp.GetComponent<MeshRenderer>()!.enabled, "плафон должен скрыться");
        Assert.IsTrue(part.GetComponent<MeshRenderer>()!.enabled, "деталь не трогаем");
    }

    /// <summary>«Скрыть источники света» гасит только плафон — сам свет
    /// продолжает гореть, за него отвечает кнопка «Свет» в тулбаре.</summary>
    [Test]
    public void HideLightSources_KeepsLightItselfOn()
    {
        var lamp = Spawn(ElementFactory.CreateLightSource("Lamp", new Vector3(0f, 2f, 0f)));
        var ls = lamp.GetComponent<LightSourceElement>()!;
        ls.EnsureLight();
        ls.SyncLightState();

        KitchenSettings.Instance.HideLightSources = true;
        SceneVisibilityManager.Apply();

        Assert.IsTrue(ls.PointLight!.enabled, "свет должен остаться включённым");
    }

    // ── Контур ──────────────────────────────────────────────

    [Test]
    public void Outline_WallAndObject_UseSeparateSettings()
    {
        var wall = Spawn(ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), "W", new Vector3(0f, 1.25f, -1.5f)));
        var part = Spawn(ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Board", Vector3.zero));
        var s = KitchenSettings.Instance;

        s.WallOutline = true; s.EdgeOutline = false;
        Assert.IsTrue(EdgeOutlineRenderer.ShouldOutline(wall.GetComponent<KitchenElement>()!, s));
        Assert.IsFalse(EdgeOutlineRenderer.ShouldOutline(part.GetComponent<KitchenElement>()!, s));

        s.WallOutline = false; s.EdgeOutline = true;
        Assert.IsFalse(EdgeOutlineRenderer.ShouldOutline(wall.GetComponent<KitchenElement>()!, s));
        Assert.IsTrue(EdgeOutlineRenderer.ShouldOutline(part.GetComponent<KitchenElement>()!, s));
    }

    [Test]
    public void Outline_NotDrawn_ForHiddenObject()
    {
        var part = Spawn(ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Board", Vector3.zero));
        var s = KitchenSettings.Instance;
        s.EdgeOutline = true;
        s.ObjectsVisible = false;
        SceneVisibilityManager.Apply();

        Assert.IsFalse(EdgeOutlineRenderer.ShouldOutline(part.GetComponent<KitchenElement>()!, s),
            "у скрытой детали контур рисоваться не должен");
    }

    // ── Окна и двери у опущенных стен ───────────────────────

    private (GameObject wall, GameObject win) MakeLoweredWallWithWindow()
    {
        _cameraGo!.transform.position = new Vector3(0f, 1.25f, -5f);
        _cameraGo!.transform.LookAt(Vector3.zero);

        var wall = Spawn(ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), "W", new Vector3(0f, 1.25f, -1.5f)));
        var win = Spawn(ElementFactory.CreateWindow(new Vector3Int(900, 1200, 100), "Win", new Vector3(0.6f, 1.2f, -1.5f)));
        win.GetComponent<WindowElement>()!.SnapToWall();
        return (wall, win);
    }

    [Test]
    public void HideOpenings_HidesWindowOfLoweredWall_ButKeepsCutout()
    {
        var (wall, win) = MakeLoweredWallWithWindow();
        var s = KitchenSettings.Instance;
        s.LowerNearWalls = true;
        s.HideOpeningsOnLoweredWalls = true;

        _wallManager!.LateUpdate();

        Assert.IsTrue(wall.GetComponent<Wall>()!.IsLowered, "стена перед камерой должна опуститься");
        Assert.IsFalse(SceneVisibility.AnyRendererEnabled(win.GetComponent<KitchenElement>()!),
            "окно опущенной стены должно скрыться");

        // Вырез — часть меша стены, он не зависит от видимости окна.
        Assert.AreEqual(1, wall.GetComponent<Wall>()!.AttachedWindows.Count,
            "окно остаётся привязанным — проём сохраняется");
    }

    [Test]
    public void HideOpenings_Off_KeepsWindowVisible()
    {
        var (wall, win) = MakeLoweredWallWithWindow();
        var s = KitchenSettings.Instance;
        s.LowerNearWalls = true;
        s.HideOpeningsOnLoweredWalls = false;

        _wallManager!.LateUpdate();

        Assert.IsTrue(wall.GetComponent<Wall>()!.IsLowered);
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(win.GetComponent<KitchenElement>()!),
            "без настройки окно остаётся видимым");
    }

    // ── Режим «помещение» ───────────────────────────────────

    [Test]
    public void RoomMode_KeepsWallsFullHeight_EvenWithLowerNearWallsOn()
    {
        var (wall, _) = MakeLoweredWallWithWindow();
        var s = KitchenSettings.Instance;
        s.LowerNearWalls = true;

        EditModeManager.SetMode(EditMode.Room);
        _wallManager!.LateUpdate();

        Assert.IsFalse(wall.GetComponent<Wall>()!.IsLowered,
            "в режиме «помещение» стены всегда в полный рост");
        Assert.AreEqual(2.5f, wall.transform.localScale.y, 0.001f);
    }

    [Test]
    public void RoomMode_ShowsOpenings_EvenWithHideOpeningsOn()
    {
        var (_, win) = MakeLoweredWallWithWindow();
        var s = KitchenSettings.Instance;
        s.LowerNearWalls = true;
        s.HideOpeningsOnLoweredWalls = true;

        EditModeManager.SetMode(EditMode.Room);
        _wallManager!.LateUpdate();

        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(win.GetComponent<KitchenElement>()!),
            "нечего прятать — стена не опущена");
    }
}
