using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.UI;

/// <summary>
/// Поворот встраиваемой техники. Два правила, оба ломались по-своему:
///
///   • варочная поверхность ОБЯЗАНА поворачиваться вокруг нормали столешницы.
///     Позу ей целиком диктовала деталь-хозяин (AlignToPart собирал её из одного
///     поворота детали), поэтому разворот пользователя жил ровно один кадр —
///     следующий SnapToPart стирал его. Теперь у панели есть собственный
///     <see cref="CooktopElement.YawDeg"/>, и вместе с ней разворачивается проём;
///
///   • у ВСЕЙ техники осмыслен только разворот вокруг вертикали. Поля X/Z в окне
///     свойств не показываются вовсе (не гасятся — убираются), а MCP отвечает
///     отказом, а не молча правит ось.
/// </summary>
public class ApplianceRotationTests : McpTestFixture
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private ProjectLoadStateGuard? _globals;

    private const float ToU = AppConstants.MM_TO_UNITS;
    private const int TopThicknessMM = 38;

    /// <summary>Панель строится ОДИН раз на класс: сборка контекстного меню — ~0,31 с,
    /// и три сборки это почти секунда прогона EditMode за панель, которую продукт
    /// собирает единожды и дальше только переоткрывает. Почему это безопасно — в сводке
    /// <see cref="ContextMenuLayoutTests"/>: боевой сценарий и есть ОДНА панель, а
    /// <c>Open</c> её же и сбрасывает, и через <c>Open</c> здесь проходит каждый тест,
    /// который панель вообще трогает.
    ///
    /// Своего <c>SelectionManager</c> класс не заводит: в EditMode <c>Awake</c> не
    /// зовётся и <c>SelectionManager.Instance</c> пуст, так что подписка панели ни на
    /// что не указывает ни при общей панели, ни при потестовой.</summary>
    [OneTimeSetUp]
    public void BuildThePanelOnce()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [OneTimeTearDown]
    public void DropThePanel()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        _menu = null;
        _canvas = null;
    }

    /// <summary>Панель переживает тест — значит потестовое состояние сбрасывается здесь.
    /// <c>ForgetLastApplyFrame</c> — окно склейки правок: в EditMode
    /// <c>Time.frameCount</c> стоит на месте, и окно, взведённое предыдущим тестом,
    /// съело бы первую правку следующего. <c>DisarmAll</c> снимает взвод кнопок
    /// удаления, а фокус — потому что <c>RefreshUnfocused</c> МОЛЧА пропускает
    /// сфокусированное поле, а <c>EventSystem</c> в EditMode один на весь прогон.</summary>
    [SetUp]
    public void SetUp()
    {
        _globals = ProjectLoadStateGuard.Capture();
        GroupManager.Clear();
        CommandStack.Clear();
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
        ConfirmDeleteButton.DisarmAll();
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null) es.SetSelectedGameObject(null);
    }

    /// <summary><c>Close()</c> обязан идти ДО уничтожения спавнов: он обнуляет
    /// <c>_target</c> панели, иначе живая панель осталась бы с уничтоженной деталью в
    /// руках и уехала бы с ней в следующий тест.</summary>
    [TearDown]
    public void TearDown()
    {
        if (_menu != null) _menu!.Close();

        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);

        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();
        _globals!.Restore();
    }

    // ── Сцена ───────────────────────────────────────────────────────────

    /// <summary>Столешница: доска, положенная пластью вверх (та же поза, что в
    /// CooktopElementTests) — «верх» у неё по локальной Z.</summary>
    private KitchenElement CreateCountertop(int widthMM = 1200, int depthMM = 650)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = "Countertop";
        el.transform.rotation = ManagedRotation.Euler(-90f, 0f, 0f);
        el.DimensionsMM = new Vector3Int(widthMM, depthMM, TopThicknessMM);
        PartRegistry.Register(el);
        return el;
    }

    private CooktopElement SeatCooktop(KitchenElement top)
    {
        var go = new GameObject("Cooktop");
        _spawned.Add(go);
        go.transform.position = new Vector3(0f, top.transform.position.y + TopThicknessMM * 0.5f * ToU + 0.05f, 0f);
        var cooktop = go.AddComponent<CooktopElement>();
        cooktop.PartName = "Cooktop";
        cooktop.DimensionsMM = new Vector3Int(
            CooktopElement.DEFAULT_WIDTH_MM, CooktopElement.DEFAULT_HEIGHT_MM, CooktopElement.DEFAULT_DEPTH_MM);
        PartRegistry.Register(cooktop);
        cooktop.SnapToPart();
        Assert.IsTrue(cooktop.IsAttached, "варочная должна сесть на столешницу");
        return cooktop;
    }

    /// <summary>Угол разворота панели вокруг мировой вертикали (°, 0..360).</summary>
    private static float WorldYaw(CooktopElement hob) =>
        Mathf.Repeat(hob.transform.rotation.eulerAngles.y, 360f);

    // ── Баг 1: поворот варочной ─────────────────────────────────────────

    [Test]
    public void Cooktop_UserRotation_SurvivesTheNextSnap()
    {
        var top = CreateCountertop();
        var hob = SeatCooktop(top);
        Assert.AreEqual(0f, hob.YawDeg, 0.01f, "севшая ровно панель развёрнута на 0°");

        hob.RotateAroundAxis(Vector3.up, 90f);
        hob.SnapToPart();

        Assert.AreEqual(90f, hob.YawDeg, 0.01f, "поворот пользователя стал собственным разворотом панели");
        Assert.AreEqual(90f, WorldYaw(hob), 0.05f);

        // Ровно тот путь, которым баг и проявлялся: следующие кадры зовут
        // SnapToPart ещё и ещё, и поворот «возвращался назад».
        for (int frame = 0; frame < 5; frame++) hob.SnapToPart();

        Assert.AreEqual(90f, hob.YawDeg, 0.01f, "разворот не откатился на следующем кадре");
        Assert.AreEqual(90f, WorldYaw(hob), 0.05f);
        Assert.IsTrue(hob.IsAttached, "панель осталась во врезке");
    }

    [Test]
    public void Cooktop_RotationAccumulates()
    {
        var top = CreateCountertop();
        var hob = SeatCooktop(top);

        for (int i = 0; i < 3; i++)
        {
            hob.RotateAroundAxis(Vector3.up, 30f);
            hob.SnapToPart();
        }

        Assert.AreEqual(90f, hob.YawDeg, 0.05f, "три поворота по 30° складываются в 90°");
    }

    [Test]
    public void Cooktop_Rotated90_TurnsTheHoleWithIt()
    {
        var top = CreateCountertop(1200, 650);
        var hob = SeatCooktop(top);

        var before = top.CutoutHoleRects()[0];
        Assert.AreEqual(hob.CutoutWidthMM / 1200f, before.xMax - before.xMin, 1e-4f);
        Assert.AreEqual(hob.CutoutDepthMM / 650f, before.yMax - before.yMin, 1e-4f);

        hob.RotateAroundAxis(Vector3.up, 90f);
        hob.SnapToPart();

        var after = top.CutoutHoleRects()[0];
        // Развернули панель — ширина и глубина проёма поменялись местами.
        Assert.AreEqual(hob.CutoutDepthMM / 1200f, after.xMax - after.xMin, 1e-4f,
            "по ширине столешницы проём стал глубиной ниши");
        Assert.AreEqual(hob.CutoutWidthMM / 650f, after.yMax - after.yMin, 1e-4f,
            "а по её глубине — шириной ниши");
    }

    [Test]
    public void Cooktop_Rotated90_SwapsMinimalPartSize()
    {
        var top = CreateCountertop();
        var hob = SeatCooktop(top);
        int cutW = hob.CutoutWidthMM, cutD = hob.CutoutDepthMM;

        hob.RotateAroundAxis(Vector3.up, 90f);
        hob.SnapToPart();

        Assert.AreEqual(cutD + 2 * CooktopElement.MIN_EDGE_MM, hob.MinPartWidthMM,
            "повёрнутой панели нужна столешница шириной под ГЛУБИНУ ниши");
        Assert.AreEqual(cutW + 2 * CooktopElement.MIN_EDGE_MM, hob.MinPartDepthMM);
    }

    [Test]
    public void Cooktop_Yaw_SurvivesSaveLoadRoundTrip()
    {
        var top = CreateCountertop();
        var hob = SeatCooktop(top);
        hob.RotateAroundAxis(Vector3.up, 90f);
        hob.SnapToPart();

        var path = Path.Combine(Application.temporaryCachePath, $"rt_yaw_{System.Guid.NewGuid():N}.json");
        var saved = SaveLoadManager.CaptureScene(new List<KitchenElement> { top, hob });
        SaveLoadManager.SaveToFile(path, saved);

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        var loaded = SaveLoadManager.LoadFromFile(path);
        Assert.IsNotNull(loaded);
        SaveLoadManager.RestoreScene(loaded!);
        File.Delete(path);

        var restored = Object.FindFirstObjectByType<CooktopElement>();
        Assert.IsNotNull(restored, "варочная восстановилась");
        Assert.AreEqual(90f, restored!.YawDeg, 0.05f, "разворот пережил round-trip");

        // И поза после врезки повёрнута — не только число в поле.
        restored.SnapToPart();
        Assert.AreEqual(90f, Mathf.Repeat(restored.transform.rotation.eulerAngles.y, 360f), 0.1f,
            "загруженную панель врезка не выкрутила обратно по осям столешницы");
    }

    [Test]
    public void ElementData_CarriesYaw()
    {
        var top = CreateCountertop();
        var hob = SeatCooktop(top);
        hob.RotateAroundAxis(Vector3.up, 90f);
        hob.SnapToPart();

        var data = ElementCapture.FromElement(hob);

        Assert.IsTrue(data.isCooktop);
        Assert.AreEqual(90f, data.cooktopYawDeg, 0.05f);
    }

    // ── Баг 2: только горизонтальный поворот ────────────────────────────

    [Test]
    public void YawOnly_CoversEveryAppliance()
    {
        var hobGo = ElementFactory.CreateCooktop("Hob", Vector3.zero, CooktopElement.MODEL_BOSCH_PUE611BB5E);
        var ovenGo = ElementFactory.CreateOven("Oven", Vector3.zero);
        var dishGo = ElementFactory.CreateDishwasher("Dish", Vector3.zero);
        _spawned.Add(hobGo); _spawned.Add(ovenGo); _spawned.Add(dishGo);

        Assert.IsTrue(FixedSize.IsYawOnly(hobGo.GetComponent<CooktopElement>()));
        Assert.IsTrue(FixedSize.IsYawOnly(ovenGo.GetComponent<OvenElement>()));
        Assert.IsTrue(FixedSize.IsYawOnly(dishGo.GetComponent<DishwasherElement>()));

        var boardGo = new GameObject("Board");
        _spawned.Add(boardGo);
        var board = boardGo.AddComponent<KitchenElement>();
        board.PartName = "Board";
        board.DimensionsMM = new Vector3Int(800, 400, 18);
        Assert.IsFalse(FixedSize.IsYawOnly(board), "обычная деталь крутится как угодно");
        Assert.IsFalse(FixedSize.IsYawOnly(null));
    }

    // ── Окно свойств ────────────────────────────────────────────────────

    private Transform MenuPanel()
    {
        var panel = _canvas!.transform.Find("ContextMenu");
        Assert.NotNull(panel);
        return panel!;
    }

    /// <summary>Имена всех виджетов поворота: подпись, поле, кнопка «на 90°».</summary>
    private static readonly string[] RotXZ =
    {
        "L_X, °", "F_X, °", "L_Z, °", "F_Z, °", "CtxRotX", "CtxRotZ",
    };

    private static readonly string[] RotY = { "L_Y, °", "F_Y, °", "CtxRotY" };

    private static void AssertRotationWidgets(Transform panel, bool xzVisible)
    {
        foreach (var name in RotXZ)
        {
            var t = panel.Find(name);
            Assert.NotNull(t, "виджет " + name + " должен существовать");
            Assert.AreEqual(xzVisible, t!.gameObject.activeSelf, name);
        }
        foreach (var name in RotY)
        {
            var t = panel.Find(name);
            Assert.NotNull(t, "виджет " + name + " должен существовать");
            Assert.IsTrue(t!.gameObject.activeSelf, name + " нужен всем");
        }
    }

    [Test]
    public void ContextMenu_Board_ShowsAllThreeRotationAxes()
    {
        var panel = MenuPanel();
        var go = new GameObject("Board");
        _spawned.Add(go);
        var board = go.AddComponent<KitchenElement>();
        board.PartName = "Board";
        board.DimensionsMM = new Vector3Int(800, 400, 18);

        _menu!.Open(board);

        AssertRotationWidgets(panel, xzVisible: true);
    }

    [Test]
    public void ContextMenu_Appliances_ShowOnlyTheVerticalAxis()
    {
        var panel = MenuPanel();
        var cases = new List<KitchenElement>();
        var hobGo = ElementFactory.CreateCooktop("Hob", Vector3.zero, CooktopElement.MODEL_BOSCH_PUE611BB5E);
        var ovenGo = ElementFactory.CreateOven("Oven", Vector3.zero);
        var dishGo = ElementFactory.CreateDishwasher("Dish", Vector3.zero);
        _spawned.Add(hobGo); _spawned.Add(ovenGo); _spawned.Add(dishGo);
        cases.Add(hobGo.GetComponent<CooktopElement>());
        cases.Add(ovenGo.GetComponent<OvenElement>());
        cases.Add(dishGo.GetComponent<DishwasherElement>());

        foreach (var appliance in cases)
        {
            _menu!.Open(appliance);
            AssertRotationWidgets(panel, xzVisible: false);
        }
    }

    [Test]
    public void ContextMenu_ReturnsTheAxesToAPlainBoard()
    {
        var panel = MenuPanel();
        var hobGo = ElementFactory.CreateOven("Oven", Vector3.zero);
        _spawned.Add(hobGo);
        var go = new GameObject("Board");
        _spawned.Add(go);
        var board = go.AddComponent<KitchenElement>();
        board.PartName = "Board";
        board.DimensionsMM = new Vector3Int(800, 400, 18);

        _menu!.Open(hobGo.GetComponent<OvenElement>());
        _menu!.Open(board);

        // Скрытая строка обязана вернуться: она общая на все типы.
        AssertRotationWidgets(panel, xzVisible: true);
    }

    // ── MCP ─────────────────────────────────────────────────────────────

    private static string ErrorMessage(McpResponse resp) =>
        JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    [Test]
    public void Mcp_EditElements_RejectsRotXAndRotZ()
    {
        var ovenGo = ElementFactory.CreateOven("Oven-mcp", Vector3.zero);
        _spawned.Add(ovenGo);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Oven-mcp", rot_x = 90f } }
        }));

        Assert.AreEqual("error", resp.type, "поворот на бок обязан быть ОТКАЗОМ, а не тишиной");
        StringAssert.Contains("rot_x/rot_z", ErrorMessage(resp));
        Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, ovenGo.transform.rotation), 0.05f,
            "и ничего не применилось");
    }

    [Test]
    public void Mcp_EditElements_AcceptsRotY()
    {
        var ovenGo = ElementFactory.CreateOven("Oven-y", Vector3.zero);
        _spawned.Add(ovenGo);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Oven-y", rot_y = 90f } }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        Assert.AreEqual(90f, Mathf.Repeat(ovenGo.transform.rotation.eulerAngles.y, 360f), 0.05f);
    }

    [Test]
    public void Mcp_SetRotation_RejectsTheHorizontalAxes()
    {
        var dishGo = ElementFactory.CreateDishwasher("Dish-mcp", Vector3.zero);
        _spawned.Add(dishGo);

        var resp = _handler!.Handle(MakeReq("set_rotation", new
        {
            ops = new object[] { new { object_path = "Dish-mcp", z_deg = 45f } }
        }));

        Assert.AreEqual("error", resp.type);
        StringAssert.Contains("built-in appliance", ErrorMessage(resp));
        Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, dishGo.transform.rotation), 0.05f);
    }
}
