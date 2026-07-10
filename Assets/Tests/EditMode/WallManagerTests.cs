using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class WallManagerTests
{
    private GameObject _cameraGo;
    private WallManager _wallManager;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _originalLowerNearWalls;

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.BoardName = name;
        e.DimensionsMM = dims;
        go.AddComponent<Wall>();
        BoardRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [SetUp]
    public void SetUp()
    {
        _cameraGo = new GameObject("TestCamera");
        _cameraGo.tag = "MainCamera";
        _cameraGo.AddComponent<Camera>();

        var go = new GameObject("WallManager");
        _wallManager = go.AddComponent<WallManager>();
        _spawned.Add(go);

        if (KitchenSettings.Instance != null)
            _originalLowerNearWalls = KitchenSettings.Instance.LowerNearWalls;
    }

    [TearDown]
    public void TearDown()
    {
        BoardRegistry.Clear();

        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);

        if (KitchenSettings.Instance != null)
            KitchenSettings.Instance.LowerNearWalls = _originalLowerNearWalls;
    }

    [Test]
    public void LateUpdate_DoesNotThrow_WithValidCameraAndWalls()
    {
        Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));

        _wallManager.LateUpdate();
    }

    [Test]
    public void LateUpdate_ShowsWalls_WhenWallsEnabled()
    {
        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        var renderer = e.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer);

        _wallManager.LateUpdate();

        Assert.IsTrue(renderer.enabled, "wall should be visible when WallsEnabled is true");
    }

    [Test]
    public void LateUpdate_LowersWallFacingCamera_WhenLowerNearWallsEnabled()
    {
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings, "KitchenSettings.Instance should be loadable from Resources");
        settings.LowerNearWalls = true;

        _cameraGo.transform.position = new Vector3(0, 1.25f, -5f);
        _cameraGo.transform.LookAt(Vector3.zero);

        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, -2f));
        var wall = e.GetComponent<Wall>();

        _wallManager.LateUpdate();

        Assert.IsTrue(wall.IsLowered, "wall between camera and scene center should be lowered");
    }

    [Test]
    public void LateUpdate_DoesNotLowerWallBehindCamera()
    {
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings);
        settings.LowerNearWalls = true;

        _cameraGo.transform.position = new Vector3(0, 1.25f, -5f);
        _cameraGo.transform.LookAt(Vector3.zero);

        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 3f));
        var wall = e.GetComponent<Wall>();

        _wallManager.LateUpdate();

        Assert.IsFalse(wall.IsLowered, "wall behind scene center should not be lowered");
    }

    [Test]
    public void LateUpdate_DoesNotThrow_WhenCameraDestroyed()
    {
        Object.DestroyImmediate(_cameraGo);
        _cameraGo = null;

        Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));

        _wallManager.LateUpdate();
    }

    [Test]
    public void LateUpdate_RestoresFullHeight_WhenWallsDisabled()
    {
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings);
        bool prev = settings.WallsEnabled;
        settings.WallsEnabled = false;

        var e = Make("Wall", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        var wall = e.GetComponent<Wall>();
        wall.SetLowered(true, 0.1f);

        _wallManager.LateUpdate();

        Assert.IsFalse(wall.IsLowered, "wall should be restored to full height when walls are disabled");
        Assert.AreEqual(2.5f, e.transform.localScale.y, 0.001f, "wall height should be restored");

        settings.WallsEnabled = prev;
    }
}
