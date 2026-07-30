using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Bulk;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.UI;

/// <summary>
/// Электрический духовой шкаф Bosch HBA514BB3 — вторая готовая модель группы
/// «Техника» (docs/APPLIANCES-BRIEF.md §2).
///
/// Проверяется то, что отличает духовку от обычной детали и от врезной техники:
/// размеры не поддаются правке НИ ОДНИМ путём, фасад собран целиком (рамка,
/// стекло, панель управления, ручка) и СХОДИТСЯ САМ С СОБОЙ — ни одна деталь
/// не свисает с фасада и не тонет в соседней, — а сама духовка ни к какой
/// детали не привязывается и переживает сохранение и дублирование.
/// </summary>
public class OvenElementTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private McpCommandHandler? _handler;

    [SetUp]
    public void SetUp()
    {
        _handler = new McpCommandHandler();
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

    [TearDown]
    public void TearDown()
    {
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
    }

    private OvenElement Make(string name = "Oven")
    {
        var go = ElementFactory.CreateOven(name, Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<OvenElement>();
    }

    private static Transform Child(OvenElement oven, string name)
    {
        var child = oven.transform.Find(name);
        Assert.IsNotNull(child, "нет дочерней коробки «" + name + "»");
        return child!;
    }

    /// <summary>Габарит дочерней коробки в ЛОКАЛЬНЫХ мм: (центр, размер).</summary>
    private static (Vector3 center, Vector3 size) BoxMM(OvenElement oven, string name)
    {
        float toU = AppConstants.MM_TO_UNITS;
        var t = Child(oven, name);
        return (t.localPosition / toU, t.localScale / toU);
    }

    // ── Габариты производителя ──────────────────────────────────────────

    [Test]
    public void Oven_HasManufacturerDimensions()
    {
        var oven = Make();

        // 594×595 фасад, 548 корпус + 19.5 фасад = 567.5 → 568 в целых мм.
        Assert.AreEqual(new Vector3Int(594, 595, 568), oven.DimensionsMM);
        Assert.AreEqual(568, OvenElement.DEPTH_MM);
        Assert.AreEqual(567.5f, OvenElement.TOTAL_DEPTH_MM, 1e-4f, "корпус 548 + фасад 19.5");
    }

    [Test]
    public void Oven_IsFixedSize()
    {
        var oven = Make();

        Assert.IsTrue(oven.HasFixedSize);
        Assert.IsTrue(FixedSize.IsFixed(oven), "общий признак техники видит духовку");
    }

    [Test]
    public void Oven_ModelIsRegisteredInTheApplianceCatalog()
    {
        Assert.IsTrue(ApplianceModels.IsKnown(OvenElement.MODEL));
        Assert.IsFalse(ApplianceModels.IsKnown("Bosch NOSUCHMODEL"));
        Assert.IsFalse(ApplianceModels.IsKnown(""), "пустая строка — это «без модели», а не модель");
    }

    // ── Правка запрещена любым путём ────────────────────────────────────

    [Test]
    public void Oven_ResizeIsIgnored()
    {
        var oven = Make();

        oven.DimensionsMM = new Vector3Int(800, 900, 700);

        Assert.AreEqual(new Vector3Int(594, 595, 568), oven.DimensionsMM,
            "габарит производителя возвращается на место в ApplyDimensions");
    }

    [Test]
    public void Oven_HasNoResizeHandles()
    {
        var oven = Make();

        Assert.IsFalse(ResizeHandleManager.SupportsHandleResize(oven), "мышь не обходит окно свойств");
    }

    [Test]
    public void RootScaleStaysUnit()
    {
        var oven = Make();

        Assert.AreEqual(Vector3.one, oven.transform.localScale,
            "корень единичный — иначе дети масштабируются дважды");
    }

    [Test]
    public void Oven_ColliderCoversTheWholeBox()
    {
        var oven = Make();
        float toU = AppConstants.MM_TO_UNITS;

        var box = oven.GetComponent<BoxCollider>();
        Assert.IsNotNull(box, "клик по духовке ловит коллайдер корня");
        Assert.AreEqual(594f, box!.size.x / toU, 0.01f);
        Assert.AreEqual(595f, box.size.y / toU, 0.01f);
        Assert.AreEqual(568f, box.size.z / toU, 0.01f);
        Assert.AreEqual(Vector3.zero, box.center);
    }

    // ── Собранный фасад ─────────────────────────────────────────────────

    [Test]
    public void Oven_IsFiveBoxes()
    {
        var oven = Make();

        var names = new List<string>();
        foreach (Transform child in oven.transform)
            if (child.gameObject.activeSelf) names.Add(child.name);

        CollectionAssert.AreEquivalent(
            new[] { "Body", "Facade", "Glass", "ControlPanel", "Handle" }, names,
            "корпус, рамка фасада, стекло, панель управления и ручка");
    }

    [Test]
    public void Facade_IsFullSizeAndInFront()
    {
        var oven = Make();
        var (center, size) = BoxMM(oven, "Facade");

        Assert.AreEqual(594f, size.x, 0.01f);
        Assert.AreEqual(595f, size.y, 0.01f);
        Assert.AreEqual(19.5f, size.z, 0.01f, "толщина фасадной рамки");
        // Передняя грань фасада — передняя грань всего прибора (567.5 / 2).
        Assert.AreEqual(567.5f * 0.5f, center.z + size.z * 0.5f, 0.01f);
    }

    [Test]
    public void Body_HangsUnderTheTopOverhangAndFillsTheDepth()
    {
        var oven = Make();
        var (center, size) = BoxMM(oven, "Body");
        var facade = BoxMM(oven, "Facade");

        Assert.AreEqual(570f, size.x, 0.01f);
        Assert.AreEqual(535f, size.y, 0.01f);
        Assert.AreEqual(548f, size.z, 0.01f);

        float facadeTop = facade.center.y + facade.size.y * 0.5f;
        Assert.AreEqual(25f, facadeTop - (center.y + size.y * 0.5f), 0.01f,
            "фасад выступает над корпусом сверху на 25");
        // Корпус стоит вплотную за фасадом и достаёт до задней грани габарита.
        Assert.AreEqual(facade.center.z - facade.size.z * 0.5f, center.z + size.z * 0.5f, 0.01f);
        Assert.AreEqual(-567.5f * 0.5f, center.z - size.z * 0.5f, 0.01f);
    }

    /// <summary>Бриф уверяет, что 595 = 25 + 535 + 7.5; арифметика этого не
    /// подтверждает, поэтому нижний выступ вычисляется и обязан закрывать фасад
    /// без щели. Тест сторожит именно СХОДИМОСТЬ, а не конкретное число.</summary>
    [Test]
    public void FacadeOverhangs_AddUpToTheFacadeHeight()
    {
        Assert.AreEqual(OvenElement.FACADE_HEIGHT_MM,
            OvenElement.FACADE_TOP_OVERHANG_MM + OvenElement.BODY_HEIGHT_MM
            + OvenElement.FACADE_BOTTOM_OVERHANG_MM,
            "верхний выступ + корпус + нижний выступ = высота фасада");
        Assert.AreEqual(35, OvenElement.FACADE_BOTTOM_OVERHANG_MM,
            "595 − 25 − 535 = 35, а не заявленные в брифе 7.5");
    }

    [Test]
    public void ControlPanel_IsTheTopStripOfTheFacade()
    {
        var oven = Make();
        var (center, size) = BoxMM(oven, "ControlPanel");
        var facade = BoxMM(oven, "Facade");

        Assert.AreEqual(594f, size.x, 0.01f, "панель на всю ширину фасада");
        Assert.AreEqual(96f, size.y, 0.01f);
        float facadeTop = facade.center.y + facade.size.y * 0.5f;
        Assert.AreEqual(facadeTop, center.y + size.y * 0.5f, 0.01f, "прижата к верху фасада");
    }

    [Test]
    public void Glass_FillsTheDoorBelowTheControlPanel()
    {
        var oven = Make();
        var (center, size) = BoxMM(oven, "Glass");
        var panel = BoxMM(oven, "ControlPanel");

        Assert.AreEqual(499, OvenElement.GLASS_HEIGHT_MM, "595 − 96");
        // Стекло — вставка в рамке: уже проёма двери на две ширины рамки.
        Assert.AreEqual(594f - 2 * OvenElement.DOOR_FRAME_MM, size.x, 0.01f);
        Assert.AreEqual(499f - 2 * OvenElement.DOOR_FRAME_MM, size.y, 0.01f);

        float panelBottom = panel.center.y - panel.size.y * 0.5f;
        Assert.Less(center.y + size.y * 0.5f, panelBottom + 0.01f, "стекло не залезает на панель");
        Assert.Greater(center.y - size.y * 0.5f, -595f * 0.5f, "и не свисает с низа фасада");
    }

    [Test]
    public void Handle_SitsAtTheBottomEdgeOfTheControlPanel()
    {
        var oven = Make();
        var (center, size) = BoxMM(oven, "Handle");
        var panel = BoxMM(oven, "ControlPanel");

        float panelBottom = panel.center.y - panel.size.y * 0.5f;
        Assert.AreEqual(panelBottom, center.y + size.y * 0.5f, 0.01f,
            "верх ручки — по низу панели управления");
        Assert.AreEqual(594f - 2 * OvenElement.HANDLE_SIDE_INSET_MM, size.x, 0.01f);
    }

    /// <summary>Ручка — единственная деталь, которой РАЗРЕШЕНО выходить за
    /// габаритную коробку: коробка описывает то, что встаёт в нишу колонны.</summary>
    [Test]
    public void Handle_IsTheOnlyPartInFrontOfTheBox()
    {
        var oven = Make();
        float halfD = 567.5f * 0.5f;

        foreach (Transform child in oven.transform)
        {
            var (center, size) = BoxMM(oven, child.name);
            float front = center.z + size.z * 0.5f;
            if (child.name == "Handle")
            {
                Assert.AreEqual(halfD + OvenElement.HANDLE_PROTRUSION_MM, front, 0.01f,
                    "ручка выступает вперёд ровно на свой максимум");
                continue;
            }
            Assert.LessOrEqual(front, halfD + 0.01f, child.name + " выходит за габарит вперёд");
        }
    }

    [Test]
    public void EveryPart_StaysInsideTheFacadeOutline()
    {
        var oven = Make();
        float halfW = 594f * 0.5f, halfH = 595f * 0.5f;

        foreach (Transform child in oven.transform)
        {
            var (center, size) = BoxMM(oven, child.name);
            Assert.LessOrEqual(Mathf.Abs(center.x) + size.x * 0.5f, halfW + 0.01f,
                child.name + " свисает по ширине");
            Assert.LessOrEqual(Mathf.Abs(center.y) + size.y * 0.5f, halfH + 0.01f,
                child.name + " свисает по высоте");
        }
    }

    // ── Каталог ─────────────────────────────────────────────────────────

    [Test]
    public void Catalog_ApplianceGroup_HasOvenSecond()
    {
        var group = SidebarCatalog.Build().Find(g => g.title == "Техника");

        Assert.AreEqual(3, group.items.Count, "варочная, духовка, посудомойка");
        var oven = group.items[1];
        Assert.IsTrue(oven.isOven, "духовка — второй пункт «Техники»");
        Assert.AreEqual(OvenElement.MODEL, oven.applianceModel);
        Assert.AreEqual(new Vector3Int(594, 595, 568), oven.dims, "в каталоге размеры производителя");
        Assert.IsFalse(oven.isCooktop, "духовка не варочная — иначе SidebarUI.Spawn ушёл бы не туда");
    }

    // ── Сериализация ────────────────────────────────────────────────────

    [Test]
    public void ElementData_MarksTheOven()
    {
        var oven = Make();

        var data = ElementData.FromElement(oven);

        Assert.IsTrue(data.isOven);
        Assert.IsFalse(data.isCooktop);
        Assert.IsFalse(data.isSink);
    }

    [Test]
    public void Oven_SurvivesSaveLoadRoundTrip()
    {
        var oven = Make("Oven1");
        oven.transform.position = new Vector3(1f, 0.3f, 2f);

        var path = Path.Combine(Application.temporaryCachePath, $"rt_oven_{System.Guid.NewGuid():N}.json");
        var saved = SaveLoadManager.CaptureScene(new List<KitchenElement> { oven });
        SaveLoadManager.SaveToFile(path, saved);

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        var loaded = SaveLoadManager.LoadFromFile(path);
        Assert.IsNotNull(loaded);
        SaveLoadManager.RestoreScene(loaded!);
        File.Delete(path);

        var restored = Object.FindFirstObjectByType<OvenElement>();
        Assert.IsNotNull(restored, "духовка восстановилась своим типом, а не деталью");
        Assert.AreEqual(new Vector3Int(594, 595, 568), restored!.DimensionsMM);
        Assert.IsTrue(restored.HasFixedSize);
        Assert.AreEqual(5, restored.transform.childCount, "фасад собран заново целиком");
    }

    [Test]
    public void Duplicate_StaysAnOven()
    {
        var oven = Make("Oven2");

        var copyGo = ElementFactory.Duplicate(oven);
        _spawned.Add(copyGo);

        var copy = copyGo.GetComponent<OvenElement>();
        Assert.IsNotNull(copy, "копия духовки — духовка, а не голая деталь");
        Assert.AreEqual(new Vector3Int(594, 595, 568), copy!.DimensionsMM);
    }

    // ── MCP ─────────────────────────────────────────────────────────────

    private McpRequest MakeReq(string method, object data)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        return new McpRequest { id = "test", method = method, Params = JObject.Parse(json) };
    }

    /// <summary>Полезная нагрузка ответа: у результата — данные, у ошибки —
    /// {code, message}. Оба живут в одном поле data.</summary>
    private static JObject Payload(McpResponse resp) => JObject.FromObject(resp.data!);

    private static string ErrorMessage(McpResponse resp) =>
        Payload(resp)["message"]!.Value<string>()!;

    [Test]
    public void Mcp_CreatesOvenWithManufacturerSize()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[] { new { name = "Oven-mcp", type = "oven", x = 0f, y = 0f, z = 0f, width = 900 } }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        var oven = Object.FindFirstObjectByType<OvenElement>();
        Assert.IsNotNull(oven);
        Assert.AreEqual(new Vector3Int(594, 595, 568), oven!.DimensionsMM,
            "width из запроса на готовую модель не влияет");
    }

    [Test]
    public void Mcp_RejectsResizingTheOven()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[] { new { name = "Oven-fix", type = "oven", x = 0f, y = 0f, z = 0f } }
        }));

        var resp = _handler.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Oven-fix", width = 700 } }
        }));

        Assert.AreEqual("error", resp.type, "правка размера готовой модели обязана быть ОТКАЗОМ, а не тишиной");
        StringAssert.Contains("fixed appliance", ErrorMessage(resp));
    }

    [Test]
    public void Mcp_RejectsAModelFromAnotherAppliance()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[]
            {
                new { name = "Hob-wrong", type = "cooktop", x = 0f, y = 0f, z = 0f, model = OvenElement.MODEL }
            }
        }));

        Assert.AreEqual("error", resp.type, "модель духовки не делает варочную духовкой");
        StringAssert.Contains("does not belong to type", ErrorMessage(resp));
    }

    [Test]
    public void Mcp_ReportsTheOvenBreakdown()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[] { new { name = "Oven-info", type = "oven", x = 0f, y = 0f, z = 0f } }
        }));

        var resp = _handler.Handle(MakeReq("get_elements", new { filter = "Oven-info" }));

        Assert.AreEqual("result", resp.type);
        var info = Payload(resp)["elements"]![0]!["oven"]!;
        Assert.AreEqual(OvenElement.MODEL, info["model"]!.Value<string>());
        Assert.IsTrue(info["fixedSize"]!.Value<bool>());
        Assert.AreEqual(96, info["controlPanelHeightMM"]!.Value<int>());
        Assert.AreEqual(499, info["glassHeightMM"]!.Value<int>());
    }

    [Test]
    public void Selector_NamesTheOvenType()
    {
        var oven = Make("Oven-sel");

        Assert.AreEqual("oven", ElementSelector.TypeOf(oven));
    }
}
