using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class WallManagerTests
{
    private GameObject? _cameraGo;
    private WallManager? _wallManager;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _originalLowerNearWalls;

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.AddComponent<MeshRenderer>();
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        go.AddComponent<Wall>();
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [SetUp]
    public void SetUp()
    {
        _cameraGo = new GameObject("TestCamera");
        _cameraGo!.tag = "MainCamera";
        _cameraGo!.AddComponent<Camera>();

        var go = new GameObject("WallManager");
        _wallManager = go.AddComponent<WallManager>();
        _spawned.Add(go);

        if (KitchenSettings.Instance != null)
            _originalLowerNearWalls = KitchenSettings.Instance.NormalView.lowerNearWalls;
    }

    [TearDown]
    public void TearDown()
    {
        PartRegistry.Clear();

        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);

        if (KitchenSettings.Instance != null)
            KitchenSettings.Instance.NormalView.lowerNearWalls = _originalLowerNearWalls;
    }

    [Test]
    public void LateUpdate_DoesNotThrow_WithValidCameraAndWalls()
    {
        Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));

        _wallManager!.LateUpdate();
    }

    [Test]
    public void LateUpdate_ShowsWalls_WhenWallsEnabled()
    {
        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        var renderer = e.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer);

        _wallManager!.LateUpdate();

        Assert.IsTrue(renderer.enabled, "wall should be visible when WallsEnabled is true");
    }

    [Test]
    public void LateUpdate_LowersWallFacingCamera_WhenLowerNearWallsEnabled()
    {
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings, "KitchenSettings.Instance should be loadable from Resources");
        settings.NormalView.lowerNearWalls = true;

        _cameraGo!.transform.position = new Vector3(0, 1.25f, -5f);
        _cameraGo!.transform.LookAt(Vector3.zero);

        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, -2f));
        var wall = e.GetComponent<Wall>();

        _wallManager!.LateUpdate();

        Assert.IsTrue(wall.IsLowered, "wall between camera and scene center should be lowered");
    }

    [Test]
    public void LateUpdate_DoesNotLowerWallBehindCamera()
    {
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings);
        settings.NormalView.lowerNearWalls = true;

        _cameraGo!.transform.position = new Vector3(0, 1.25f, -5f);
        _cameraGo!.transform.LookAt(Vector3.zero);

        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 3f));
        var wall = e.GetComponent<Wall>();

        _wallManager!.LateUpdate();

        Assert.IsFalse(wall.IsLowered, "wall behind scene center should not be lowered");
    }

    [Test]
    public void LateUpdate_DoesNotThrow_WhenCameraDestroyed()
    {
        Object.DestroyImmediate(_cameraGo);
        _cameraGo = null;

        Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));

        _wallManager!.LateUpdate();
    }

    [Test]
    public void LateUpdate_RestoresFullHeight_WhenWallsDisabled()
    {
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings);
        bool prev = settings.NormalView.wallsEnabled;
        settings.NormalView.wallsEnabled = false;

        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        var wall = e.GetComponent<Wall>();
        wall.SetLowered(true, 0.1f);

        _wallManager!.LateUpdate();

        Assert.IsFalse(wall.IsLowered, "wall should be restored to full height when walls are disabled");
        Assert.AreEqual(2.5f, e.transform.localScale.y, 0.001f, "wall height should be restored");

        settings.NormalView.wallsEnabled = prev;
    }

    // ── Collider tests ─────────────────────────────────────────────────────

    [Test]
    public void LateUpdate_DisablesColliderWhenWallsDisabled()
    {
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings);
        bool prevWalls = settings.NormalView.wallsEnabled;
        settings.NormalView.wallsEnabled = false;

        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        var collider = e.gameObject.AddComponent<BoxCollider>();
        Assert.IsTrue(collider.enabled, "collider should start enabled");

        _wallManager!.LateUpdate();

        Assert.IsFalse(collider.enabled, "collider should be disabled when walls are hidden");

        settings.NormalView.wallsEnabled = prevWalls;
    }

    [Test]
    public void LateUpdate_EnablesColliderWhenWallsEnabled()
    {
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings);
        bool prevWalls = settings.NormalView.wallsEnabled;
        settings.NormalView.wallsEnabled = true;

        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        var collider = e.gameObject.AddComponent<BoxCollider>();

        _wallManager!.LateUpdate();

        Assert.IsTrue(collider.enabled, "collider should stay enabled when walls are visible");

        settings.NormalView.wallsEnabled = prevWalls;
    }

    [Test]
    public void LateUpdate_KeepsColliderEnabledWhenWallsEnabledAndLowered()
    {
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings);
        bool prevWalls = settings.NormalView.wallsEnabled;
        bool prevLower = settings.NormalView.lowerNearWalls;
        settings.NormalView.wallsEnabled = true;
        settings.NormalView.lowerNearWalls = true;

        _cameraGo!.transform.position = new Vector3(0, 1.25f, -5f);
        _cameraGo!.transform.LookAt(Vector3.zero);

        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, -2f));
        var collider = e.gameObject.AddComponent<BoxCollider>();
        var wall = e.GetComponent<Wall>();

        _wallManager!.LateUpdate();

        Assert.IsTrue(wall.IsLowered, "wall should be lowered");
        Assert.IsTrue(collider.enabled, "lowered wall collider should remain enabled (smaller)");

        settings.NormalView.wallsEnabled = prevWalls;
        settings.NormalView.lowerNearWalls = prevLower;
    }

    [Test]
    public void LateUpdate_RestoresColliderWhenWallsReEnabled()
    {
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings);
        bool prevWalls = settings.NormalView.wallsEnabled;

        // Шаг 1: выключаем стены
        settings.NormalView.wallsEnabled = false;

        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        var collider = e.gameObject.AddComponent<BoxCollider>();

        _wallManager!.LateUpdate();
        Assert.IsFalse(collider.enabled, "collider should be disabled when walls are hidden");

        // Шаг 2: включаем стены обратно
        settings.NormalView.wallsEnabled = true;

        _wallManager!.LateUpdate();
        Assert.IsTrue(collider.enabled, "collider should be re-enabled when walls are visible again");

        settings.NormalView.wallsEnabled = prevWalls;
    }
}
