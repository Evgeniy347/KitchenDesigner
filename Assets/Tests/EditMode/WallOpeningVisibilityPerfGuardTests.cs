using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож дефекта «WallManager.LateUpdate скрывает/показывает окна и двери стены
/// заново на КАЖДЫЙ кадр, даже когда состояние (скрыт/не скрыт) не поменялось со времени
/// прошлого кадра». `ApplyOpeningVisibility` гоняла `SceneVisibility.SetRenderersEnabled`,
/// которая аллоцирует свежий массив рендереров (`GetComponentsInChildren`) на каждый вызов —
/// то же семейство дефекта, что и у `FacadeElement.StepDoor`: покоящийся объект не имеет
/// права снова и снова платить за пересчёт того же результата. Считаем СОБЫТИЯ
/// (<see cref="WallManager.TakeActiveOpeningVisibilityApplications"/>), не миллисекунды —
/// они плавают от машины к машине.</summary>
public class WallOpeningVisibilityPerfGuardTests
{
    private GameObject? _cameraGo;
    private WallManager? _wallManager;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _prevWallsEnabled;
    private bool _prevLowerNearWalls;

    private KitchenElement MakeWall(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        go.AddComponent<Wall>();
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private WindowElement MakeWindow(Wall wall, Vector3 pos)
    {
        var go = new GameObject("Окно");
        go.transform.position = pos;
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var window = go.AddComponent<WindowElement>();
        window.DimensionsMM = new Vector3Int(900, 1400, 100);
        go.AddComponent<BoxCollider>();
        _spawned.Add(go);
        wall.RegisterWindow(window);
        return window;
    }

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        WallManager.TakeActiveOpeningVisibilityApplications();

        _cameraGo = new GameObject("TestCamera");
        _cameraGo!.tag = "MainCamera";
        _cameraGo!.AddComponent<Camera>();

        var go = new GameObject("WallManager");
        _wallManager = go.AddComponent<WallManager>();
        _spawned.Add(go);

        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings, "KitchenSettings.Instance should be loadable from Resources");
        _prevWallsEnabled = settings.NormalView.wallsEnabled;
        _prevLowerNearWalls = settings.NormalView.lowerNearWalls;
        settings.NormalView.wallsEnabled = true;
        settings.NormalView.lowerNearWalls = false;
    }

    [TearDown]
    public void TearDown()
    {
        var settings = KitchenSettings.Instance;
        if (settings != null)
        {
            settings.NormalView.wallsEnabled = _prevWallsEnabled;
            settings.NormalView.lowerNearWalls = _prevLowerNearWalls;
        }

        PartRegistry.Clear();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);

        WallManager.TakeActiveOpeningVisibilityApplications();
    }

    /// <summary>Отрицательный контроль: стена с окном, ничего не меняется кадр от кадра —
    /// видимость окна обязана применяться один раз, а не на каждый опрос.</summary>
    [Test]
    public void SteadyWallWithWindow_RepeatedLateUpdate_AppliesVisibilityOnlyOnce()
    {
        var wallElement = MakeWall("Стена", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        var wall = wallElement.GetComponent<Wall>();
        MakeWindow(wall, wallElement.transform.position);

        _wallManager!.LateUpdate();
        Assert.Greater(WallManager.TakeActiveOpeningVisibilityApplications(), 0,
            "первый кадр обязан применить видимость хотя бы раз");

        for (int i = 0; i < 5; i++) _wallManager!.LateUpdate();

        Assert.AreEqual(0, WallManager.TakeActiveOpeningVisibilityApplications(),
            "стена и её окно не менялись пять кадров подряд — повторное применение видимости " +
            "не должно было произойти ни разу");
    }

    /// <summary>Положительный контроль к тесту выше: без него «ноль» было бы зелёным просто
    /// потому, что счётчик не работает вообще. Скрытие стены (окно тоже обязано спрятаться)
    /// обязано засчитаться.</summary>
    [Test]
    public void WallHiddenAfterSteadyState_NextLateUpdate_CountsAsActive()
    {
        var wallElement = MakeWall("Стена", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        var wall = wallElement.GetComponent<Wall>();
        MakeWindow(wall, wallElement.transform.position);

        _wallManager!.LateUpdate();
        for (int i = 0; i < 3; i++) _wallManager!.LateUpdate();
        WallManager.TakeActiveOpeningVisibilityApplications();

        KitchenSettings.Instance.NormalView.wallsEnabled = false;
        _wallManager!.LateUpdate();

        Assert.Greater(WallManager.TakeActiveOpeningVisibilityApplications(), 0,
            "видимость окна сменилась (стену выключили) — это обязано было засчитаться");
    }
}
