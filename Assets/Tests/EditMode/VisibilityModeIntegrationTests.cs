using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Интеграционные тесты видимости: режимы «Обычный» / «Помещение» /
/// «Фото» против пресетов вида (стены, объекты, контуры, свет, опускание).
/// Спецификация — таблица <see cref="ViewResolver"/>: что режим форсирует, что
/// берёт из пресета и что при этом можно менять руками.</summary>
public class VisibilityModeIntegrationTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private GameObject? _cameraGo;
    private WallManager? _wallManager;
    private KitchenSettingsData? _settingsBackup;

    // Элементы сцены
    private GameObject _wallFront = null!;
    private GameObject _wallPart = null!;
    private GameObject _window = null!;
    private GameObject _door = null!;
    private GameObject _shelf = null!;
    private GameObject _lamp = null!;

    private KitchenElement _wallFrontEl = null!;
    private KitchenElement _windowEl = null!;
    private KitchenElement _doorEl = null!;
    private KitchenElement _shelfEl = null!;

    private Wall _wallFrontComp = null!;
    private Wall _wallPartComp = null!;

    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s, "Resources/KitchenSettings.asset не найден");
        _settingsBackup = s.ToData();
        s.ResetToDefaults();

        // ── Камера ────────────────────────────────────────────
        _cameraGo = new GameObject("TestCamera");
        _cameraGo.tag = "MainCamera";
        _cameraGo.AddComponent<Camera>();
        _cameraGo.transform.position = new Vector3(0f, 1.25f, -5f);
        _cameraGo.transform.LookAt(Vector3.zero);

        // ── WallManager ───────────────────────────────────────
        var wmGo = new GameObject("WallManager");
        _wallManager = wmGo.AddComponent<WallManager>();
        _spawned.Add(wmGo);

        BuildScene();

        EditModeManager.Reset();

        // Хеш «уже применённых настроек» статический и переживает тест: без
        // сброса Apply() выйдет рано и оставит рендереры от соседнего теста,
        // а ассерты пройдут по чужому состоянию.
        SceneVisibilityManager.Invalidate();
    }

    [TearDown]
    public void TearDown()
    {
        EditModeManager.Reset();
        PartRegistry.Clear();
        SceneVisibilityManager.Invalidate();

        foreach (var g in _spawned)
            if (g != null) Object.DestroyImmediate(g);
        _spawned.Clear();

        if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
        _cameraGo = null;

        var s = KitchenSettings.Instance;
        if (s != null && _settingsBackup != null) s.ApplyFrom(_settingsBackup);
        _settingsBackup = null;
    }

    private GameObject Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go;
    }

    private void BuildScene()
    {
        // Передняя стена (на неё будем вешать окно и дверь)
        _wallFront = Spawn(ElementFactory.CreateWall(
            new Vector3Int(3000, 2500, 100), "WallFront",
            new Vector3(0f, 1.25f, -1.5f)));
        _wallFrontEl = _wallFront.GetComponent<KitchenElement>()!;
        _wallFrontComp = _wallFront.GetComponent<Wall>()!;

        // Перегородка (перпендикулярна передней)
        _wallPart = Spawn(ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "WallPart",
            new Vector3(0f, 1.25f, 0f)));
        _wallPartComp = _wallPart.GetComponent<Wall>()!;

        // Окно на передней стене
        _window = Spawn(ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win",
            new Vector3(0.6f, 1.2f, -1.5f)));
        _windowEl = _window.GetComponent<KitchenElement>()!;
        _window.GetComponent<WindowElement>()!.SnapToWall();

        // Дверь на передней стене (слева от окна)
        _door = Spawn(ElementFactory.CreateDoor(
            new Vector3Int(900, 2000, 100), "Door",
            new Vector3(-0.6f, 1.0f, -1.5f)));
        _doorEl = _door.GetComponent<KitchenElement>()!;
        _door.GetComponent<DoorElement>()!.SnapToWall();

        // Деталь (полка)
        _shelf = Spawn(ElementFactory.CreatePart(
            new Vector3Int(600, 18, 500), "Shelf",
            new Vector3(0.5f, 0.5f, 0.2f)));
        _shelfEl = _shelf.GetComponent<KitchenElement>()!;

        // Источник света
        _lamp = Spawn(ElementFactory.CreateLightSource(
            "Lamp", new Vector3(0f, 2.5f, 0.5f)));
    }

    private void ApplyVisibility()
    {
        _wallManager!.LateUpdate();
        SceneVisibilityManager.Apply();
    }

    private static ViewPreset Normal => KitchenSettings.Instance.NormalView;
    private static ViewPreset Room => KitchenSettings.Instance.RoomView;

    // ═══════════════════════════════════════════════════════════
    //  1. Таблица режимов — исполняемая спецификация
    // ═══════════════════════════════════════════════════════════

    // Пресет, в котором КАЖДОЕ поле отличается от «из коробки»: так видно,
    // где режим подставил своё значение, а где взял пользовательское.
    private static void SetDistinctivePreset(ViewPreset p)
    {
        p.wallsEnabled = false;
        p.wallOutline = false;
        p.lowerNearWalls = true;
        p.hideOpeningsOnLoweredWalls = true;
        p.objectsVisible = false;
        p.edgeOutline = false;
        p.hideLightSources = true;
    }

    // Обычный режим: всё из пресета, всё редактируется.
    [TestCase(ViewField.Walls, false, true)]
    [TestCase(ViewField.WallOutline, false, true)]
    [TestCase(ViewField.LowerNearWalls, true, true)]
    [TestCase(ViewField.HideOpeningsOnLoweredWalls, true, true)]
    [TestCase(ViewField.Objects, false, true)]
    [TestCase(ViewField.ObjectOutline, false, true)]
    [TestCase(ViewField.HideLightSources, true, true)]
    public void Matrix_NormalMode(ViewField field, bool value, bool editable)
        => AssertMatrix(EditMode.Normal, field, value, editable);

    // Помещение: стены целые и не опускаются (форс), остальное — на усмотрение.
    [TestCase(ViewField.Walls, true, false)]
    [TestCase(ViewField.WallOutline, false, true)]
    [TestCase(ViewField.LowerNearWalls, false, false)]
    [TestCase(ViewField.HideOpeningsOnLoweredWalls, false, false)]
    [TestCase(ViewField.Objects, false, true)]
    [TestCase(ViewField.ObjectOutline, false, true)]
    [TestCase(ViewField.HideLightSources, true, true)]
    public void Matrix_RoomMode(ViewField field, bool value, bool editable)
        => AssertMatrix(EditMode.Room, field, value, editable);

    // Фото: видно всё, менять нельзя ничего. Контуры — не сокрытие геометрии,
    // поэтому берутся из пресета обычного режима.
    [TestCase(ViewField.Walls, true, false)]
    [TestCase(ViewField.WallOutline, false, false)]
    [TestCase(ViewField.LowerNearWalls, false, false)]
    [TestCase(ViewField.HideOpeningsOnLoweredWalls, false, false)]
    [TestCase(ViewField.Objects, true, false)]
    [TestCase(ViewField.ObjectOutline, false, false)]
    [TestCase(ViewField.HideLightSources, false, false)]
    public void Matrix_PhotoMode(ViewField field, bool value, bool editable)
        => AssertMatrix(EditMode.Photo, field, value, editable);

    private static void AssertMatrix(EditMode mode, ViewField field, bool value, bool editable)
    {
        SetDistinctivePreset(Normal);
        SetDistinctivePreset(Room);
        EditModeManager.SetMode(mode);

        Assert.AreEqual(value, ViewResolver.Current.Get(field),
            $"{mode}/{field}: эффективное значение");
        // Пресет, открытый на вкладке, — тот же, что правит режим.
        var presetMode = mode == EditMode.Room ? EditMode.Room : EditMode.Normal;
        Assert.AreEqual(editable, ViewResolver.IsEditable(mode, presetMode, field),
            $"{mode}/{field}: доступность тумблера");
    }

    // ═══════════════════════════════════════════════════════════
    //  2. Обычный режим
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void NormalMode_Defaults_EverythingVisible()
    {
        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();

        Assert.IsTrue(_wallFront.GetComponent<MeshRenderer>()!.enabled, "стена видна");
        Assert.IsTrue(_wallPart.GetComponent<MeshRenderer>()!.enabled, "перегородка видна");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_windowEl), "окно видно");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_doorEl), "дверь видна");
        Assert.IsTrue(_shelf.GetComponent<MeshRenderer>()!.enabled, "деталь видна");
        Assert.IsTrue(_lamp.GetComponent<MeshRenderer>()!.enabled, "плафон виден");
    }

    /// <summary>Окна и двери — часть стены: нет стены, нет и рамы, иначе она
    /// висит в воздухе.</summary>
    [Test]
    public void NormalMode_WallsDisabled_HidesWallsWithTheirOpenings()
    {
        Normal.wallsEnabled = false;

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();

        Assert.IsFalse(_wallFront.GetComponent<MeshRenderer>()!.enabled, "стена скрыта");
        Assert.IsFalse(_wallPart.GetComponent<MeshRenderer>()!.enabled, "перегородка скрыта");
        Assert.IsFalse(_wallFrontComp.IsLowered, "опущенная стена восстановлена");
        Assert.IsFalse(SceneVisibility.AnyRendererEnabled(_windowEl),
            "окно скрыто вместе со стеной");
        Assert.IsFalse(SceneVisibility.AnyRendererEnabled(_doorEl),
            "дверь скрыта вместе со стеной");
    }

    [Test]
    public void NormalMode_WallsDisabled_DisablesOpeningColliders()
    {
        Normal.wallsEnabled = false;

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();

        Assert.IsFalse(_wallFrontComp.IsLowered, "стена восстановлена");
        Assert.IsFalse(SceneVisibility.AnyRendererEnabled(_windowEl), "окно скрыто");
        Assert.IsFalse(SceneVisibility.AnyRendererEnabled(_doorEl), "дверь скрыта");

        var windowCollider = _window.GetComponent<Collider>();
        Assert.IsNotNull(windowCollider, "у окна есть коллайдер");
        Assert.IsFalse(windowCollider!.enabled, "коллайдер окна выключен вместе со стеной");

        var doorCollider = _door.GetComponent<Collider>();
        Assert.IsNotNull(doorCollider, "у двери есть коллайдер");
        Assert.IsFalse(doorCollider!.enabled, "коллайдер двери выключен вместе со стеной");
    }

    [Test]
    public void NormalMode_ObjectsVisibleFalse_HidesDetailsAndLamp()
    {
        Normal.objectsVisible = false;

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();

        Assert.IsFalse(_shelf.GetComponent<MeshRenderer>()!.enabled, "деталь скрыта");
        Assert.IsFalse(_lamp.GetComponent<MeshRenderer>()!.enabled, "плафон скрыт");

        Assert.IsTrue(_wallFront.GetComponent<MeshRenderer>()!.enabled,
            "стена не «объект», остаётся видна");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_windowEl),
            "окно не «объект», остаётся видно");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_doorEl),
            "дверь не «объект», остаётся видна");
    }

    [Test]
    public void NormalMode_HideLightSources_HidesOnlyLamp()
    {
        Normal.hideLightSources = true;

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();

        Assert.IsFalse(_lamp.GetComponent<MeshRenderer>()!.enabled,
            "плафон скрыт флагом «скрыть источники света»");
        Assert.IsTrue(_shelf.GetComponent<MeshRenderer>()!.enabled, "деталь не задета");
    }

    [Test]
    public void NormalMode_LowerNearWalls_LowersFrontWall_OpeningsStayVisible()
    {
        Normal.lowerNearWalls = true;
        Normal.hideOpeningsOnLoweredWalls = false;

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();

        Assert.IsTrue(_wallFrontComp.IsLowered, "передняя стена перед камерой — опускается");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_windowEl), "окно видно");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_doorEl), "дверь видна");
    }

    [Test]
    public void NormalMode_HideOpeningsOnLoweredWalls_HidesWindowAndDoor()
    {
        Normal.lowerNearWalls = true;
        Normal.hideOpeningsOnLoweredWalls = true;

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();

        Assert.IsTrue(_wallFrontComp.IsLowered, "стена опущена");
        Assert.IsFalse(SceneVisibility.AnyRendererEnabled(_windowEl), "окно опущенной стены скрыто");
        Assert.IsFalse(SceneVisibility.AnyRendererEnabled(_doorEl), "дверь опущенной стены скрыта");

        var windowCollider = _window.GetComponent<Collider>();
        Assert.IsNotNull(windowCollider, "у окна есть коллайдер");
        Assert.IsFalse(windowCollider!.enabled, "коллайдер окна выключен — клик не должен его задеть");

        var doorCollider = _door.GetComponent<Collider>();
        Assert.IsNotNull(doorCollider, "у двери есть коллайдер");
        Assert.IsFalse(doorCollider!.enabled, "коллайдер двери выключен — клик не должен его задеть");

        // Привязка сохраняется — вырез в стене часть её меша, а не окна.
        Assert.GreaterOrEqual(_wallFrontComp.AttachedWindows.Count, 1, "окно всё ещё AttachedWindow");
        Assert.GreaterOrEqual(_wallFrontComp.AttachedDoors.Count, 1, "дверь всё ещё AttachedDoor");
    }

    // ═══════════════════════════════════════════════════════════
    //  3. Фоторежим — видно всё
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void PhotoMode_ShowsEverything_RegardlessOfPreset()
    {
        SetDistinctivePreset(Normal);   // всё погашено и опускается

        EditModeManager.SetMode(EditMode.Photo);
        ApplyVisibility();

        Assert.IsTrue(PhotoMode.Active, "фоторежим активен");
        Assert.IsTrue(_wallFront.GetComponent<MeshRenderer>()!.enabled, "стена видна");
        Assert.IsTrue(_wallPart.GetComponent<MeshRenderer>()!.enabled, "перегородка видна");
        Assert.IsFalse(_wallFrontComp.IsLowered, "стены не опускаются");
        Assert.IsFalse(_wallPartComp.IsLowered, "перегородка не опускается");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_windowEl), "окно видно");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_doorEl), "дверь видна");
        Assert.IsTrue(_shelf.GetComponent<MeshRenderer>()!.enabled, "деталь видна");
        Assert.IsTrue(_lamp.GetComponent<MeshRenderer>()!.enabled,
            "плафон виден: «скрыть источники света» в фоторежиме не действует");
    }

    /// <summary>Вход и выход не трогают сохраняемые настройки: фоторежим только
    /// подставляет свои значения поверх.</summary>
    [Test]
    public void PhotoMode_DoesNotWriteToPresets()
    {
        SetDistinctivePreset(Normal);
        SetDistinctivePreset(Room);
        var normalBefore = Normal.Clone();
        var roomBefore = Room.Clone();

        EditModeManager.SetMode(EditMode.Photo);
        ApplyVisibility();
        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();

        foreach (ViewField f in System.Enum.GetValues(typeof(ViewField)))
        {
            Assert.AreEqual(normalBefore.Get(f), Normal.Get(f), $"пресет «обычный», {f}");
            Assert.AreEqual(roomBefore.Get(f), Room.Get(f), $"пресет «помещение», {f}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  4. Режим «помещение»
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void RoomMode_WallsForcedVisibleAndFullHeight()
    {
        SetDistinctivePreset(Room);      // стены выключены, опускание включено

        EditModeManager.SetMode(EditMode.Room);
        ApplyVisibility();

        Assert.IsTrue(_wallFront.GetComponent<MeshRenderer>()!.enabled,
            "в «помещении» стены показываются принудительно");
        Assert.IsFalse(_wallFrontComp.IsLowered, "и не опускаются");
        Assert.IsFalse(_wallPartComp.IsLowered, "перегородка тоже");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_windowEl), "окно видно");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_doorEl), "дверь видна");
    }

    /// <summary>Кликабельность отдельна от видимости: режим «помещение»
    /// существует ради правки стен, и они обязаны ловить клик.</summary>
    [Test]
    public void RoomMode_WallsStaySelectable()
    {
        SetDistinctivePreset(Room);

        EditModeManager.SetMode(EditMode.Room);
        ApplyVisibility();

        Assert.IsTrue(EditModeManager.IsInteractable(_wallFrontEl),
            "стена интерактивна в режиме «помещение»");
        var wallCollider = _wallFront.GetComponent<Collider>();
        Assert.IsNotNull(wallCollider, "у стены должен быть коллайдер");
        Assert.IsTrue(wallCollider!.enabled, "коллайдер включён");
    }

    [Test]
    public void RoomMode_ObjectsVisibleFalse_HidesObjectsOnly()
    {
        Room.objectsVisible = false;

        EditModeManager.SetMode(EditMode.Room);
        ApplyVisibility();

        Assert.IsFalse(_shelf.GetComponent<MeshRenderer>()!.enabled, "деталь скрыта");
        Assert.IsFalse(_lamp.GetComponent<MeshRenderer>()!.enabled, "плафон скрыт");

        Assert.IsTrue(_wallFront.GetComponent<MeshRenderer>()!.enabled, "стена видна — не объект");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_windowEl), "окно видно — не объект");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_doorEl), "дверь видна — не объект");
    }

    // ═══════════════════════════════════════════════════════════
    //  5. Переключение режимов: пресеты независимы и восстанавливаются
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void SwitchingModes_RestoresNormalPreset()
    {
        Normal.wallsEnabled = false;

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();
        Assert.IsFalse(_wallFront.GetComponent<MeshRenderer>()!.enabled, "стены выключены");

        EditModeManager.SetMode(EditMode.Room);
        ApplyVisibility();
        Assert.IsTrue(_wallFront.GetComponent<MeshRenderer>()!.enabled, "в «помещении» стены есть");

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();
        Assert.IsFalse(_wallFront.GetComponent<MeshRenderer>()!.enabled,
            "настройка обычного режима вернулась");
    }

    [Test]
    public void EditingRoomPreset_DoesNotTouchNormalPreset()
    {
        Normal.objectsVisible = true;
        Room.objectsVisible = false;

        EditModeManager.SetMode(EditMode.Room);
        ApplyVisibility();
        Assert.IsFalse(_shelf.GetComponent<MeshRenderer>()!.enabled, "в «помещении» деталь скрыта");

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();
        Assert.IsTrue(_shelf.GetComponent<MeshRenderer>()!.enabled, "в обычном режиме деталь видна");
        Assert.IsTrue(Normal.objectsVisible, "пресет обычного режима не тронут");
    }

    [Test]
    public void CycleNormal_Photo_Normal_RestoresWallLowering()
    {
        Normal.lowerNearWalls = true;
        Normal.wallsEnabled = true;

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();
        Assert.IsTrue(_wallFrontComp.IsLowered, "Normal: стена опущена");

        EditModeManager.SetMode(EditMode.Photo);
        ApplyVisibility();
        Assert.IsFalse(_wallFrontComp.IsLowered, "Photo: стена НЕ опущена");
        Assert.IsTrue(_wallFront.GetComponent<MeshRenderer>()!.enabled, "Photo: стена видна");

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();
        Assert.IsTrue(_wallFrontComp.IsLowered, "после Photo→Normal опускание восстанавливается");
    }

    [Test]
    public void SwitchFromNormalToPhoto_KeepsOpeningsVisible()
    {
        Normal.lowerNearWalls = true;
        Normal.hideOpeningsOnLoweredWalls = true;

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();
        Assert.IsTrue(_wallFrontComp.IsLowered);
        Assert.IsFalse(SceneVisibility.AnyRendererEnabled(_windowEl), "Normal: окно скрыто");
        Assert.IsFalse(SceneVisibility.AnyRendererEnabled(_doorEl), "Normal: дверь скрыта");

        EditModeManager.SetMode(EditMode.Photo);
        ApplyVisibility();
        Assert.IsFalse(_wallFrontComp.IsLowered, "Photo: стена НЕ опущена");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_windowEl), "Photo: окно снова видно");
        Assert.IsTrue(SceneVisibility.AnyRendererEnabled(_doorEl), "Photo: дверь снова видна");

        EditModeManager.SetMode(EditMode.Normal);
        ApplyVisibility();
        Assert.IsTrue(_wallFrontComp.IsLowered, "Normal: стена снова опущена");
        Assert.IsFalse(SceneVisibility.AnyRendererEnabled(_windowEl), "Normal: окно снова скрыто");
        Assert.IsFalse(SceneVisibility.AnyRendererEnabled(_doorEl), "Normal: дверь снова скрыта");
    }

    // ═══════════════════════════════════════════════════════════
    //  6. Режим — один источник истины
    // ═══════════════════════════════════════════════════════════

    /// <summary>Reset идёт через SetMode: фоторежим не может остаться включённым
    /// при Mode = Normal (раньше оставался и ломал изоляцию тестов).</summary>
    [Test]
    public void Reset_TurnsPhotoModeOff()
    {
        EditModeManager.SetMode(EditMode.Photo);
        Assert.IsTrue(PhotoMode.Active, "фоторежим активен");

        EditModeManager.Reset();

        Assert.AreEqual(EditMode.Normal, EditModeManager.Mode, "режим сброшен");
        Assert.IsFalse(PhotoMode.Active, "фоторежим выключен вместе с ним");
    }

    [Test]
    public void AfterReset_WallManagerLowersWallsAgain()
    {
        PhotoMode.SetActive(true);
        EditModeManager.Reset();

        Normal.lowerNearWalls = true;
        Normal.wallsEnabled = true;

        _wallManager!.LateUpdate();

        Assert.IsTrue(_wallFrontComp.IsLowered,
            "в обычном режиме стена опускается — состояние фоторежима не утекло");
    }

    [Test]
    public void PhotoModeSetActive_DrivesEditMode()
    {
        PhotoMode.SetActive(true);
        Assert.AreEqual(EditMode.Photo, EditModeManager.Mode);

        PhotoMode.SetActive(false);
        Assert.AreEqual(EditMode.Normal, EditModeManager.Mode);
    }

    // ═══════════════════════════════════════════════════════════
    //  7. Доступность категорий для выделения
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void IsCategoryActive_NormalMode_RegularAccessible_RoomNot()
    {
        EditModeManager.SetMode(EditMode.Normal);

        Assert.IsTrue(EditModeManager.IsCategoryActive(EditModeManager.Category.Regular));
        Assert.IsFalse(EditModeManager.IsCategoryActive(EditModeManager.Category.Room));
        Assert.IsTrue(EditModeManager.IsCategoryActive(EditModeManager.Category.Always));
    }

    [Test]
    public void IsCategoryActive_RoomMode_RoomAccessible_RegularNot()
    {
        EditModeManager.SetMode(EditMode.Room);

        Assert.IsTrue(EditModeManager.IsCategoryActive(EditModeManager.Category.Room));
        Assert.IsFalse(EditModeManager.IsCategoryActive(EditModeManager.Category.Regular));
        Assert.IsTrue(EditModeManager.IsCategoryActive(EditModeManager.Category.Always));
    }

    [Test]
    public void IsCategoryActive_PhotoMode_SameAsNormal()
    {
        EditModeManager.SetMode(EditMode.Photo);

        Assert.IsTrue(EditModeManager.IsCategoryActive(EditModeManager.Category.Regular));
        Assert.IsFalse(EditModeManager.IsCategoryActive(EditModeManager.Category.Room));
        Assert.IsTrue(EditModeManager.IsCategoryActive(EditModeManager.Category.Always));
    }

    [Test]
    public void IsInteractable_Wall_InRoomOnly()
    {
        Assert.IsFalse(EditModeManager.IsInteractable(_wallFrontEl), "Normal: стена не интерактивна");

        EditModeManager.SetMode(EditMode.Room);
        Assert.IsTrue(EditModeManager.IsInteractable(_wallFrontEl), "Room: стена интерактивна");

        EditModeManager.SetMode(EditMode.Photo);
        Assert.IsFalse(EditModeManager.IsInteractable(_wallFrontEl), "Photo: стена не интерактивна");
    }

    [Test]
    public void IsInteractable_Shelf_InNormalAndPhotoNotRoom()
    {
        Assert.IsTrue(EditModeManager.IsInteractable(_shelfEl), "Normal: деталь интерактивна");

        EditModeManager.SetMode(EditMode.Room);
        Assert.IsFalse(EditModeManager.IsInteractable(_shelfEl), "Room: деталь НЕ интерактивна");

        EditModeManager.SetMode(EditMode.Photo);
        Assert.IsTrue(EditModeManager.IsInteractable(_shelfEl), "Photo: деталь интерактивна");
    }

    [Test]
    public void IsInteractable_WindowAndDoor_AlwaysInteractable()
    {
        foreach (var mode in new[] { EditMode.Normal, EditMode.Room, EditMode.Photo })
        {
            EditModeManager.SetMode(mode);
            Assert.IsTrue(EditModeManager.IsInteractable(_windowEl), $"{mode}: окно всегда интерактивно");
            Assert.IsTrue(EditModeManager.IsInteractable(_doorEl), $"{mode}: дверь всегда интерактивна");
        }
    }
}
