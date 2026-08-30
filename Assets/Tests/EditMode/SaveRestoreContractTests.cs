using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Контракт сохранения и восстановления: то, что фабрика умеет СОЗДАТЬ, файл
/// обязан УМЕТЬ СОХРАНИТЬ, а загрузчик — ВОССТАНОВИТЬ тем же типом. Расхождение
/// в любой из трёх таблиц — это потеря пользовательских данных, а не стилевая
/// придирка (CONVENTIONS.md → «A capability table has a twin in the contract»).
/// </summary>
public class SaveRestoreContractTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _globals;

    [SetUp]
    public void Setup()
    {
        _globals = ProjectLoadStateGuard.Capture();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();

        foreach (var e in UnityEngine.Object.FindObjectsByType<KitchenElement>())
            if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);

        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();
        _globals?.Restore();
    }

    private KitchenElement Register(GameObject go)
    {
        var el = go.GetComponent<KitchenElement>();
        _spawned.Add(go);
        PartRegistry.Register(el);
        return el;
    }

    private static string TypeSignature(KitchenElement el) =>
        el.GetComponent<Wall>() != null ? "Wall" : el.GetType().Name;

    private static List<GameObject> ReloadThroughFile(IEnumerable<KitchenElement> elements)
    {
        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(elements));
        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data, "проект должен читаться обратно из JSON");

        foreach (var e in UnityEngine.Object.FindObjectsByType<KitchenElement>())
            if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();

        return SaveLoadManager.RestoreScene(data!);
    }

    private static ElementData BareBoardData(string name)
    {
        var el = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), name, Vector3.zero)
            .GetComponent<KitchenElement>();
        var data = ElementData.FromElement(el);
        UnityEngine.Object.DestroyImmediate(el.gameObject);
        return data;
    }

    private static KitchenElement RestoreOne(ElementData data)
    {
        var project = new ProjectData(new[] { data });
        var created = SaveLoadManager.RestoreScene(project);
        Assert.AreEqual(1, created.Count, "восстановиться должен ровно один элемент");
        return created[0].GetComponent<KitchenElement>();
    }

    [Test]
    public void EveryTypeTheFactoryCanCreate_ComesBackAsTheSameType_AfterSaveAndLoad()
    {
        var made = new List<KitchenElement>
        {
            Register(ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Part", new Vector3(0f, 0.5f, 0f))),
            Register(ElementFactory.CreateWall(new Vector3Int(100, 2700, 3000), "WallA", new Vector3(-3f, 1.35f, 0f))),
            Register(ElementFactory.CreateFacade(new Vector3Int(450, 700, 18), "FacadeA", new Vector3(1f, 0.35f, 0f))),
            Register(ElementFactory.CreateAssembledFacade(new Vector3Int(600, 800, 18), "AssembledA", new Vector3(2f, 0.4f, 0f), AssembledFill.Glass)),
            Register(ElementFactory.Instance.CreatePanel(new Vector3Int(500, 700, 4), "PanelA", new Vector3(3f, 0.35f, 0f))),
            Register(ElementFactory.CreateRadialShelf(600, 400, 18, 200, "RadialA", new Vector3(4f, 0.5f, 0f))),
            Register(ElementFactory.CreateDrawer(DrawerType.C, 450, DrawerColor.Anthracite, 400, "DrawerA", new Vector3(5f, 0.3f, 0f))),
            Register(ElementFactory.CreateTable(new Vector3Int(1200, 750, 600), "TableA", new Vector3(6f, 0.375f, 0f))),
            Register(ElementFactory.CreateRadiusTable(new Vector3Int(1200, 750, 600), "RadiusTableA", new Vector3(8f, 0.375f, 0f))),
            Register(ElementFactory.CreatePillar(75, "PillarA", new Vector3(10f, 1.2f, 0f))),
            Register(ElementFactory.CreateFloor(new Vector3Int(3000, 20, 3000), "FloorA", new Vector3(0f, -0.01f, 6f))),
            Register(ElementFactory.CreateLightSource("LampA", new Vector3(11f, 2.2f, 0f))),
            Register(ElementFactory.CreateSink("SinkA", new Vector3(12f, 0.9f, 0f))),
            Register(ElementFactory.CreateCooktop("CooktopA", new Vector3(13f, 0.9f, 0f))),
            Register(ElementFactory.CreateOven("OvenA", new Vector3(14f, 0.4f, 0f))),
            Register(ElementFactory.CreateDishwasher("DishwasherA", new Vector3(15f, 0.4f, 0f))),
            Register(ElementFactory.CreateWindow(new Vector3Int(900, 1400, 100), "WindowA", new Vector3(16f, 1.4f, 0f))),
            Register(ElementFactory.CreateDoor(new Vector3Int(900, 2100, 100), "DoorA", new Vector3(18f, 1.05f, 0f))),
        };

        var before = made.Select(TypeSignature).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var after = ReloadThroughFile(made)
            .Select(go => go.GetComponent<KitchenElement>())
            .Where(el => el != null)
            .Select(TypeSignature)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var lost = before.Except(after).ToList();
        var invented = after.Except(before).ToList();
        Assert.IsEmpty(lost,
            "тип сохраняется, но восстанавливается ДРУГИМ — это потеря данных пользователя: "
            + string.Join(", ", lost));
        Assert.IsEmpty(invented,
            "загрузка вернула тип, которого в сцене не было: " + string.Join(", ", invented));
        Assert.AreEqual(before, after, "набор типов после загрузки обязан совпасть поэлементно");
    }

    [Test]
    public void Restore_OldSaveWithEdgeSkipValidation_MarksAllFourSidesManual()
    {
        var data = BareBoardData("LegacyEdges");
        data.edgeSkipValidation = true;
        data.edgeManualMask = 0;

        var el = RestoreOne(data);
        _spawned.Add(el.gameObject);

        Assert.AreEqual(EdgeManual.AllMask, el.EdgeManualMask,
            "в проектах старше сторон-по-отдельности общий флаг «не проверять кромки» "
            + "равнозначен «все четыре стороны ручные»; иначе такой проект после загрузки "
            + "внезапно начинает сыпать ошибками кромок");
    }

    [Test]
    public void Restore_EdgeManualMask_WinsOverTheLegacyFlag()
    {
        var data = BareBoardData("PerSideEdges");
        data.edgeSkipValidation = true;
        data.edgeManualMask = EdgeManual.Bit(EdgeSide.L1);

        var el = RestoreOne(data);
        _spawned.Add(el.gameObject);

        Assert.AreEqual(EdgeManual.Bit(EdgeSide.L1), el.EdgeManualMask,
            "новое поле точнее старого флага: маска сторон обязана пережить загрузку целиком");
    }

    [Test]
    public void Restore_FractionalEdge_IsPulledOntoTheMillimetreGrid_WithoutResizingThePart()
    {
        var dims = new Vector3Int(600, 18, 500);
        var el = Register(ElementFactory.CreatePart(dims, "OffGrid", new Vector3(0.0002f, 0.5f, 0f)));
        var data = ElementData.FromElement(el);
        UnityEngine.Object.DestroyImmediate(el.gameObject);
        _spawned.Clear();
        PartRegistry.Clear();

        var restored = RestoreOne(data);
        _spawned.Add(restored.gameObject);

        float minXmm = restored.GetVertices().Min(v => v.x) / AppConstants.MM_TO_UNITS;
        Assert.AreEqual(Mathf.Round(minXmm), minXmm, MmGrid.EpsMm,
            "грань на половине миллиметра — проём, в который не встаёт ни одна деталь целого "
            + "размера; загрузка выравнивает такие грани один раз");
        Assert.AreEqual(dims, restored.DimensionsMM,
            "сетка двигает деталь, но НЕ меняет её габарит: не тот размер чинит человек");
    }

    [Test]
    public void Restore_ToolbarToggles_TintAndLights_ComeBackFromTheFile()
    {
        var board = Register(ElementFactory.CreatePart(
            new Vector3Int(600, 18, 500), "ToggleBoard", new Vector3(0f, 0.5f, 0f)));

        ElementHighlighter.TintEnabled = false;
        LightSourceElement.SetGlobalOn(false);
        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(new[] { board }));

        ElementHighlighter.TintEnabled = true;
        LightSourceElement.SetGlobalOn(true);
        var data = SaveLoadManager.Deserialize(json);
        SaveLoadManager.RestoreScene(data!);

        Assert.IsFalse(ElementHighlighter.TintEnabled,
            "тумблер тонировки — часть рабочего места пользователя, он лежит в файле проекта");
        Assert.IsFalse(LightSourceElement.GlobalOn,
            "выключатель света — тоже часть проекта, а не рантайм-состояние");
    }

    [Test]
    public void Capture_PartRidingAnOpenFacade_StoresItsRestingPose_NotTheOffsetOne()
    {
        var facade = (FacadeElement)Register(ElementFactory.CreateFacade(
            new Vector3Int(450, 700, 18), "RideFacade", Vector3.zero));
        var board = Register(ElementFactory.CreatePart(
            new Vector3Int(400, 18, 300), "RideBoard", new Vector3(0f, -0.35f, 0.25f)));
        board.AttachedToName = facade.PartName;
        var restPosition = board.transform.position;

        facade.SetOpen(true);
        facade.StepDoor(1f);
        AttachRider.Step();
        Assert.AreNotEqual(restPosition, board.transform.position,
            "деталь обязана уехать вместе с фасадом — иначе тесту нечего проверять");

        var data = ElementData.FromElement(board);

        Assert.AreEqual(restPosition.x, data.Position.x, 1e-4f,
            "в файл идёт поза покоя: сохранив уехавший трансформ, проект открылся бы "
            + "с разъехавшейся сборкой");
        Assert.AreEqual(restPosition.y, data.Position.y, 1e-4f, "то же по вертикали");
        Assert.AreEqual(restPosition.z, data.Position.z, 1e-4f, "то же по глубине");
    }

    [Test]
    public void OldSaveWithoutTheNewerFields_ReadsBackWithSafeDefaults()
    {
        string json = "{\"version\":" + AppConstants.SAVE_FORMAT_VERSION
            + ",\"elements\":[{\"name\":\"Old\",\"dimensionsMM\":[600,18,500],"
            + "\"position\":[0,0.5,0],\"rotation\":[0,0,0,1]}]}";

        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data, "старый проект обязан читаться");
        var element = data!.elements[0];

        Assert.AreEqual(0, element.gapFront,
            "зазоры по толщине появились позже остальных: их отсутствие читается как ноль, "
            + "то есть ровно как было до их появления");
        Assert.IsTrue(element.edgeBanding, "кромка «из коробки» включена");
        Assert.AreEqual(AppConstants.EDGE_THICKNESS_DEFAULT_MM, element.edgeThicknessMM, 1e-4f,
            "толщина ленты берётся из умолчания, а не из нуля");
        Assert.AreEqual(AppConstants.RADIAL_CORNER_RADIUS_DEFAULT, element.cornerRadius,
            "радиус скругления — из умолчания");
        Assert.IsEmpty(element.grooves, "пазов в старом файле нет — пустой список, не null");
        Assert.IsEmpty(element.textureOverlays, "накладок тоже нет");
        Assert.AreEqual(LightSourceElement.DEFAULT_TEMPERATURE_K, element.lightTemperatureK,
            "параметры лампы берутся из её умолчаний");
        Assert.AreEqual("", element.cooktopModel, "варочная старого проекта — свободная");
        Assert.AreEqual(0f, element.cooktopYawDeg, 1e-4f, "своего разворота у неё тогда не было");
        Assert.IsFalse(element.isOven, "духовки в старых проектах не бывает");
        Assert.IsFalse(element.isDishwasher, "и посудомойки тоже");

        Assert.IsTrue(data.tintEnabled, "тумблеры вида — из умолчаний приложения");
        Assert.IsTrue(data.lightsOn, "свет включён");
        Assert.IsEmpty(data.windows, "окна проекта остаются на местах по умолчанию");
        Assert.AreEqual("", data.projectInstructions, "инструкций проекта тогда не было");
        Assert.IsFalse(data.basePlateValid,
            "иначе JsonUtility отдал бы пустой ElementData вместо null и подложка обнулилась бы");
        Assert.IsEmpty(data.undoHistory, "истории нет — пустой массив");
        Assert.AreEqual("Resize", data.handleMode, "режим ручек по умолчанию");
    }

    [Test]
    public void SaveProject_ArchivesThePreviousFile_OnlyWhenAskedTo()
    {
        string name = "backup_probe_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
        string backups = System.IO.Path.Combine(SaveLoadManager.SavesDirectory, "backups");

        Assert.IsTrue(SaveLoadManager.SaveProject(name), "первый файл записан");
        Assert.IsTrue(SaveLoadManager.SaveProject(name), "второй раз поверх — с архивацией");
        Assert.AreEqual(1, ZipsFor(backups, name), "прежняя версия уехала в zip");

        Assert.IsTrue(SaveLoadManager.SaveProject(name, backup: false), "автосохранение — без архива");
        Assert.AreEqual(1, ZipsFor(backups, name),
            "автосохранение идёт каждую минуту: с архивацией zip'ы плодились бы бесконечно");

        System.IO.File.Delete(SaveLoadManager.PathForName(name));
        foreach (var zip in ZipPaths(backups, name)) System.IO.File.Delete(zip);
    }

    private static string[] ZipPaths(string backups, string name) =>
        System.IO.Directory.Exists(backups)
            ? System.IO.Directory.GetFiles(backups, name + "_*.zip")
            : new string[0];

    private static int ZipsFor(string backups, string name) => ZipPaths(backups, name).Length;

    [Test]
    public void OldSaveWithoutASettingsBlock_KeepsSnapGridAndAutosaveOn()
    {
        string json = "{\"version\":" + AppConstants.SAVE_FORMAT_VERSION + ",\"elements\":[]}";

        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data!.settings,
            "JsonUtility создаёт вложенный объект даже когда его нет в JSON — «настроек "
            + "нет» здесь неотличимо от «настройки нулевые», и защищают только "
            + "инициализаторы полей");

        var settings = data.settings!;
        Assert.AreEqual(18, settings.gridStep, "шаг сетки — как «из коробки», а не 0");
        Assert.IsTrue(settings.gridEnabled, "сетка остаётся включённой");
        Assert.IsTrue(settings.snapEnabled,
            "проект, сохранённый до появления блока настроек, не имеет права выключить "
            + "пользователю привязку");
        Assert.AreEqual(50f, settings.snapThreshold, 1e-4f, "порог привязки — умолчание, не 1 мм");
        Assert.IsTrue(settings.blockOnViolation, "блокировка по нарушениям остаётся");
        Assert.IsTrue(settings.autoSave, "автосохранение остаётся включённым");
        Assert.AreEqual(60, settings.autoSaveInterval, "интервал автосохранения — минута, не 10 с");
        Assert.IsTrue(settings.windowedMode, "оконный режим остаётся");

        KitchenSettings.Instance.ResetToDefaults();
        SaveLoadManager.RestoreScene(data);

        Assert.IsTrue(KitchenSettings.Instance.SnapEnabled,
            "и то же самое после реальной загрузки: старый проект открывается с рабочей "
            + "привязкой, а не с молча выключенной");
        Assert.IsTrue(KitchenSettings.Instance.AutoSave, "и с работающим автосохранением");
    }
}
