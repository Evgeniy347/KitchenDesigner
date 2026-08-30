using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Bulk;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.UI;

/// <summary>
/// Встраиваемая посудомоечная машина Bosch SMV25EX02E — третья готовая модель
/// группы «Техника» (docs/APPLIANCES-BRIEF.md §3).
///
/// От духовки её отличает ГЛАВНОЕ: своей фасадной панели у машины нет, лицо ей
/// делает обычный мебельный фасад, ПРИСТЁГНУТЫЙ по имени. Поэтому здесь, кроме
/// обычного для техники «размер не поддаётся правке ни одним путём», проверяется
/// вся жизнь этой ссылки: сохранение, переименование фасада, его удаление,
/// дублирование машины — и диапазон высоты фасада, который обязан ПРЕДУПРЕЖДАТЬ,
/// а не запрещать.
/// </summary>
public class DishwasherElementTests
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

    private DishwasherElement Make(string name = "Dishwasher")
    {
        var go = ElementFactory.CreateDishwasher(name, Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<DishwasherElement>();
    }

    /// <summary>Фасад машины: 600 × высота × 18, прижат лицом к переду прибора
    /// (перёд — +Z), низ фасада — на высоте цоколя. Именно так его ставит
    /// пользователь, и только так проверка контакта имеет смысл.</summary>
    private FacadeElement MakeFacadeFor(DishwasherElement dw, string name,
        int heightMM = DishwasherElement.FACADE_NOMINAL_HEIGHT_MM, int thicknessMM = 18)
    {
        float toU = AppConstants.MM_TO_UNITS;
        var dims = new Vector3Int(DishwasherElement.FACADE_WIDTH_MM, heightMM, thicknessMM);
        var dwPos = dw.transform.position;
        // Перед машины + полтолщины фасада: грани сходятся вплотную.
        float z = dwPos.z + (DishwasherElement.BODY_DEPTH_MM * 0.5f + thicknessMM * 0.5f) * toU;
        // Верх фасада — по верху корпуса; остаток снизу и есть цоколь.
        float top = dwPos.y + DishwasherElement.BODY_HEIGHT_MM * 0.5f * toU;
        float y = top - heightMM * 0.5f * toU;

        var go = ElementFactory.CreateFacade(dims, name, new Vector3(dwPos.x, y, z), 0, 0, 0, 0);
        _spawned.Add(go);
        return go.GetComponent<FacadeElement>();
    }

    private static (Vector3 center, Vector3 size) BoxMM(DishwasherElement dw, string name)
    {
        float toU = AppConstants.MM_TO_UNITS;
        var t = dw.transform.Find(name);
        Assert.IsNotNull(t, "нет дочерней коробки «" + name + "»");
        return (t!.localPosition / toU, t.localScale / toU);
    }

    private static List<AnalysisIssue> IssuesWithCode(string code)
    {
        var found = new List<AnalysisIssue>();
        foreach (var i in SceneAnalyzer.Analyze())
            if (i.Code == code) found.Add(i);
        return found;
    }

    // ── Габариты производителя ──────────────────────────────────────────

    [Test]
    public void Dishwasher_HasManufacturerDimensions()
    {
        var dw = Make();

        Assert.AreEqual(new Vector3Int(598, 815, 550), dw.DimensionsMM);
        Assert.AreEqual(600, DishwasherElement.NICHE_WIDTH_MM, "ниша шире корпуса — в габарит не входит");
    }

    /// <summary>Почему номинал именно 815, а не любое число из 815–875.
    ///
    /// Фасад стоит от верха цоколя до верха корпуса, значит цоколь = высота
    /// корпуса − высота фасада. На границах диапазонов брифа это даёт РОВНО
    /// заявленные пределы цоколя — то есть 655–725 (фасад) и 90–220 (цоколь)
    /// это один и тот же диапазон, пересчитанный через корпус, и 815 стоит на
    /// его нижнем конце. Если хоть одно из шести чисел брифа поедет, красным
    /// станет этот тест, а не геометрия.</summary>
    [Test]
    public void NominalHeight_TiesTheFacadeRangeToThePlinthRange()
    {
        Assert.AreEqual(DishwasherElement.HEIGHT_MIN_MM, DishwasherElement.BODY_HEIGHT_MM,
            "номинал = нижняя граница: ножки завинчены до упора");

        Assert.AreEqual(DishwasherElement.PLINTH_MIN_MM,
            DishwasherElement.HEIGHT_MIN_MM - DishwasherElement.FACADE_MAX_HEIGHT_MM,
            "815 − 725 = 90 — минимальный цоколь");
        Assert.AreEqual(DishwasherElement.PLINTH_MAX_MM,
            DishwasherElement.HEIGHT_MAX_MM - DishwasherElement.FACADE_MIN_HEIGHT_MM,
            "875 − 655 = 220 — максимальный цоколь");

        // Номинальный фасад 720 даёт цоколь 95, а НЕ минимальные 90: под
        // столешницей 820 остаётся 5 мм зазора, как и нужно встраиваемой технике.
        Assert.AreEqual(95, DishwasherElement.PlinthForFacade(
            DishwasherElement.FACADE_NOMINAL_HEIGHT_MM));
        Assert.AreEqual(820, DishwasherElement.FACADE_NOMINAL_HEIGHT_MM
            + DishwasherElement.PlinthForFacade(DishwasherElement.FACADE_NOMINAL_HEIGHT_MM) + 5);
    }

    /// <summary>РАСХОЖДЕНИЕ В БРИФЕ: одна и та же схема подписывает нишу под
    /// цоколь 89 мм, а сам цоколь — «min 90». Ограничением взят цоколь, а не
    /// его ниша; расхождение ровно в 1 мм и зафиксировано здесь, чтобы молча
    /// не превратиться в 89 при следующей правке.</summary>
    [Test]
    public void PlinthNiche_IsOneMmShorterThanTheMinimalPlinth()
    {
        Assert.AreEqual(1, DishwasherElement.PLINTH_MIN_MM - DishwasherElement.PLINTH_NICHE_MM);
    }

    [Test]
    public void Dishwasher_IsFixedSize()
    {
        var dw = Make();

        Assert.IsTrue(dw.HasFixedSize);
        Assert.IsTrue(FixedSize.IsFixed(dw), "общий признак техники видит посудомойку");
    }

    [Test]
    public void Dishwasher_ModelIsRegisteredInTheApplianceCatalog()
    {
        Assert.IsTrue(ApplianceModels.IsKnown(DishwasherElement.MODEL));
        Assert.IsFalse(ApplianceModels.IsKnown("Bosch SMV00NOSUCH"));
    }

    // ── Правка запрещена любым путём ────────────────────────────────────

    [Test]
    public void Dishwasher_ResizeIsIgnored()
    {
        var dw = Make();

        dw.DimensionsMM = new Vector3Int(800, 900, 700);

        Assert.AreEqual(new Vector3Int(598, 815, 550), dw.DimensionsMM,
            "габарит производителя возвращается на место в ApplyDimensions");
    }

    [Test]
    public void Dishwasher_HasNoResizeHandles()
    {
        var dw = Make();

        Assert.IsFalse(ResizeHandleManager.SupportsHandleResize(dw), "мышь не обходит окно свойств");
    }

    [Test]
    public void RootScaleStaysUnit()
    {
        var dw = Make();

        Assert.AreEqual(Vector3.one, dw.transform.localScale,
            "корень единичный — иначе дети масштабируются дважды");
    }

    [Test]
    public void Dishwasher_ColliderCoversTheWholeBox()
    {
        var dw = Make();
        float toU = AppConstants.MM_TO_UNITS;

        var box = dw.GetComponent<BoxCollider>();
        Assert.IsNotNull(box, "клик по машине ловит коллайдер корня");
        Assert.AreEqual(598f, box!.size.x / toU, 0.01f);
        Assert.AreEqual(815f, box.size.y / toU, 0.01f);
        Assert.AreEqual(550f, box.size.z / toU, 0.01f);
        Assert.AreEqual(Vector3.zero, box.center);
    }

    // ── Модель ──────────────────────────────────────────────────────────

    /// <summary>Восемь коробок: полый бак из пяти стенок, основание под ним и
    /// дверца с полосой панели. МЕБЕЛЬНОГО ФАСАДА среди детей БЫТЬ НЕ ДОЛЖНО —
    /// он отдельный элемент, и вторая передняя плоскость поверх пристёгнутой
    /// была бы браком; собственная дверца прибора («Door») — это не он.</summary>
    [Test]
    public void Dishwasher_IsEightBoxesAndCarriesNoFacadeOfItsOwn()
    {
        var dw = Make();

        var names = new List<string>();
        foreach (Transform child in dw.transform)
            if (child.gameObject.activeSelf) names.Add(child.name);

        CollectionAssert.AreEquivalent(
            new[]
            {
                "BodyBottom", "BodyTop", "BodyLeft", "BodyRight", "BodyBack",
                "Base", "Door", "ControlPanel",
            }, names,
            "полый бак из пяти стенок, основание и дверца с панелью управления");
        CollectionAssert.DoesNotContain(names, "Facade",
            "своей фасадной панели у полновстраиваемой машины нет");
    }

    /// <summary>ИСХОДНЫЙ СИМПТОМ: «в UI высота 815, а низа не видно». Низ
    /// прибора не рисовался вовсе — вся нижняя полоса считалась пустой нишей.
    ///
    /// Разбивка по высоте, которую задаёт производитель:
    ///   725 — дверца (она же бак), закрывается самым высоким фасадом;
    ///    90 — основание, УТОПЛЕННОЕ вглубь на 100 мм под носки обуви;
    ///   815 — сумма, паспортный габарит.</summary>
    [Test]
    public void Base_IsTheBottomBlockRecessedForToes()
    {
        var dw = Make();
        var (center, size) = BoxMM(dw, "Base");

        Assert.AreEqual(725, DishwasherElement.TANK_HEIGHT_MM, "дверца — 725");
        Assert.AreEqual(90, DishwasherElement.BASE_HEIGHT_MM, "основание — 90");
        Assert.AreEqual(815, DishwasherElement.TANK_HEIGHT_MM + DishwasherElement.BASE_HEIGHT_MM,
            "725 + 90 = 815 — общая высота");

        Assert.AreEqual(598f, size.x, 0.01f, "основание во всю ширину прибора");
        Assert.AreEqual(90f, size.y, 0.01f);
        Assert.AreEqual(450f, size.z, 0.01f, "550 − 100 утопления");
        Assert.AreEqual(-815f * 0.5f, center.y - size.y * 0.5f, 0.01f,
            "подошва — низ габарита: на ней машина и стоит");
        Assert.AreEqual(-550f * 0.5f, center.z - size.z * 0.5f, 0.01f,
            "тыл основания — по тылу прибора");
        Assert.AreEqual(550f * 0.5f - 100f, center.z + size.z * 0.5f, 0.01f,
            "перёд утоплен ровно на 100 мм — там ниша цоколя и место для ног");
    }

    /// <summary>Дверца стоит НА основании и доходит до верха: между ними ни
    /// щели, ни нахлёста, иначе 725 + 90 = 815 разошлось бы с моделью.</summary>
    [Test]
    public void Door_StandsOnTheBaseAndReachesTheTop()
    {
        var dw = Make();
        var (baseC, baseS) = BoxMM(dw, "Base");
        var (doorC, doorS) = BoxMM(dw, "Door");

        Assert.AreEqual(DishwasherElement.FACADE_MAX_HEIGHT_MM, doorS.y, 0.01f,
            "высота дверцы = самый высокий допустимый фасад");
        Assert.AreEqual(baseC.y + baseS.y * 0.5f, doorC.y - doorS.y * 0.5f, 0.01f,
            "низ дверцы — верх основания");
        Assert.AreEqual(815f * 0.5f, doorC.y + doorS.y * 0.5f, 0.01f, "верх дверцы — верх габарита");
    }

    [Test]
    public void ControlPanel_IsTheTopStripOfTheFront()
    {
        var dw = Make();
        var (center, size) = BoxMM(dw, "ControlPanel");

        Assert.AreEqual(598f, size.x, 0.01f, "полоса на всю ширину прибора");
        Assert.AreEqual(DishwasherElement.CONTROL_PANEL_HEIGHT_MM, size.y, 0.01f);
        Assert.AreEqual(815f * 0.5f, center.y + size.y * 0.5f, 0.01f, "прижата к верху корпуса");
        Assert.AreEqual(550f * 0.5f, center.z + size.z * 0.5f, 0.01f, "заподлицо с передом");
    }

    [Test]
    public void EveryPart_StaysInsideTheBox()
    {
        var dw = Make();
        float halfW = 598f * 0.5f, halfH = 815f * 0.5f, halfD = 550f * 0.5f;

        foreach (Transform child in dw.transform)
        {
            var (center, size) = BoxMM(dw, child.name);
            Assert.LessOrEqual(Mathf.Abs(center.x) + size.x * 0.5f, halfW + 0.01f, child.name + " по ширине");
            Assert.LessOrEqual(Mathf.Abs(center.y) + size.y * 0.5f, halfH + 0.01f, child.name + " по высоте");
            Assert.LessOrEqual(Mathf.Abs(center.z) + size.z * 0.5f, halfD + 0.01f, child.name + " по глубине");
        }
    }

    // ── Пристёгнутый фасад ──────────────────────────────────────────────

    [Test]
    public void Dishwasher_HasNoFacadeByDefault()
    {
        var dw = Make();

        Assert.IsEmpty(dw.AttachedFacadeName);
        Assert.IsNull(dw.FindAttachedFacade());
    }

    [Test]
    public void Dishwasher_IsAFacadeHost()
    {
        var dw = Make();

        Assert.IsInstanceOf<IFacadeHost>(dw, "фасад пристёгивается тем же механизмом, что у ящика");
    }

    [Test]
    public void AttachedFacade_IsFoundByName()
    {
        var dw = Make("DW1");
        var facade = MakeFacadeFor(dw, "DW1_front");

        dw.AttachedFacadeName = facade.PartName;

        Assert.AreSame(facade, dw.FindAttachedFacade());
    }

    /// <summary>Фасад, поставленный вплотную к переду машины, обязан считаться
    /// «в контакте» — на этом стоит и список в окне свойств, и DWH-02.</summary>
    [Test]
    public void AttachedFacade_InFrontOfTheMachine_CountsAsContact()
    {
        var dw = Make("DW2");
        var facade = MakeFacadeFor(dw, "DW2_front");

        Assert.IsTrue(DrawerLinks.IsFacadeInContact(dw, facade));
    }

    [Test]
    public void FacadeMovedAway_IsNoLongerInContact()
    {
        var dw = Make("DW3");
        var facade = MakeFacadeFor(dw, "DW3_front");

        facade.transform.position += new Vector3(0f, 0f, 0.3f);

        Assert.IsFalse(DrawerLinks.IsFacadeInContact(dw, facade));
    }

    /// <summary>Переименование фасада НЕ рвёт связь: DrawerLinks.Rename чинит
    /// обратные ссылки всех хозяев фасада, а не только ящиков.</summary>
    [Test]
    public void RenamingTheFacade_KeepsTheDishwasherLink()
    {
        var dw = Make("DW4");
        var facade = MakeFacadeFor(dw, "DW4_front");
        dw.AttachedFacadeName = facade.PartName;

        DrawerLinks.Rename(facade, "Fasad_DW");

        Assert.AreEqual("Fasad_DW", facade.PartName);
        Assert.AreEqual("Fasad_DW", dw.AttachedFacadeName, "ссылка машины на фасад обновлена");
        Assert.AreSame(facade, dw.FindAttachedFacade());
    }

    /// <summary>Удаление фасада не оставляет висячей ссылки на объект: имя
    /// перестаёт находиться, и это ловит валидация (см. DWH-02 ниже).</summary>
    [Test]
    public void DeletingTheFacade_LeavesNoDanglingReference()
    {
        var dw = Make("DW5");
        var facade = MakeFacadeFor(dw, "DW5_front");
        dw.AttachedFacadeName = facade.PartName;

        CommandStack.Execute(new DeleteCommand(facade.gameObject));

        Assert.IsNull(dw.FindAttachedFacade(), "удалённый фасад больше не находится");

        CommandStack.Undo();
        Assert.AreSame(facade, dw.FindAttachedFacade(), "и возвращается вместе с undo");
    }

    [Test]
    public void Duplicate_DoesNotStealTheFacade()
    {
        var dw = Make("DW6");
        var facade = MakeFacadeFor(dw, "DW6_front");
        dw.AttachedFacadeName = facade.PartName;

        var copyGo = ElementFactory.Duplicate(dw);
        _spawned.Add(copyGo);

        var copy = copyGo.GetComponent<DishwasherElement>();
        Assert.IsNotNull(copy, "копия посудомойки — посудомойка, а не голая деталь");
        Assert.AreEqual(new Vector3Int(598, 815, 550), copy!.DimensionsMM);
        Assert.IsEmpty(copy.AttachedFacadeName, "копия не крадёт чужой фасад");
    }

    // ── Валидация фасада: ПРЕДУПРЕЖДЕНИЯ, а не отказы ───────────────────

    [Test]
    public void NoFacade_IsReportedAsDwh01()
    {
        Make("DW7");

        var issues = IssuesWithCode(IssueCatalog.CodeDishwasherNoFacade);

        Assert.AreEqual(1, issues.Count, "полновстраиваемая машина без фасада — дефект сборки");
        Assert.AreEqual(IssueLevel.Warning, issues[0].Level, "предупреждение, а не ошибка");
    }

    [Test]
    public void FacadeAttached_ClearsDwh01()
    {
        var dw = Make("DW8");
        var facade = MakeFacadeFor(dw, "DW8_front");
        dw.AttachedFacadeName = facade.PartName;

        Assert.IsEmpty(IssuesWithCode(IssueCatalog.CodeDishwasherNoFacade));
        Assert.IsEmpty(IssuesWithCode(IssueCatalog.CodeDishwasherFacadeOrphaned));
        Assert.IsEmpty(IssuesWithCode(IssueCatalog.CodeDishwasherFacadeHeight));
    }

    [Test]
    public void FacadeMovedAway_IsReportedAsDwh02()
    {
        var dw = Make("DW9");
        var facade = MakeFacadeFor(dw, "DW9_front");
        dw.AttachedFacadeName = facade.PartName;

        facade.transform.position += new Vector3(0f, 0f, 0.3f);

        Assert.AreEqual(1, IssuesWithCode(IssueCatalog.CodeDishwasherFacadeOrphaned).Count);
    }

    [Test]
    public void DeletedFacade_IsReportedAsDwh02()
    {
        var dw = Make("DW10");
        var facade = MakeFacadeFor(dw, "DW10_front");
        dw.AttachedFacadeName = facade.PartName;

        CommandStack.Execute(new DeleteCommand(facade.gameObject));

        var issues = IssuesWithCode(IssueCatalog.CodeDishwasherFacadeOrphaned);
        Assert.AreEqual(1, issues.Count, "имя есть, фасада нет — это должно быть СЛЫШНО");
        StringAssert.Contains("DW10_front", issues[0].Message);
    }

    /// <summary>Фасад вне 655–725 — повод СООБЩИТЬ, а не отказать: пристегнуть
    /// его получается, и элемент остаётся связанным.</summary>
    [Test]
    public void FacadeOutOfHeightRange_WarnsButStaysAttached()
    {
        var dw = Make("DW11");
        var facade = MakeFacadeFor(dw, "DW11_front", heightMM: 600);
        dw.AttachedFacadeName = facade.PartName;

        var issues = IssuesWithCode(IssueCatalog.CodeDishwasherFacadeHeight);

        Assert.AreEqual(1, issues.Count);
        Assert.AreEqual(IssueLevel.Warning, issues[0].Level);
        StringAssert.Contains("655", issues[0].Message);
        Assert.AreSame(facade, dw.FindAttachedFacade(), "связь не разорвана");
    }

    [Test]
    public void FacadeHeightRange_IsInclusiveAtBothEnds()
    {
        Assert.IsTrue(DishwasherElement.IsFacadeHeightValid(DishwasherElement.FACADE_MIN_HEIGHT_MM));
        Assert.IsTrue(DishwasherElement.IsFacadeHeightValid(DishwasherElement.FACADE_MAX_HEIGHT_MM));
        Assert.IsFalse(DishwasherElement.IsFacadeHeightValid(DishwasherElement.FACADE_MIN_HEIGHT_MM - 1));
        Assert.IsFalse(DishwasherElement.IsFacadeHeightValid(DishwasherElement.FACADE_MAX_HEIGHT_MM + 1));
    }

    // ── Каталог ─────────────────────────────────────────────────────────

    [Test]
    public void Catalog_ApplianceGroup_HasDishwasherFourth()
    {
        var group = SidebarCatalog.Build().Find(g => g.title == "Техника");

        Assert.AreEqual(4, group.items.Count, "общая варочная, модельная варочная, духовка, посудомойка");
        var dw = group.items[3];
        Assert.IsTrue(dw.isDishwasher, "посудомойка — четвёртый пункт «Техники»");
        Assert.AreEqual(DishwasherElement.MODEL, dw.applianceModel);
        Assert.AreEqual(new Vector3Int(598, 815, 550), dw.dims);
        Assert.IsFalse(dw.isOven, "иначе SidebarUI.Spawn ушёл бы не туда");
        Assert.IsFalse(dw.isCooktop);
    }

    // ── Сериализация ────────────────────────────────────────────────────

    [Test]
    public void ElementData_MarksTheDishwasher()
    {
        var dw = Make();

        var data = ElementData.FromElement(dw);

        Assert.IsTrue(data.isDishwasher);
        Assert.IsFalse(data.isOven);
        Assert.IsFalse(data.isCooktop);
    }

    [Test]
    public void Dishwasher_SurvivesSaveLoadRoundTripWithItsFacade()
    {
        var dw = Make("DW_rt");
        dw.transform.position = new Vector3(1f, 0.4075f, 2f);
        var facade = MakeFacadeFor(dw, "DW_rt_front");
        dw.AttachedFacadeName = facade.PartName;

        var path = Path.Combine(Application.temporaryCachePath, $"rt_dw_{System.Guid.NewGuid():N}.json");
        var saved = SaveLoadManager.CaptureScene(new List<KitchenElement> { dw, facade });
        SaveLoadManager.SaveToFile(path, saved);

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        var loaded = SaveLoadManager.LoadFromFile(path);
        Assert.IsNotNull(loaded);
        SaveLoadManager.RestoreScene(loaded!);
        File.Delete(path);

        var restored = Object.FindFirstObjectByType<DishwasherElement>();
        Assert.IsNotNull(restored, "машина восстановилась своим типом, а не деталью");
        Assert.AreEqual(new Vector3Int(598, 815, 550), restored!.DimensionsMM);
        Assert.IsTrue(restored.HasFixedSize);
        Assert.AreEqual(8, restored.transform.childCount, "бак, основание и дверца собраны заново целиком");
        Assert.AreEqual("DW_rt_front", restored.AttachedFacadeName, "привязка фасада пережила сохранение");
        Assert.IsNotNull(restored.FindAttachedFacade(), "и фасад по этому имени действительно находится");
    }

    /// <summary>Загрузка переименовывает элементы при коллизии имён; ссылка на
    /// фасад обязана поехать вместе с ним (тот же Remap, что у ящика).</summary>
    [Test]
    public void Loading_RemapsTheFacadeLinkWhenTheFacadeIsRenamed()
    {
        ElementData Data(string name) => new ElementData
        {
            name = name,
            dimensionsMM = new[] { 600, 720, 18 },
            position = new[] { 0f, 0f, 0f },
            rotation = new[] { 0f, 0f, 0f, 1f },
        };

        var facadeData = Data("Фасад 600");
        facadeData.isFacade = true;

        var dwData = Data("Posudomojka");
        dwData.isDishwasher = true;
        dwData.dishwasherAttachedFacadeName = "Фасад 600";

        var mgr = new SaveLoadManagerInstance();
        var created = mgr.RestoreScene(new ProjectData { elements = new[] { facadeData, dwData } });
        foreach (var go in created) _spawned.Add(go);

        var dw = created[1].GetComponent<DishwasherElement>();
        Assert.IsNotNull(dw);
        Assert.AreEqual("Fasad_600", dw!.AttachedFacadeName,
            "имя фасада нормализовано — ссылка поехала за ним");
        Assert.IsNotNull(dw.FindAttachedFacade());
    }

    // ── MCP ─────────────────────────────────────────────────────────────

    private McpRequest MakeReq(string method, object data)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        return new McpRequest { id = "test", method = method, Params = JObject.Parse(json) };
    }

    private static JObject Payload(McpResponse resp) => JObject.FromObject(resp.data!);

    private static string ErrorMessage(McpResponse resp) =>
        Payload(resp)["message"]!.Value<string>()!;

    [Test]
    public void Mcp_CreatesDishwasherWithManufacturerSize()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[]
            {
                new { name = "DW-mcp", type = "dishwasher", x = 0f, y = 0f, z = 0f, width = 900 }
            }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        var dw = Object.FindFirstObjectByType<DishwasherElement>();
        Assert.IsNotNull(dw);
        Assert.AreEqual(new Vector3Int(598, 815, 550), dw!.DimensionsMM,
            "width из запроса на готовую модель не влияет");
    }

    [Test]
    public void Mcp_RejectsResizingTheDishwasher()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[] { new { name = "DW-fix", type = "dishwasher", x = 0f, y = 0f, z = 0f } }
        }));

        var resp = _handler.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "DW-fix", height = 700 } }
        }));

        Assert.AreEqual("error", resp.type, "правка размера готовой модели обязана быть ОТКАЗОМ");
        StringAssert.Contains("fixed appliance", ErrorMessage(resp));
    }

    [Test]
    public void Mcp_RejectsAModelFromAnotherAppliance()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[]
            {
                new { name = "Oven-wrong", type = "oven", x = 0f, y = 0f, z = 0f,
                      model = DishwasherElement.MODEL }
            }
        }));

        Assert.AreEqual("error", resp.type, "модель посудомойки не делает духовку посудомойкой");
        StringAssert.Contains("does not belong to type", ErrorMessage(resp));
    }

    [Test]
    public void Mcp_AttachesAndDetachesTheFacade()
    {
        var dw = Make("DW-mcp2");
        var facade = MakeFacadeFor(dw, "DW_mcp2_front");

        var attach = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = dw.PartName, attached_facade_name = facade.PartName } }
        }));
        Assert.AreEqual("result", attach.type, attach.type == "error" ? ErrorMessage(attach) : "");
        Assert.AreEqual(facade.PartName, dw.AttachedFacadeName);

        var detach = _handler.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = dw.PartName, attached_facade_name = "" } }
        }));
        Assert.AreEqual("result", detach.type);
        Assert.IsEmpty(dw.AttachedFacadeName);
    }

    /// <summary>Чужому типу поле фасада по-прежнему не положено — иначе
    /// обобщение проверки съело бы её смысл.</summary>
    [Test]
    public void Mcp_RejectsAttachedFacadeOnAPlainBoard()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Board1", Vector3.zero);
        _spawned.Add(go);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Board1", attached_facade_name = "whatever" } }
        }));

        Assert.AreEqual("error", resp.type);
        StringAssert.Contains("attached_facade_name", ErrorMessage(resp));
    }

    [Test]
    public void Mcp_ReportsTheDishwasherBreakdown()
    {
        var dw = Make("DW-info");
        var facade = MakeFacadeFor(dw, "DW_info_front");
        dw.AttachedFacadeName = facade.PartName;

        var resp = _handler!.Handle(MakeReq("get_elements", new { filter = "DW-info" }));

        Assert.AreEqual("result", resp.type);
        var info = Payload(resp)["elements"]![0]!["dishwasher"]!;
        Assert.AreEqual(DishwasherElement.MODEL, info["model"]!.Value<string>());
        Assert.IsTrue(info["fixedSize"]!.Value<bool>());
        Assert.AreEqual("DW_info_front", info["attachedFacadeName"]!.Value<string>());
        Assert.AreEqual(600, info["nicheWidthMM"]!.Value<int>());
        Assert.AreEqual(720, info["facadeNominalHeightMM"]!.Value<int>());
        Assert.AreEqual(95, info["plinthMM"]!.Value<int>(),
            "цоколь под номинальным фасадом 720 при корпусе 815");
    }

    [Test]
    public void Selector_NamesTheDishwasherType()
    {
        var dw = Make("DW-sel");

        Assert.AreEqual("dishwasher", ElementSelector.TypeOf(dw));
    }

    // ── Навеска фасада на кронштейнах ───────────────────────────────────
    // Фасад полновстраиваемой машины держат кронштейны, а не винты через
    // фронт: он ОБЯЗАН стоять с монтажным зазором, иначе дверь не откинется.
    // Ящик — противоположный случай, и послабление к нему не относится.

    /// <summary>Тот же MakeFacadeFor, но фасад отодвинут от фронта прибора на
    /// gapMM — ровно то, что делает монтажный кронштейн.</summary>
    private FacadeElement MakeFacadeAtGap(DishwasherElement dw, string name, float gapMM,
        int heightMM = DishwasherElement.FACADE_NOMINAL_HEIGHT_MM)
    {
        var facade = MakeFacadeFor(dw, name, heightMM);
        facade.transform.position += new Vector3(0f, 0f, gapMM * AppConstants.MM_TO_UNITS);
        return facade;
    }

    private DrawerElement MakeDrawerWithFacadeAtGap(string name, float gapMM,
        out FacadeElement facade)
    {
        float toU = AppConstants.MM_TO_UNITS;
        var drawerGo = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite,
            400, name, new Vector3(0f, 0.043f, 0f));
        _spawned.Add(drawerGo);
        // Задняя грань фасада ровно на фронте ящика — плюс проверяемый зазор.
        var facadeGo = ElementFactory.CreateFacade(new Vector3Int(400, 86, 18), name + "_front",
            new Vector3(0f, 0.043f, 0.184f + gapMM * toU), 2, 2, 2, 2);
        _spawned.Add(facadeGo);
        facade = facadeGo.GetComponent<FacadeElement>();
        return drawerGo.GetComponent<DrawerElement>();
    }

    /// <summary>ИСХОДНЫЙ СИМПТОМ ДОСЛОВНО. В проекте пользователя фасад
    /// B3_door стоит в 2 мм от посудомойки — это нормальная навеска на
    /// кронштейнах, а не ошибка. Раньше контакта «не было», список фасадов в
    /// окне свойств оказывался пустым и крепить было нечего.
    ///
    /// Второй половиной теста заперт ящик: у него фронт стянут с фасадом
    /// винтами заподлицо, и те же 2 мм обязаны остаться отрывом.</summary>
    [Test]
    public void FacadeTwoMillimetresAway_AttachesToTheDishwasher_ButNotToADrawer()
    {
        var dw = Make("DW-2mm");
        var dwFacade = MakeFacadeAtGap(dw, "DW_2mm_front", 2f);

        Assert.IsTrue(DrawerLinks.IsFacadeInContact(dw, dwFacade),
            "фасад на кронштейнах в 2 мм — это навешенный фасад");

        var drawer = MakeDrawerWithFacadeAtGap("Yaschik2mm", 2f, out var drawerFacade);

        Assert.IsFalse(DrawerLinks.IsFacadeInContact(drawer, drawerFacade),
            "фронт ящика прикручен заподлицо: 2 мм у него — это отрыв");
    }

    [Test]
    public void MountGap_IsFiveMillimetresForTheDishwasherAndZeroForTheDrawer()
    {
        var dw = Make("DW-gapconst");
        var drawerGo = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite,
            400, "Yaschik-gapconst", Vector3.zero);
        _spawned.Add(drawerGo);

        Assert.AreEqual(5f, DishwasherElement.FACADE_MOUNT_GAP_MM, 1e-4f);
        Assert.AreEqual(5f, dw.FacadeMountGapMm, 1e-4f);
        Assert.AreEqual(0f, drawerGo.GetComponent<DrawerElement>().FacadeMountGapMm, 1e-4f,
            "у ящика монтажного зазора нет — проверка остаётся строгой");
    }

    /// <summary>Допуск конечный: фасад дальше него — уже не навеска, а
    /// отдельная деталь рядом. Иначе «пристёгнут» перестало бы что-либо
    /// значить.</summary>
    [Test]
    public void FacadeBeyondTheMountGap_IsNotAttached()
    {
        var dw = Make("DW-far");
        var facade = MakeFacadeAtGap(dw, "DW_far_front",
            DishwasherElement.FACADE_MOUNT_GAP_MM + 3f);

        Assert.IsFalse(DrawerLinks.IsFacadeInContact(dw, facade));
    }

    /// <summary>Допуск меряет зазор ПО НОРМАЛИ и требует перекрытия — сдвиг
    /// фасада вбок его не обманывает.</summary>
    [Test]
    public void FacadeSlidSideways_IsNotAttachedEvenWithinTheGap()
    {
        var dw = Make("DW-side");
        var facade = MakeFacadeAtGap(dw, "DW_side_front", 2f);

        facade.transform.position += new Vector3(0.9f, 0f, 0f);

        Assert.IsFalse(DrawerLinks.IsFacadeInContact(dw, facade));
    }

    /// <summary>Вся цепочка, а не только выпадающий список: окно свойств
    /// предлагает фасад, не красит подпись красным, а анализатор не выдаёт
    /// DWH-02. Все трое зовут один IsFacadeInContact — тест сторожит, что они
    /// согласны. Фасад стоит на 5мм (нижняя граница монтажного зазора),
    /// чтобы DWH-04 не вмешивался.</summary>
    [Test]
    public void FacadeOnBrackets_IsOfferedNotOrphanedAndRaisesNoDwh02()
    {
        var panel = BuildMenu();
        var dw = Make("DW-chain");
        var facade = MakeFacadeAtGap(dw, "DW_chain_front", 5f);
        dw.AttachedFacadeName = facade.PartName;

        _menu!.Open(dw);
        var dropdown = panel.GetComponentInChildren<TMPro.TMP_Dropdown>(true);

        var names = new List<string>();
        foreach (var o in FacadeDropdown(panel).options) names.Add(o.text);
        CollectionAssert.Contains(names, "DW_chain_front",
            "фасад на 5мм обязан попадать в список окна свойств");

        Assert.AreNotEqual(Color.red, FacadeDropdown(panel).captionText.color,
            "подпись не красная — фасад не оторван");

        CollectionAssert.IsEmpty(IssuesWithCode("DWH-02"),
            "правильно навешенный фасад — не оторвавшийся");
        CollectionAssert.IsEmpty(IssuesWithCode("DWH-04"),
            "5мм — нижняя граница монтажного зазора, DWH-04 не светится");
        Assert.IsNotNull(dropdown);
    }

    /// <summary>Пара «посудомойка ↔ её фасад» проверяется правилом «зазор сзади
    /// ≥5мм», а не общим GAP-01/02. Чужой фасад в тех же координатах подчиняется
    /// общему правилу [2..4]: 1мм даёт GAP-01 (снап не дожат), 3мм — зелёная
    /// зона, 5мм — GAP-02 (снап не сработал).</summary>
    [Test]
    public void AttachedFacade_FollowsFiveMmRule_StrangerFollowsGeneralRule()
    {
        var dw = Make("DW-gap01");
        var facade = MakeFacadeAtGap(dw, "DW_gap01_front", 2f);

        // ── Свой фасад пристёгнут: 2мм < 5мм → DWH-04, общее правило не
        //    применяется (FindNearContacts пропускает пару).
        dw.AttachedFacadeName = facade.PartName;
        CollectionAssert.IsEmpty(PairsWithCode("GAP-01", dw, facade),
            "общее правило GAP-01 для пристёгнутого фасада не действует");
        CollectionAssert.IsEmpty(PairsWithCode("GAP-02", dw, facade),
            "общее правило GAP-02 для пристёгнутого фасада не действует");
        Assert.IsNotEmpty(PairsWithCode("DWH-04", dw, facade),
            "2мм < 5мм — фасад прижат к прибору, нужен зазор по схеме");

        // ── Тот же фасад на 5мм (ровно монтажный минимум): ошибок нет.
        var ok = MakeFacadeAtGap(dw, "DW_ok_front", 5f);
        dw.AttachedFacadeName = ok.PartName;
        CollectionAssert.IsEmpty(IssuesWithCode("DWH-04"),
            "ровно 5мм — нижняя граница монтажного зазора, ОК");
        CollectionAssert.IsEmpty(PairsWithCode("GAP-01", dw, ok));
        CollectionAssert.IsEmpty(PairsWithCode("GAP-02", dw, ok));

        // ── Отстёгнутый фасад в 3мм: зелёная зона [2..4], ошибок нет.
        dw.AttachedFacadeName = "";
        CollectionAssert.IsEmpty(PairsWithCode("GAP-01", dw, facade),
            "3мм в зелёной зоне — отстёгнутый фасад не светится");
        CollectionAssert.IsEmpty(PairsWithCode("GAP-02", dw, facade));

        // ── Отстёгнутый фасад на 1мм: снап не дожат → GAP-01.
        var near = MakeFacadeAtGap(dw, "DW_1mm_front", 1f);
        Assert.IsNotEmpty(PairsWithCode("GAP-01", dw, near),
            "1мм < 2мм — снап не дожат, GAP-01");

        // ── Отстёгнутый фасад на 5мм: снап не сработал → GAP-02.
        var far = MakeFacadeAtGap(dw, "DW_5mm_front", 5f);
        Assert.IsNotEmpty(PairsWithCode("GAP-02", dw, far),
            "5мм > 4мм — снап не сработал, GAP-02");
    }

    // ── Задний зазор фасада посудомойки ≥ 5мм (DWH-04) ────────────────

    [Test]
    public void DishwasherFacadeBackGap_BelowFive_FiresDwh04()
    {
        var dw = Make("DW-bg-small");
        var facade = MakeFacadeAtGap(dw, "DW_bg_small_front", 3f);
        dw.AttachedFacadeName = facade.PartName;

        Assert.IsNotEmpty(PairsWithCode("DWH-04", dw, facade),
            "3мм < 5мм — фасад прижат к прибору, DWH-04");
    }

    [Test]
    public void DishwasherFacadeBackGap_AtFive_NoDwh04()
    {
        var dw = Make("DW-bg-ok");
        var facade = MakeFacadeAtGap(dw, "DW_bg_ok_front", 5f);
        dw.AttachedFacadeName = facade.PartName;

        CollectionAssert.IsEmpty(IssuesWithCode("DWH-04"),
            "ровно 5мм — нижняя граница, ОК");
    }

    [Test]
    public void DishwasherFacadeBackGap_AboveFive_NoDwh04NoGapRules()
    {
        var dw = Make("DW-bg-far");
        var facade = MakeFacadeAtGap(dw, "DW_bg_far_front", 8f);
        dw.AttachedFacadeName = facade.PartName;

        // Пара «прибор ↔ фасад» исключена из общего правила, и своё правило
        // 5мм тоже выполнено — никаких ошибок.
        CollectionAssert.IsEmpty(IssuesWithCode("DWH-04"));
        CollectionAssert.IsEmpty(PairsWithCode("GAP-01", dw, facade));
        CollectionAssert.IsEmpty(PairsWithCode("GAP-02", dw, facade));
    }

    [Test]
    public void DishwasherFacadeBackGap_NoAttachedFacade_NoDwh04()
    {
        // Без фасада — DWH-04 не выдаётся (там нечему мерить), а DWH-01
        // срабатывает на отсутствие фасада.
        var dw = Make("DW-bg-none");
        CollectionAssert.IsEmpty(IssuesWithCode("DWH-04"));
        Assert.IsNotEmpty(IssuesWithCode("DWH-01"),
            "у машины нет фасада — DWH-01, своя отдельная ветка");
    }

    // ── Открывание не отвинчивает фасад (DWH-02/DWH-04) ───────────────

    /// <summary>СИМПТОМ: стоит ОТКРЫТЬ посудомойку — загорается DWH-02 «фасад
    /// не на месте», закрыть — гаснет. Навеска от открывания не меняется:
    /// фасад-пассажир едет вместе с дверцей, и контакт с прибором обязан
    /// меряться по ЗАКРЫТОЙ позе, а не по той, в которой фасад лежит
    /// горизонтально в полуметре от корпуса.</summary>
    [Test]
    public void OpenDoor_KeepsTheAttachedFacadeMounted()
    {
        var dw = Make("DW-open-link");
        var facade = MakeFacadeAtGap(dw, "DW_open_link_front", 5f);
        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);

        Assert.IsTrue(DrawerLinks.IsFacadeInContact(dw, facade), "закрытая машина: фасад навешен");
        CollectionAssert.IsEmpty(IssuesWithCode("DWH-02"));

        var closedPos = facade.transform.position;
        dw.SetOpen(true);
        dw.StepDoor(10f);

        Assert.AreEqual(1f, dw.DoorProgress, 1e-4f, "дверца действительно откинута");
        Assert.Greater((facade.transform.position - closedPos).magnitude, 0.1f,
            "и фасад действительно уехал вместе с ней — иначе проверка ниже холостая");

        Assert.IsTrue(DrawerLinks.IsFacadeInContact(dw, facade),
            "открывание не отвинчивает фасад от кронштейнов");
        CollectionAssert.IsEmpty(IssuesWithCode("DWH-02"),
            "DWH-02 у открытой машины — это и был баг");
        CollectionAssert.IsEmpty(IssuesWithCode("DWH-04"),
            "монтажный зазор тоже меряется по закрытой позе");
    }

    /// <summary>Обратная сторона: подмена позы НЕ должна прятать настоящий
    /// отрыв. Фасад, оттащенный от ЗАКРЫТОЙ машины, обязан дать DWH-02.</summary>
    [Test]
    public void FacadeDraggedAwayFromAClosedDishwasher_StillFiresDwh02()
    {
        var dw = Make("DW-open-link2");
        var facade = MakeFacadeAtGap(dw, "DW_open_link2_front", 5f);
        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);
        CollectionAssert.IsEmpty(IssuesWithCode("DWH-02"));

        facade.transform.position += new Vector3(0f, 0f, 0.3f);

        Assert.IsFalse(DrawerLinks.IsFacadeInContact(dw, facade));
        Assert.IsNotEmpty(PairsWithCode("DWH-02", dw, facade),
            "фасад в 300мм от прибора — он оторван, и это обязано быть видно");
    }

    private static List<AnalysisIssue> PairsWithCode(string code, KitchenElement a, KitchenElement b)
    {
        var found = new List<AnalysisIssue>();
        foreach (var i in IssuesWithCode(code))
            if ((i.Target == a && i.Secondary == b) || (i.Target == b && i.Secondary == a))
                found.Add(i);
        return found;
    }

    // ── Фасад пристёгнут: анимация хоста, фасад — пассажир ─────────────

    /// <summary>Пристёгнутый фасад становится пассажиром дверцы: своей
    /// анимации больше нет, трансформом владеет посудомойка.</summary>
    [Test]
    public void AttachedFacade_BecomesPassenger()
    {
        var dw = Make("DW-pass1");
        var facade = MakeFacadeFor(dw, "DW_pass1_front");

        Assert.IsFalse(facade.IsPassenger, "до пристёгивания фасад — самостоятельная дверца");

        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);

        Assert.IsTrue(facade.IsPassenger, "после пристёгивания фасад — пассажир дверцы");
    }

    /// <summary>Отстёгнутый фасад перестаёт быть пассажиром: его собственная
    /// анимация (та же, что была до пристёгивания) снова включается.</summary>
    [Test]
    public void DetachingFacade_RestoresSelfAnimation()
    {
        var dw = Make("DW-pass2");
        var facade = MakeFacadeFor(dw, "DW_pass2_front");
        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);
        Assert.IsTrue(facade.IsPassenger);

        var prev = dw.FindAttachedFacade();
        dw.AttachedFacadeName = "";
        dw.OnAttachedFacadeChanged(prev, null);

        Assert.IsFalse(facade.IsPassenger, "отстёгнутый фасад снова анимируется сам");
    }

    /// <summary>Открытие посудомойки НЕ двигает фасад по его собственной петле
    /// (раньше именно это и происходило: <c>SyncAttachedFacade</c> звал
    /// <c>f.SetOpen</c>, и фасад крутил свой <c>DoorMode.HingeFrontLeft</c>).
    /// Сейчас фасад — пассажир, его собственный прогресс анимации остаётся 0.</summary>
    [Test]
    public void AttachedFacade_DoesNotAnimateOnItsOwn()
    {
        var dw = Make("DW-pass3");
        var facade = MakeFacadeFor(dw, "DW_pass3_front");
        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);

        dw.SetOpen(true);
        for (int i = 0; i < 10; i++) dw.StepDoor(0.1f);

        Assert.IsTrue(facade.IsOpen, "состояние синхронизируется");
        Assert.AreEqual(0f, facade.DoorProgress, 0.001f,
            "собственная анимация фасада выключена — прогресс 0");
    }

    /// <summary>Поза фасада-пассажира при любом прогрессе дверцы совпадает с
    /// расчётом «закрытая поза фасада, повёрнутая вокруг петли дверцы на тот
    /// же угол». Именно так крышка и фасад остаются склеенными.</summary>
    [Test]
    public void AttachedFacade_FollowsDoorHingeAtEveryProgress()
    {
        var dw = Make("DW-pass4");
        var facade = MakeFacadeFor(dw, "DW_pass4_front");
        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);

        float toU = AppConstants.MM_TO_UNITS;
        var hingeLocal = DishwasherElement.HingeLocalMM * toU;

        for (float p = 0f; p <= 1.0001f; p += 0.1f)
        {
            dw.SetOpen(p > 0f);
            dw.StepDoor(0.6f); // больше, чем OpenSeconds → _t выходит на target
            dw.ApplyDoorPose();

            var doorRot = DishwasherElement.DoorLocalRotation(dw.DoorProgress);
            var facadeLocal = dw.transform.InverseTransformPoint(facade.ClosedPosition);
            var facadeLocalRot = Quaternion.Inverse(dw.transform.rotation) * facade.ClosedRotation;
            var expectedLocal = hingeLocal + doorRot * (facadeLocal - hingeLocal);
            var expectedWorld = dw.transform.TransformPoint(expectedLocal);
            var expectedRotWorld = dw.transform.rotation * (doorRot * facadeLocalRot);

            Assert.AreEqual(expectedWorld.x, facade.transform.position.x, 1e-4f,
                $"прогресс {p:F2}: X фасада совпадает с расчётом через петлю дверцы");
            Assert.AreEqual(expectedWorld.y, facade.transform.position.y, 1e-4f,
                $"прогресс {p:F2}: Y фасада совпадает с расчётом через петлю дверцы");
            Assert.AreEqual(expectedWorld.z, facade.transform.position.z, 1e-4f,
                $"прогресс {p:F2}: Z фасада совпадает с расчётом через петлю дверцы");
            Assert.AreEqual(expectedRotWorld.x, facade.transform.rotation.x, 1e-4f,
                $"прогресс {p:F2}: поворот фасада совпадает с расчётом");
            Assert.AreEqual(expectedRotWorld.y, facade.transform.rotation.y, 1e-4f);
            Assert.AreEqual(expectedRotWorld.z, facade.transform.rotation.z, 1e-4f);
            Assert.AreEqual(expectedRotWorld.w, facade.transform.rotation.w, 1e-4f);
        }
    }

    /// <summary>AABB открытого состояния посудомойки учитывает фасад через
    /// ПЕТЛЮ ДВЕРЦЫ, а не собственную петлю фасада. Раньше вызов
    /// <c>facade.GetOpenBounds</c> использовал <c>FacadeDoor.Pose</c> с модой
    /// фасада по умолчанию, и проверка коллизий ловила призрак — фасад ехал
    /// вбок, пока дверца шла вниз.</summary>
    [Test]
    public void GetOpenBounds_AttachedFacade_UsesDishwasherHinge()
    {
        var dw = Make("DW-pass5");
        var facade = MakeFacadeFor(dw, "DW_pass5_front");
        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);

        // Полностью открытая дверца: фасад ложится горизонтально перед ней.
        dw.SetOpen(true);
        dw.StepDoor(0.6f);
        dw.ApplyDoorPose();

        var (min, max) = dw.GetOpenBounds(1f);
        var half = facade.transform.localScale * 0.5f;
        // Все 8 углов текущего AABB фасада должны входить в AABB открытого
        // состояния — именно это и есть «фасад посчитан через петлю дверцы».
        // Если бы фасад считали по своей петле, его углы уехали бы вбок и
        // часть из них выпала бы из GetOpenBounds.
        const float eps = 1e-4f;
        var corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
            corners[i] = facade.transform.position + facade.transform.rotation * new Vector3(
                (i & 1) == 0 ? -half.x : half.x,
                (i & 2) == 0 ? -half.y : half.y,
                (i & 4) == 0 ? -half.z : half.z);
        foreach (var c in corners)
        {
            Assert.GreaterOrEqual(c.x, min.x - eps, $"угол {c} внутри AABB по X");
            Assert.LessOrEqual(c.x, max.x + eps, $"угол {c} внутри AABB по X");
            Assert.GreaterOrEqual(c.y, min.y - eps, $"угол {c} внутри AABB по Y");
            Assert.LessOrEqual(c.y, max.y + eps, $"угол {c} внутри AABB по Y");
            Assert.GreaterOrEqual(c.z, min.z - eps, $"угол {c} внутри AABB по Z");
            Assert.LessOrEqual(c.z, max.z + eps, $"угол {c} внутри AABB по Z");
        }
    }

    /// <summary>Round-trip «открыть → закрыть» возвращает фасад РОВНО в ту
    /// позу, в которой его поставил пользователь перед пристёгиванием. Раньше
    /// <c>ClosedPosition</c> для пассажира возвращал <c>transform.position</c>
    /// после открытия — и закрытие закрепляло фасад в позиции, до которой его
    /// довезла петля дверцы, а не там, где он стоял изначально.</summary>
    [Test]
    public void AttachedFacade_OpenThenClose_ReturnsToOriginalPose()
    {
        var dw = Make("DW-rt");
        var facade = MakeFacadeFor(dw, "DW_rt_front");
        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);
        dw.ApplyDoorPose();

        var closedPos = facade.transform.position;
        var closedRot = facade.transform.rotation;

        // Полностью открыть.
        dw.SetOpen(true);
        for (int i = 0; i < 10; i++) dw.StepDoor(0.1f);
        dw.ApplyDoorPose();
        Assert.AreNotEqual(closedPos, facade.transform.position,
            "на открытой дверце фасад должен уехать из закрытой позы");

        // Полностью закрыть.
        dw.SetOpen(false);
        for (int i = 0; i < 10; i++) dw.StepDoor(0.1f);
        dw.ApplyDoorPose();

        Assert.AreEqual(closedPos.x, facade.transform.position.x, 1e-4f,
            "закрытие возвращает фасад в исходную позицию по X");
        Assert.AreEqual(closedPos.y, facade.transform.position.y, 1e-4f,
            "закрытие возвращает фасад в исходную позицию по Y");
        Assert.AreEqual(closedPos.z, facade.transform.position.z, 1e-4f,
            "закрытие возвращает фасад в исходную позицию по Z");
        Assert.AreEqual(closedRot.x, facade.transform.rotation.x, 1e-4f,
            "закрытие возвращает фасад в исходный поворот");
        Assert.AreEqual(closedRot.y, facade.transform.rotation.y, 1e-4f);
        Assert.AreEqual(closedRot.z, facade.transform.rotation.z, 1e-4f);
        Assert.AreEqual(closedRot.w, facade.transform.rotation.w, 1e-4f);
    }

    /// <summary>«E» на выделенном фасаде, пристёгнутом к посудомойке, идёт
    /// через <c>dw.ToggleOpen()</c> — а не через <c>f.ToggleOpen()</c> (фасад
    /// пассажир, его собственная анимация выключена). Защита от регрессии:
    /// <see cref="Rendering.CameraController.ToggleSelectedOpenables"/> обязан
    /// найти хост-посудомойку по фасаду и открыть именно её.</summary>
    [Test]
    public void AttachedFacade_HotkeyGoesThroughDishwasher()
    {
        var dw = Make("DW-hotkey");
        var facade = MakeFacadeFor(dw, "DW_hotkey_front");
        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);

        // Имитируем выбор фасада + «E»: путь один и тот же, что в
        // CameraController.ToggleSelectedOpenables — открыть хост, не фасад.
        var host = KitchenDesigner.Core.UI.ContextMenuUI.FindDishwasherForFacade(facade);
        Assert.IsNotNull(host, "фасад обязан находиться среди хостов посудомойки");
        host!.ToggleOpen();

        Assert.IsTrue(dw.IsOpen, "посудомойка открыта — хост-ветка отработала");
        Assert.IsTrue(facade.IsOpen, "состояние фасада синхронизировано с хостом");
        Assert.AreEqual(0f, facade.DoorProgress, 0.001f,
            "фасад-пассажир не крутит собственную анимацию");
    }

    /// <summary>При перетаскивании фасада ручками <c>ValidationPosition</c>
    /// должна идти за <c>transform.position</c> — иначе валидация/коллизия
    /// «залипает» на старой <c>_closedPos</c> и считает фасад там, где его
    /// давно нет. Это было критично для посудомойки: пользователь тащит фасад,
    /// а SceneAnalyzer/ConstraintValidator ругаются на старый зазор с соседом.
    /// Проверяется через <see cref="FacadeElement.GetOpenBounds"/>: для пассажира
    /// он строится от текущего трансформа, а не от <c>_closedPos</c>.</summary>
    [Test]
    public void AttachedFacade_ValidationFollowsTransform()
    {
        var dw = Make("DW-move");
        var facade = MakeFacadeFor(dw, "DW_move_front");
        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);
        dw.ApplyDoorPose();

        Assert.IsTrue(facade.IsPassenger, "фасад — пассажир посудомойки");

        var (beforeMin, beforeMax) = facade.GetOpenBounds(0f);
        var beforeCenterX = (beforeMin.x + beforeMax.x) * 0.5f;

        // Имитируем перетаскивание ручкой: пользователь сдвинул фасад.
        var delta = new Vector3(0.3f, 0f, 0f);
        facade.transform.position += delta;

        var (afterMin, afterMax) = facade.GetOpenBounds(0f);
        var afterCenterX = (afterMin.x + afterMax.x) * 0.5f;
        Assert.AreEqual(delta.x, afterCenterX - beforeCenterX, 1e-4f,
            "центр AABB фасада (GetOpenBounds) сдвинулся ровно на дельту — " +
            "валидация следует за transform, а не за _closedPos");

        // Round-trip «закрыт → открыт → закрыт» с НОВОГО места: опорная точка
        // петли дверцы теперь там, куда тащили фасад, не на исходной _closedPos.
        dw.SetOpen(true);
        dw.ApplyDoorPose();
        var openedPos = facade.transform.position;
        Assert.AreNotEqual(facade.transform.position - delta, openedPos,
            "на открытой дверце фасад сдвинулся петлёй — он едет от новой опорной точки");

        dw.SetOpen(false);
        dw.ApplyDoorPose();
        // Опорная точка петли — там, куда тащили: фасад вернулся в newPos.
        Assert.AreEqual(beforeCenterX + delta.x,
            (facade.GetOpenBounds(0f).min.x + facade.GetOpenBounds(0f).max.x) * 0.5f,
            1e-4f,
            "закрытие возвращает фасад в newPos, откуда его тащили");
    }

    // ── Полый бак и ниша цоколя ─────────────────────────────────────────

    /// <summary>Габарит (центр, размер) в мм по вершинам ВАЛИДАЦИИ — то, чем
    /// машина участвует в коллизиях и прилипании.</summary>
    private static (Vector3 center, Vector3 size) ValidationBoxMM(DishwasherElement dw)
    {
        float toU = AppConstants.MM_TO_UNITS;
        var v = dw.GetVertices();
        var min = v[0];
        var max = v[0];
        foreach (var p in v) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        return ((min + max) * 0.5f / toU, (max - min) / toU);
    }

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

    /// <summary>Бак — 598 × 725 × 550 и стоит НА основании. Пять стенок
    /// обязаны сложиться ровно в этот габарит и оставить внутри пустоту.</summary>
    [Test]
    public void Body_IsAHollowBoxAboveTheBase()
    {
        var dw = Make();

        var walls = new[] { "BodyBottom", "BodyTop", "BodyLeft", "BodyRight", "BodyBack" };
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var wall in walls)
        {
            var (c, s) = BoxMM(dw, wall);
            min = Vector3.Min(min, c - s * 0.5f);
            max = Vector3.Max(max, c + s * 0.5f);
        }

        Assert.AreEqual(598f, max.x - min.x, 0.01f, "ширина бака");
        Assert.AreEqual(725f, max.y - min.y, 0.01f, "высота бака = 815 − 90");
        Assert.AreEqual(407.5f, max.y, 0.01f, "верх бака — верх габарита");
        Assert.AreEqual(-407.5f + 90f, min.y, 0.01f, "низ бака — верх основания");
        // Передний проём отдан дверце, поэтому стенки не доходят до переда.
        Assert.AreEqual(-275f, min.z, 0.01f);
        Assert.AreEqual(275f - DishwasherElement.DOOR_THICKNESS_MM, max.z, 0.01f);

        // Внутри пусто: ни одна стенка не заходит в камеру.
        float t = DishwasherElement.BODY_WALL_MM;
        foreach (var wall in walls)
        {
            var (c, s) = BoxMM(dw, wall);
            bool insideX = c.x - s.x * 0.5f > min.x + t - 0.01f && c.x + s.x * 0.5f < max.x - t + 0.01f;
            bool insideY = c.y - s.y * 0.5f > min.y + t - 0.01f && c.y + s.y * 0.5f < max.y - t + 0.01f;
            bool insideZ = c.z - s.z * 0.5f > min.z + t - 0.01f && c.z + s.z * 0.5f < max.z - t + 0.01f;
            Assert.IsFalse(insideX && insideY && insideZ, wall + " стоит в камере — короб не полый");
        }
    }

    [Test]
    public void ValidationVolume_IsTheTankWithoutTheBase()
    {
        var dw = Make();
        var (center, size) = ValidationBoxMM(dw);

        Assert.AreEqual(598f, size.x, 0.01f);
        Assert.AreEqual(725f, size.y, 0.01f, "в коллизии идёт бак, а не габарит 815");
        Assert.AreEqual(550f, size.z, 0.01f);
        Assert.AreEqual(0f, center.x, 0.01f);
        Assert.AreEqual(45f, center.y, 0.01f, "бак поднят на полвысоты основания");
        Assert.AreEqual(0f, center.z, 0.01f);
    }

    // ── Опора под прибором (DWH-05) ─────────────────────────────────────
    //
    // ИСХОДНЫЙ СИМПТОМ: посудомойка проваливалась в пол без единой ошибки.
    // Объём валидации начинается на 90 мм выше подошвы (цокольная полоса отдана
    // мебели), поэтому ни COL-01, ни COL-02 низ прибора не видят вовсе.

    /// <summary>Пол под подошвой: верх плиты пола совпадает с низом прибора.</summary>
    private KitchenElement FloorUnder(DishwasherElement dw, int thicknessMM = 100)
    {
        float toU = AppConstants.MM_TO_UNITS;
        float topMM = dw.SoleCenterWorld.y / toU;
        var go = ElementFactory.CreateFloor(new Vector3Int(4000, thicknessMM, 4000), "Pol",
            new Vector3(0f, (topMM - thicknessMM * 0.5f) * toU, 0f));
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    [Test]
    public void Dishwasher_StandingOnTheFloor_HasNoSupportIssue()
    {
        var dw = Make("Posudomoyka");
        FloorUnder(dw);

        CollectionAssert.IsEmpty(IssuesWithCode("DWH-05"),
            "машина стоит на полу — опора есть");
    }

    [Test]
    public void Dishwasher_WithNothingUnderneath_FiresDwh05()
    {
        var dw = Make("Posudomoyka");

        var issues = IssuesWithCode("DWH-05");
        Assert.IsNotEmpty(issues, "под подошвой пусто — прибору не на чем стоять");
        Assert.AreEqual(IssueLevel.Error, issues[0].Level, "это ошибка, а не предупреждение");
        Assert.AreSame(dw, issues[0].Target);
        StringAssert.Contains("не на чем стоять", issues[0].Message);
    }

    /// <summary>Ровно то, на что жаловались: машину утопили в пол — и тишина.
    /// Подошва ушла ВНУТРЬ плиты пола, это обязано быть ошибкой.</summary>
    [Test]
    public void Dishwasher_SunkIntoTheFloor_FiresDwh05()
    {
        var dw = Make("Posudomoyka");
        var floor = FloorUnder(dw);

        // Опускаем прибор на 40 мм — подошва внутри плиты.
        dw.transform.position += new Vector3(0f, -40f * AppConstants.MM_TO_UNITS, 0f);

        var issues = IssuesWithCode("DWH-05");
        Assert.IsNotEmpty(issues, "прибор провалился в пол");
        Assert.AreSame(floor, issues[0].Secondary, "виновник — та деталь, в которую утоплен");
        StringAssert.Contains("утоплена", issues[0].Message);
        StringAssert.Contains("40", issues[0].Message, "и на сколько именно");
    }

    /// <summary>Ножки регулируемые: паспортные 815–875 набираются именно ими, а
    /// в модели их нет. Значит корпус вправе висеть над полом на их ход — ровно
    /// так и стоит машина в docs/example.save.json: верх подведён под столешницу
    /// 820, корпус на 4.5 мм выше пола, ножки выкручены.</summary>
    [Test]
    public void Dishwasher_RaisedWithinTheFeetTravel_IsStillSupported()
    {
        var dw = Make("Posudomoyka");
        FloorUnder(dw);
        float toU = AppConstants.MM_TO_UNITS;

        dw.transform.position += new Vector3(0f, 4.5f * toU, 0f);
        CollectionAssert.IsEmpty(IssuesWithCode("DWH-05"), "4.5 мм — ножки выкручены на 4.5");

        dw.transform.position += new Vector3(0f, (DishwasherElement.FEET_ADJUST_MM - 4.5f) * toU, 0f);
        CollectionAssert.IsEmpty(IssuesWithCode("DWH-05"), "60 мм — ножки выкручены до упора");

        dw.transform.position += new Vector3(0f, 10f * toU, 0f);
        Assert.IsNotEmpty(IssuesWithCode("DWH-05"), "70 мм — ножки столько не дают, машина висит");
    }

    /// <summary>Опорой считается ЛЮБАЯ деталь под подошвой — пол, цоколь,
    /// подставка. Пользователь вправе собирать это чем угодно.</summary>
    [Test]
    public void Dishwasher_OnASupportBoard_IsSupported()
    {
        var dw = Make("Posudomoyka");
        // Доска 20мм ровно под подошвой (низ габарита −407.5).
        Board("Podstavka", new Vector3(0f, -407.5f - 10f, 0f), new Vector3Int(598, 20, 550));

        CollectionAssert.IsEmpty(IssuesWithCode("DWH-05"),
            "деталь под низом — законная опора, не обязательно пол");
    }

    /// <summary>Сосед СБОКУ опорой не является: он касается прибора, но под ним
    /// не стоит. Иначе «не на чем стоять» гасил бы любой шкаф рядом.</summary>
    [Test]
    public void Dishwasher_NeighbourBeside_IsNotSupport()
    {
        var dw = Make("Posudomoyka");
        // Боковина вплотную справа, во всю высоту — под прибором её нет.
        Board("side_R", new Vector3(308f, 0f, 0f), new Vector3Int(18, 815, 550));

        Assert.IsNotEmpty(IssuesWithCode("DWH-05"),
            "касание боком — не опора");
    }

    /// <summary>Мебельный цоколь стоит В НИШЕ, ПЕРЕД основанием, а не под ним —
    /// опорой он не считается, и «утопленной» машину не делает. Ровно ради
    /// этого основание и утоплено на 100 мм.</summary>
    [Test]
    public void PlinthInFrontOfTheBase_IsNeitherSupportNorCollision()
    {
        var dw = Make("Posudomoyka");
        FloorUnder(dw);
        // Цоколь 89мм в передней полосе: от подошвы вверх, перед основанием.
        Board("Cokol", new Vector3(0f, -407.5f + 44.5f, 275f - 50f), new Vector3Int(598, 89, 100));

        CollectionAssert.IsEmpty(IssuesWithCode("DWH-05"),
            "опору даёт пол; цоколь перед основанием ничего не ломает");
        var partners = OverlapPartners(dw);
        CollectionAssert.IsEmpty(partners,
            "цоколь в нише — не пересечение: " + string.Join(", ", partners.ConvertAll(p => p.PartName)));
    }

    /// <summary>Примерка в другую позицию обязана давать то же самое, что
    /// настоящий переезд, — иначе снэп и валидация разойдутся.</summary>
    [Test]
    public void ValidationVolume_FollowsAHypotheticalPosition()
    {
        var dw = Make();
        var probe = new Vector3(1.5f, 0.4f, -2f);

        var tried = dw.GetVerticesAt(probe);
        dw.transform.position = probe;
        var actual = dw.GetVertices();

        for (int i = 0; i < 8; i++)
            Assert.AreEqual(0f, (tried[i] - actual[i]).magnitude, 1e-5f, "вершина " + i);
    }

    /// <summary>ИСХОДНЫЙ СИМПТОМ: COL-01 «Leg ↔ Posudomoyka». Ножка стоит в
    /// нише цоколя — там, где у настоящей машины пустота, — и пересечением
    /// это быть не может.</summary>
    [Test]
    public void LegInThePlinthNiche_ProducesNoOverlap()
    {
        var dw = Make("Posudomoyka");
        // Ножка целиком в нижней полосе габарита (низ −407.5, ниша до −318.5).
        Board("Leg", new Vector3(0f, -407.5f + 44.5f, 200f), new Vector3Int(50, 89, 50));

        var partners = OverlapPartners(dw);

        CollectionAssert.IsEmpty(partners,
            "машина «наезжает» на: " + string.Join(", ", partners.ConvertAll(p => p.PartName)));
    }

    /// <summary>А НАСТОЯЩЕЕ пересечение бака ловиться обязано — иначе «нет
    /// коллизий» означало бы «проверка не работает».</summary>
    [Test]
    public void BoardInsideTheTank_IsStillAnOverlap()
    {
        var dw = Make("Posudomoyka");
        Board("intruder", new Vector3(0f, 44.5f, 0f), new Vector3Int(300, 300, 300));

        var partners = OverlapPartners(dw);

        Assert.AreEqual(1, partners.Count, "деталь в баке машины — это COL-01");
        Assert.AreEqual("intruder", partners[0].PartName);
    }

    [Test]
    public void Collider_CoversTheWholeBox_WhileValidationDoesNot()
    {
        var dw = Make();
        float toU = AppConstants.MM_TO_UNITS;
        var box = dw.GetComponent<BoxCollider>();

        Assert.AreEqual(815f, box!.size.y / toU, 0.01f, "клик по нише цоколя обязан выделять машину");
        Assert.AreEqual(725f, ValidationBoxMM(dw).size.y, 0.01f);
    }

    // ── Дверца ──────────────────────────────────────────────────────────

    private static Transform Child(DishwasherElement dw, string name)
    {
        var t = dw.transform.Find(name);
        Assert.IsNotNull(t, "нет дочерней коробки «" + name + "»");
        return t!;
    }

    [Test]
    public void Door_IsClosedByDefault()
    {
        var dw = Make();

        Assert.IsFalse(dw.IsOpen);
        Assert.AreEqual(0f, dw.DoorProgress, 1e-4f);
        Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity,
            Child(dw, "Door").localRotation), 0.01f);
    }

    /// <summary>Откидная дверца: поворот вокруг НИЖНЕЙ кромки на 90°. Верх
    /// уезжает вперёд на всю высоту двери и опускается к петле.</summary>
    [Test]
    public void Door_DropsDownAroundItsBottomEdge()
    {
        var dw = Make();
        float toU = AppConstants.MM_TO_UNITS;

        Vector3 Edge(Transform d, float sign) => d.localPosition + d.localRotation * new Vector3(
            0f,
            sign * DishwasherElement.TANK_HEIGHT_MM * 0.5f * toU,
            -DishwasherElement.DOOR_THICKNESS_MM * 0.5f * toU);

        var closedHinge = Edge(Child(dw, "Door"), -1f);
        Assert.AreEqual(0f, (closedHinge - DishwasherElement.HingeLocalMM * toU).magnitude, 1e-5f,
            "закрытая дверца стоит на своей же оси петли");

        dw.SetOpen(true);
        dw.StepDoor(10f);

        Assert.IsTrue(dw.IsOpen);
        Assert.AreEqual(1f, dw.DoorProgress, 1e-4f);

        var door = Child(dw, "Door");
        Assert.AreEqual(90f, Quaternion.Angle(Quaternion.identity, door.localRotation), 0.01f,
            "дверца раскрыта ровно в горизонталь");
        Assert.AreEqual(0f, (Edge(door, -1f) - closedHinge).magnitude, 1e-5f,
            "нижняя кромка не сдвинулась");

        var openTop = Edge(door, 1f);
        Assert.AreEqual(closedHinge.z / toU + DishwasherElement.TANK_HEIGHT_MM, openTop.z / toU, 0.01f,
            "верх дверцы вынесло вперёд на её высоту");
        Assert.AreEqual(closedHinge.y, openTop.y, 1e-5f, "и опустился на уровень петли");
    }

    [Test]
    public void Door_ClosesBack()
    {
        var dw = Make();
        dw.SetOpen(true);
        dw.StepDoor(10f);

        dw.SetOpen(false);
        dw.StepDoor(10f);

        Assert.IsFalse(dw.IsOpen);
        Assert.AreEqual(0f, dw.DoorProgress, 1e-4f);
        Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity,
            Child(dw, "Door").localRotation), 0.01f);
    }

    [Test]
    public void ForceClose_SlamsTheDoorInstantly()
    {
        var dw = Make();
        dw.SetOpen(true);
        dw.StepDoor(10f);

        dw.ForceClose();

        Assert.IsFalse(dw.IsOpen);
        Assert.AreEqual(0f, dw.DoorProgress, 1e-4f);
    }

    /// <summary>Открывание — транзитная анимация: корень не двигается вовсе,
    /// поэтому откинутая дверца не может породить COL-01 (её в объёме
    /// валидации нет и в закрытом виде).</summary>
    [Test]
    public void OpenDoor_ProducesNoOverlap()
    {
        var dw = Make("Posudomoyka");
        Board("side_L", new Vector3(-308f, 44.5f, 0f), new Vector3Int(18, 726, 550));
        Board("side_R", new Vector3(308f, 44.5f, 0f), new Vector3Int(18, 726, 550));
        var before = dw.GetVertices();

        dw.SetOpen(true);
        dw.StepDoor(10f);

        var partners = OverlapPartners(dw);
        CollectionAssert.IsEmpty(partners,
            "откинутая дверца пересекается с: "
            + string.Join(", ", partners.ConvertAll(p => p.PartName)));

        var after = dw.GetVertices();
        for (int i = 0; i < 8; i++)
            Assert.AreEqual(0f, (before[i] - after[i]).magnitude, 1e-6f,
                "поза валидации при открывании не двигается");
    }

    /// <summary>Фасад прикручен к дверце и обязан откидываться ВМЕСТЕ с ней —
    /// ровно так же, как фасад ящика выезжает вместе с коробом.</summary>
    [Test]
    public void AttachedFacade_OpensAndClosesWithTheDoor()
    {
        var dw = Make("DW-open");
        var facade = MakeFacadeAtGap(dw, "DW_open_front", 2f);
        facade.Mode = DoorMode.HingeFrontBottom;
        dw.AttachedFacadeName = facade.PartName;

        dw.SetOpen(true);
        Assert.IsTrue(facade.IsOpen, "фасад откидывается вместе с дверцей");

        dw.SetOpen(false);
        Assert.IsFalse(facade.IsOpen);
    }

    [Test]
    public void ForceClose_TakesTheFacadeWithIt()
    {
        var dw = Make("DW-slam");
        var facade = MakeFacadeAtGap(dw, "DW_slam_front", 2f);
        facade.Mode = DoorMode.HingeFrontBottom;
        dw.AttachedFacadeName = facade.PartName;
        dw.SetOpen(true);
        dw.StepDoor(10f);

        dw.ForceClose();

        Assert.IsFalse(dw.IsOpen);
        Assert.IsFalse(facade.IsOpen);
    }

    [Test]
    public void OpenDoor_SurvivesSaveLoadRoundTrip()
    {
        var dw = Make("DW-rt-open");
        var facade = MakeFacadeAtGap(dw, "DW_rt_open_front", 2f);
        facade.Mode = DoorMode.HingeFrontBottom;
        dw.AttachedFacadeName = facade.PartName;
        dw.SetOpen(true);
        dw.StepDoor(10f);

        var path = Path.Combine(Application.temporaryCachePath,
            $"rt_dwdoor_{System.Guid.NewGuid():N}.json");
        SaveLoadManager.SaveToFile(path,
            SaveLoadManager.CaptureScene(new List<KitchenElement> { dw, facade }));

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        var loaded = SaveLoadManager.LoadFromFile(path);
        Assert.IsNotNull(loaded);
        SaveLoadManager.RestoreScene(loaded!);
        File.Delete(path);

        var restored = Object.FindFirstObjectByType<DishwasherElement>();
        Assert.IsNotNull(restored);
        Assert.IsTrue(restored!.IsOpen, "откинутая дверца обязана пережить сохранение");
        Assert.AreEqual("DW_rt_open_front", restored.AttachedFacadeName,
            "и привязка фасада вместе с ней");
        Assert.IsNotNull(restored.FindAttachedFacade());
        Assert.IsTrue(restored.FindAttachedFacade()!.IsOpen, "фасад восстановился откинутым");
    }

    [Test]
    public void Mcp_OpensAndClosesTheDishwasherDoor()
    {
        var dw = Make("DW-door-mcp");

        var open = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "DW-door-mcp", is_open = true } }
        }));
        Assert.AreEqual("result", open.type, open.type == "error" ? ErrorMessage(open) : "");
        Assert.IsTrue(dw.IsOpen);

        var info = _handler.Handle(MakeReq("get_elements", new { filter = "DW-door-mcp" }));
        var dish = Payload(info)["elements"]![0]!["dishwasher"]!;
        Assert.IsTrue(dish["isOpen"]!.Value<bool>());
        Assert.AreEqual(89, dish["plinthNicheMM"]!.Value<int>());
        Assert.AreEqual(90, dish["baseHeightMM"]!.Value<int>(), "собственное основание прибора");
        Assert.AreEqual(100, dish["baseSetbackMM"]!.Value<int>(), "утопление основания вглубь");
        Assert.AreEqual(5f, dish["facadeMountGapMM"]!.Value<float>(), 1e-4f);

        _handler.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "DW-door-mcp", is_open = false } }
        }));
        Assert.IsFalse(dw.IsOpen);
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

    private static TMPro.TMP_Dropdown FacadeDropdown(Transform panel)
    {
        foreach (var dd in panel.GetComponentsInChildren<TMPro.TMP_Dropdown>(true))
            if (dd.name.Contains("Фасад") || dd.name.Contains("Facade")) return dd;
        foreach (var dd in panel.GetComponentsInChildren<TMPro.TMP_Dropdown>(true))
            foreach (var o in dd.options)
                if (o.text == "(нет фасада)") return dd;
        Assert.Fail("в окне свойств нет списка фасадов");
        return null!;
    }

    /// <summary>Кнопка «Открыть дверцу» — по образцу духовки: видна только у
    /// машины и переключает подпись по состоянию.</summary>
    [Test]
    public void ContextMenu_ShowsTheDoorButtonForTheDishwasherOnly()
    {
        var panel = BuildMenu();
        var dw = Make("DW-ui");
        var boardGo = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Board", Vector3.zero);
        _spawned.Add(boardGo);

        _menu!.Open(dw);
        var button = panel.Find("CtxDishwasherDoor");
        Assert.IsNotNull(button, "у машины должна быть кнопка открывания");
        Assert.IsTrue(button!.gameObject.activeSelf);
        Assert.AreEqual("Открыть дверцу",
            button.GetComponentInChildren<TMPro.TMP_Text>().text);

        dw.SetOpen(true);
        _menu!.Open(dw);
        Assert.AreEqual("Закрыть дверцу",
            button.GetComponentInChildren<TMPro.TMP_Text>().text);

        _menu!.Open(boardGo.GetComponent<KitchenElement>());
        Assert.IsFalse(button.gameObject.activeSelf, "обычной детали дверца не положена");
    }

    /// <summary>Кнопка «Открыть» у фасада-пассажира ведёт МАШИНУ: подпись
    /// приходит от хоста (IOpenable.OpenActionLabel), а клик открывает его,
    /// а не собственную петлю фасада. Раньше эти две ветки жили копиями в
    /// ContextMenuUI и CameraController и умели разойтись.</summary>
    [Test]
    public void ContextMenu_FacadeOnDishwasher_OpenButtonDrivesTheDishwasher()
    {
        var panel = BuildMenu();
        var dw = Make("DW-facade-btn");
        var facade = MakeFacadeFor(dw, "DW_facade_btn_front");
        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);

        _menu!.Open(facade);
        var button = panel.Find("CtxDoor");
        Assert.IsNotNull(button, "у фасада есть кнопка открывания");
        var label = button!.GetComponentInChildren<TMPro.TMP_Text>(true);
        Assert.AreEqual("Открыть дверцу", label.text,
            "подпись берётся у ХОСТА: у машины это «дверца», а не «Открыть» фасада");

        button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Assert.IsTrue(dw.IsOpen, "клик открывает машину, а не фасад-пассажир");
        Assert.AreEqual("Закрыть дверцу", label.text, "подпись переехала вслед за хостом");
    }

    /// <summary>Окно свойств фасада-пассажира остаётся ОТКРЫТЫМ и показывает
    /// строку «Дверца». Прежний UpdateModeDropdownEnabled гасил
    /// <c>_modeDropdown.transform.parent</c>, а родитель дропдауна — сама
    /// панель: строка так никогда и не пряталась, зато метод мог погасить
    /// всё окно. Тест краснеет, если кто-то снова начнёт гасить панель.</summary>
    [Test]
    public void ContextMenu_FacadeOnDishwasher_PanelAndHingeModeRowStayVisible()
    {
        var panel = BuildMenu();
        var dw = Make("DW-mode-row");
        var facade = MakeFacadeFor(dw, "DW_mode_row_front");
        dw.AttachedFacadeName = facade.PartName;
        dw.OnAttachedFacadeChanged(null, facade);

        _menu!.Open(facade);

        Assert.IsTrue(panel.gameObject.activeSelf, "окно свойств обязано остаться открытым");
        var mode = panel.Find("CtxMode");
        Assert.IsNotNull(mode, "строка «Дверца» существует");
        Assert.IsTrue(mode!.gameObject.activeSelf,
            "строка видна: у фасада есть фасет Facade, и решает видимость раскладка");
    }
}
