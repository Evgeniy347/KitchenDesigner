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
    private Canvas? _canvas;
    private ContextMenuUI? _menu;

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

        if (_menu != null) Object.DestroyImmediate(_menu.gameObject);
        _menu = null;
        if (_canvas != null) Object.DestroyImmediate(_canvas.gameObject);
        _canvas = null;

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

    /// <summary>Габарит (центр, размер) в мм по вершинам ВАЛИДАЦИИ — то, чем
    /// духовка участвует в коллизиях и прилипании.</summary>
    private static (Vector3 center, Vector3 size) ValidationBoxMM(OvenElement oven)
    {
        float toU = AppConstants.MM_TO_UNITS;
        var v = oven.GetVertices();
        var min = v[0];
        var max = v[0];
        foreach (var p in v) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        return ((min + max) * 0.5f / toU, (max - min) / toU);
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
    public void Oven_IsNineBoxes()
    {
        var oven = Make();

        var names = new List<string>();
        foreach (Transform child in oven.transform)
            if (child.gameObject.activeSelf) names.Add(child.name);

        CollectionAssert.AreEquivalent(
            new[]
            {
                "BodyBottom", "BodyTop", "BodyLeft", "BodyRight", "BodyBack",
                "Facade", "Glass", "ControlPanel", "Handle",
            }, names,
            "полый короб из пяти стенок плюс четыре коробки дверцы");
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

    /// <summary>Корпус — 560 × 570 × 548 (DNS: встраивание 57 × 54.8 см, ниша
    /// 560⁺⁸). Пять стенок обязаны сложиться ровно в этот габарит и оставить
    /// внутри пустоту.</summary>
    [Test]
    public void Body_IsAHollowBoxUnderTheTopOverhang()
    {
        var oven = Make();
        var facade = BoxMM(oven, "Facade");

        var walls = new[] { "BodyBottom", "BodyTop", "BodyLeft", "BodyRight", "BodyBack" };
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var wall in walls)
        {
            var (c, s) = BoxMM(oven, wall);
            min = Vector3.Min(min, c - s * 0.5f);
            max = Vector3.Max(max, c + s * 0.5f);
        }

        Assert.AreEqual(560f, max.x - min.x, 0.01f, "ширина корпуса");
        Assert.AreEqual(570f, max.y - min.y, 0.01f, "высота корпуса");
        Assert.AreEqual(548f, max.z - min.z, 0.01f, "глубина корпуса");

        float facadeTop = facade.center.y + facade.size.y * 0.5f;
        Assert.AreEqual(25f, facadeTop - max.y, 0.01f,
            "фасад выступает над корпусом сверху на 25");
        Assert.AreEqual(facade.center.y - facade.size.y * 0.5f, min.y, 0.01f,
            "снизу фасад заподлицо с корпусом");
        // Корпус стоит вплотную за фасадом и достаёт до задней грани габарита.
        Assert.AreEqual(facade.center.z - facade.size.z * 0.5f, max.z, 0.01f);
        Assert.AreEqual(-567.5f * 0.5f, min.z, 0.01f);

        // Внутри пусто: ни одна стенка не заходит в камеру.
        float t = OvenElement.BODY_WALL_MM;
        foreach (var wall in walls)
        {
            var (c, s) = BoxMM(oven, wall);
            bool insideX = c.x - s.x * 0.5f > min.x + t - 0.01f && c.x + s.x * 0.5f < max.x - t + 0.01f;
            bool insideY = c.y - s.y * 0.5f > min.y + t - 0.01f && c.y + s.y * 0.5f < max.y - t + 0.01f;
            bool insideZ = c.z - s.z * 0.5f > min.z + t - 0.01f && c.z + s.z * 0.5f < max.z - t + 0.01f;
            Assert.IsFalse(insideX && insideY && insideZ, wall + " стоит в камере — короб не полый");
        }
    }

    /// <summary>595 = 25 + 570: нижнего выступа у фасада НЕТ. Тест сторожит
    /// сходимость — уточнят любую из трёх величин, и она обязана сойтись.</summary>
    [Test]
    public void FacadeOverhangs_AddUpToTheFacadeHeight()
    {
        Assert.AreEqual(OvenElement.FACADE_HEIGHT_MM,
            OvenElement.FACADE_TOP_OVERHANG_MM + OvenElement.BODY_HEIGHT_MM
            + OvenElement.FACADE_BOTTOM_OVERHANG_MM,
            "верхний выступ + корпус + нижний выступ = высота фасада");
        Assert.AreEqual(0, OvenElement.FACADE_BOTTOM_OVERHANG_MM,
            "595 − 25 − 570 = 0: снизу фасад заподлицо с корпусом");
        Assert.AreEqual(560, OvenElement.BODY_WIDTH_MM, "ниша 560⁺⁸, корпус в неё входит");
        Assert.AreEqual(570, OvenElement.BODY_HEIGHT_MM, "высота встраивания 57 см");
        Assert.AreEqual(548, OvenElement.BODY_DEPTH_MM, "глубина встраивания 54.8 см");
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

    // ── Объём валидации: корпус, а не фасад ─────────────────────────────

    [Test]
    public void ValidationVolume_IsTheBodyOnly()
    {
        var oven = Make();
        var (center, size) = ValidationBoxMM(oven);

        Assert.AreEqual(560f, size.x, 0.01f, "в коллизии идёт корпус, а не фасад 594");
        Assert.AreEqual(570f, size.y, 0.01f);
        Assert.AreEqual(548f, size.z, 0.01f);
        // Корпус утоплен за фасад и опущен на верхний выступ — поза валидации
        // обязана уехать вместе с ним.
        Assert.AreEqual(0f, center.x, 0.01f);
        Assert.AreEqual(-12.5f, center.y, 0.01f, "570/2 + 25 ниже центра габарита");
        Assert.AreEqual(-9.75f, center.z, 0.01f, "за фасадом 19.5");
    }

    /// <summary>Примерка в другую позицию обязана давать то же самое, что
    /// настоящий переезд, — иначе снэп и валидация разойдутся.</summary>
    [Test]
    public void ValidationVolume_FollowsAHypotheticalPosition()
    {
        var oven = Make();
        var probe = new Vector3(1.5f, 0.4f, -2f);

        var tried = oven.GetVerticesAt(probe);
        oven.transform.position = probe;
        var actual = oven.GetVertices();

        for (int i = 0; i < 8; i++)
            Assert.AreEqual(0f, (tried[i] - actual[i]).magnitude, 1e-5f, "вершина " + i);
    }

    /// <summary>Коллайдер выбора остаётся по ПОЛНОЙ коробке: кликают по фасаду,
    /// и он обязан попадать в духовку, хотя в объём валидации не входит.</summary>
    [Test]
    public void Collider_CoversTheFacade_WhileValidationDoesNot()
    {
        var oven = Make();
        float toU = AppConstants.MM_TO_UNITS;
        var box = oven.GetComponent<BoxCollider>();

        Assert.AreEqual(594f, box!.size.x / toU, 0.01f, "клик по фасаду обязан выделять духовку");
        Assert.AreEqual(560f, ValidationBoxMM(oven).size.x, 0.01f);
    }

    /// <summary>Деталь-доска в мировых координатах (мм).</summary>
    private KitchenElement Board(string name, Vector3 centerMM, Vector3Int dimsMM)
    {
        float toU = AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreatePart(dimsMM, name, centerMM * toU);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private static List<KitchenElement> OverlapPartners(KitchenElement el)
    {
        var result = new List<KitchenElement>();
        var r = ConstraintValidator.Validate(PartRegistry.GetAll());
        if (r.diagnostics == null) return result;
        foreach (var v in r.diagnostics)
        {
            if (v.kind != ViolationKind.Overlap) continue;
            if (v.element == el && v.other != null) result.Add(v.other);
            else if (v.other == el) result.Add(v.element);
        }
        return result;
    }

    /// <summary>Штатный модуль 600: проём 564, боковины 18, полка под духовкой.
    /// Фасад 594 ЛЕЖИТ ПОВЕРХ боковин — так прибор и монтируют, и это не может
    /// быть ошибкой. До правки такая установка давала три COL-01 разом.</summary>
    [Test]
    public void StandardModule600_ProducesNoOverlap()
    {
        var oven = Make("Duhovka");
        // Боковины: внутренние грани на ±282 → проём 564.
        Board("side_L", new Vector3(-291f, 0f, -9.75f), new Vector3Int(18, 700, 560));
        Board("side_R", new Vector3(291f, 0f, -9.75f), new Vector3Int(18, 700, 560));
        // Полка вплотную под корпусом (низ корпуса на −297.5).
        Board("shelf", new Vector3(0f, -306.5f, -9.75f), new Vector3Int(564, 18, 560));

        var partners = OverlapPartners(oven);

        CollectionAssert.IsEmpty(partners,
            "духовка в своём модуле пересекается с: "
            + string.Join(", ", partners.ConvertAll(p => p.PartName)));
    }

    /// <summary>А вот НАСТОЯЩЕЕ пересечение корпуса ловиться обязано — иначе
    /// «нет коллизий» означало бы «проверка не работает».</summary>
    [Test]
    public void BoardInsideTheBody_IsStillAnOverlap()
    {
        var oven = Make("Duhovka");
        Board("intruder", new Vector3(0f, -12.5f, -9.75f), new Vector3Int(300, 300, 300));

        var partners = OverlapPartners(oven);

        Assert.AreEqual(1, partners.Count, "деталь в камере духовки — это COL-01");
        Assert.AreEqual("intruder", partners[0].PartName);
    }

    // ── Дверца ──────────────────────────────────────────────────────────

    [Test]
    public void Door_IsClosedByDefault()
    {
        var oven = Make();

        Assert.IsFalse(oven.IsOpen);
        Assert.AreEqual(0f, oven.DoorProgress, 1e-4f);
        Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity,
            Child(oven, "Facade").localRotation), 0.01f);
    }

    /// <summary>Откидная дверца: поворот вокруг НИЖНЕЙ кромки фасада на 90°.
    /// Верх фасада уезжает вперёд, низ остаётся на месте.</summary>
    [Test]
    public void Door_DropsDownAroundItsBottomEdge()
    {
        var oven = Make();
        float toU = AppConstants.MM_TO_UNITS;

        // Ось петли — нижняя кромка на ЗАДНЕЙ плоскости фасада (там дверца
        // прилегает к корпусу), т.е. середина нижнего заднего ребра рамки.
        Vector3 HingeEdge(Transform f) => f.localPosition + f.localRotation * new Vector3(
            0f,
            -OvenElement.FACADE_HEIGHT_MM * 0.5f * toU,
            -OvenElement.FACADE_THICKNESS_MM * 0.5f * toU);
        Vector3 TopEdge(Transform f) => f.localPosition + f.localRotation * new Vector3(
            0f,
            OvenElement.FACADE_HEIGHT_MM * 0.5f * toU,
            -OvenElement.FACADE_THICKNESS_MM * 0.5f * toU);

        var closedHinge = HingeEdge(Child(oven, "Facade"));
        Assert.AreEqual(0f, (closedHinge - OvenElement.HingeLocalMM * toU).magnitude, 1e-5f,
            "закрытая дверца стоит на своей же оси петли");

        oven.SetOpen(true);
        oven.StepDoor(10f);

        Assert.IsTrue(oven.IsOpen);
        Assert.AreEqual(1f, oven.DoorProgress, 1e-4f);

        var facade = Child(oven, "Facade");
        Assert.AreEqual(90f, Quaternion.Angle(Quaternion.identity, facade.localRotation), 0.01f,
            "дверца раскрыта ровно в горизонталь");

        // Ось поворота стоит на месте — это и значит «откидывается вокруг неё».
        Assert.AreEqual(0f, (HingeEdge(facade) - closedHinge).magnitude, 1e-5f,
            "нижняя кромка не сдвинулась");

        // Верх фасада ушёл ВПЕРЁД на всю высоту дверцы и опустился к петле.
        var openTop = TopEdge(facade);
        Assert.AreEqual(closedHinge.z / toU + OvenElement.FACADE_HEIGHT_MM, openTop.z / toU, 0.01f,
            "верх дверцы вынесло вперёд на её высоту");
        Assert.AreEqual(closedHinge.y, openTop.y, 1e-5f, "и опустился на уровень петли");
    }

    [Test]
    public void Door_ClosesBack()
    {
        var oven = Make();
        oven.SetOpen(true);
        oven.StepDoor(10f);

        oven.SetOpen(false);
        oven.StepDoor(10f);

        Assert.IsFalse(oven.IsOpen);
        Assert.AreEqual(0f, oven.DoorProgress, 1e-4f);
        Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity,
            Child(oven, "Facade").localRotation), 0.01f);
    }

    [Test]
    public void ForceClose_SlamsTheDoorInstantly()
    {
        var oven = Make();
        oven.SetOpen(true);
        oven.StepDoor(10f);

        oven.ForceClose();

        Assert.IsFalse(oven.IsOpen);
        Assert.AreEqual(0f, oven.DoorProgress, 1e-4f);
    }

    /// <summary>Открывание — транзитная анимация: корень не двигается вовсе,
    /// поэтому откинутая дверца не может породить COL-01 (её в объёме
    /// валидации нет и в закрытом виде).</summary>
    [Test]
    public void OpenDoor_ProducesNoOverlap()
    {
        var oven = Make("Duhovka");
        Board("side_L", new Vector3(-291f, 0f, -9.75f), new Vector3Int(18, 700, 560));
        Board("side_R", new Vector3(291f, 0f, -9.75f), new Vector3Int(18, 700, 560));
        Board("shelf", new Vector3(0f, -306.5f, -9.75f), new Vector3Int(564, 18, 560));
        var before = oven.GetVertices();

        oven.SetOpen(true);
        oven.StepDoor(10f);

        var partners = OverlapPartners(oven);
        CollectionAssert.IsEmpty(partners,
            "откинутая дверца пересекается с: "
            + string.Join(", ", partners.ConvertAll(p => p.PartName)));

        var after = oven.GetVertices();
        for (int i = 0; i < 8; i++)
            Assert.AreEqual(0f, (before[i] - after[i]).magnitude, 1e-6f,
                "поза валидации при открывании не двигается");
    }

    // ── Цвет ────────────────────────────────────────────────────────────

    /// <summary>Дверца чёрная, корпус серый: чёрный полый короб внутри сливался
    /// бы сам с собой.</summary>
    [Test]
    public void Body_IsGrey_WhileTheDoorStaysBlack()
    {
        var oven = Make();

        var body = Child(oven, "BodyLeft").GetComponent<MeshRenderer>().sharedMaterial;
        var facade = Child(oven, "Facade").GetComponent<MeshRenderer>().sharedMaterial;

        Assert.AreNotSame(facade, body, "корпус и фасад больше не красятся одним материалом");
        foreach (var wall in new[] { "BodyBottom", "BodyTop", "BodyLeft", "BodyRight", "BodyBack" })
            Assert.AreSame(body, Child(oven, wall).GetComponent<MeshRenderer>().sharedMaterial,
                wall + " красится тем же серым, что и остальной короб");

        Assert.Less(facade.GetColor("_BaseColor").grayscale, 0.1f, "дверца чёрная");
        Assert.Greater(body.GetColor("_BaseColor").grayscale, 0.3f, "корпус серый");
    }

    // ── Каталог ─────────────────────────────────────────────────────────

    [Test]
    public void Catalog_ApplianceGroup_HasOvenThird()
    {
        var group = SidebarCatalog.Build().Find(g => g.title == "Техника");

        Assert.AreEqual(4, group.items.Count, "общая варочная, модельная варочная, духовка, посудомойка");
        var oven = group.items[2];
        Assert.IsTrue(oven.isOven, "духовка — третий пункт «Техники»");
        Assert.AreEqual(OvenElement.MODEL, oven.applianceModel);
        Assert.AreEqual(new Vector3Int(594, 595, 568), oven.dims, "в каталоге размеры производителя");
        Assert.IsFalse(oven.isCooktop, "духовка не варочная — иначе SidebarUI.Spawn ушёл бы не туда");
    }

    // ── Сериализация ────────────────────────────────────────────────────

    [Test]
    public void ElementData_MarksTheOven()
    {
        var oven = Make();

        var data = ElementCapture.FromElement(oven);

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
        Assert.AreEqual(9, restored.transform.childCount, "короб и фасад собраны заново целиком");
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
            items = new object[] { new { name = "Oven-mcp", type = "oven", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f, width = 900 } }
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
            items = new object[] { new { name = "Oven-fix", type = "oven", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
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
                new { name = "Hob-wrong", type = "cooktop", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f, model = OvenElement.MODEL }
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
            items = new object[] { new { name = "Oven-info", type = "oven", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
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

    [Test]
    public void Mcp_OpensAndClosesTheOvenDoor()
    {
        var oven = Make("Oven-door");

        var open = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Oven-door", is_open = true } }
        }));
        Assert.AreEqual("result", open.type, open.type == "error" ? ErrorMessage(open) : "");
        Assert.IsTrue(oven.IsOpen);

        var info = _handler.Handle(MakeReq("get_elements", new { filter = "Oven-door" }));
        Assert.IsTrue(Payload(info)["elements"]![0]!["oven"]!["isOpen"]!.Value<bool>());

        _handler.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Oven-door", is_open = false } }
        }));
        Assert.IsFalse(oven.IsOpen);
    }

    [Test]
    public void OpenDoor_SurvivesSaveLoadRoundTrip()
    {
        var oven = Make("Oven-open");
        oven.SetOpen(true);
        oven.StepDoor(10f);

        var path = Path.Combine(Application.temporaryCachePath, $"rt_ovendoor_{System.Guid.NewGuid():N}.json");
        SaveLoadManager.SaveToFile(path,
            SaveLoadManager.CaptureScene(new List<KitchenElement> { oven }));

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        var loaded = SaveLoadManager.LoadFromFile(path);
        Assert.IsNotNull(loaded);
        SaveLoadManager.RestoreScene(loaded!);
        File.Delete(path);

        var restored = Object.FindFirstObjectByType<OvenElement>();
        Assert.IsNotNull(restored);
        Assert.IsTrue(restored!.IsOpen, "откинутая дверца обязана пережить сохранение");
    }

    // ── Окно свойств ────────────────────────────────────────────────────

    private Transform BuildMenu()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
        var panel = _canvas!.transform.Find("ContextMenu");
        Assert.IsNotNull(panel);
        return panel!;
    }

    /// <summary>Кнопка «Открыть дверцу» — по образцу «Открыть ящик»: видна
    /// только у духовки и переключает подпись по состоянию.</summary>
    [Test]
    public void ContextMenu_ShowsTheDoorButtonForTheOvenOnly()
    {
        var panel = BuildMenu();
        var oven = Make("Oven-ui");
        var boardGo = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Board", Vector3.zero);
        _spawned.Add(boardGo);

        _menu!.Open(oven);
        var button = panel.Find("CtxOvenDoor");
        Assert.IsNotNull(button, "у духовки должна быть кнопка открывания");
        Assert.IsTrue(button!.gameObject.activeSelf);
        Assert.AreEqual("Открыть дверцу",
            button.GetComponentInChildren<TMPro.TMP_Text>().text);

        oven.SetOpen(true);
        _menu!.Open(oven);
        Assert.AreEqual("Закрыть дверцу",
            button.GetComponentInChildren<TMPro.TMP_Text>().text);

        _menu!.Open(boardGo.GetComponent<KitchenElement>());
        Assert.IsFalse(button.gameObject.activeSelf, "обычной детали кнопка не нужна");
    }
}
