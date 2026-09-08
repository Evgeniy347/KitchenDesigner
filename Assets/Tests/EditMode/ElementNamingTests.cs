using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Имена элементов: допустимый алфавит (^[A-Za-z0-9_-]+$) и глобальная
/// уникальность. Имя — ключ связей (пара ящика, фасад ящика, стена окна/двери),
/// поэтому отдельный блок тестов стережёт перенос ссылок при загрузке проекта
/// со старыми (кириллическими и дублирующимися) именами.
/// </summary>
public class ElementNamingTests : ElementTestBase
{
    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    private KitchenElement MakePart(string name)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), name, Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    // ── Алфавит ─────────────────────────────────────────────────────────

    [Test]
    public void IsValid_AcceptsLatinDigitsDashUnderscore()
    {
        Assert.IsTrue(ElementNaming.IsValid("B4_upper-2"));
        Assert.IsTrue(ElementNaming.IsValid("Board800x400"));
    }

    [Test]
    public void IsValid_RejectsSpacesCyrillicAndPunctuation()
    {
        Assert.IsFalse(ElementNaming.IsValid("Фасад"), "кириллица");
        Assert.IsFalse(ElementNaming.IsValid("Board 800"), "пробел");
        Assert.IsFalse(ElementNaming.IsValid("Board(copy)"), "скобки");
        Assert.IsFalse(ElementNaming.IsValid("Board.1"), "точка");
        Assert.IsFalse(ElementNaming.IsValid(""), "пустое");
        Assert.IsFalse(ElementNaming.IsValid(null), "null");
    }

    [Test]
    public void Sanitize_TransliteratesCyrillic()
    {
        Assert.AreEqual("Fasad_600x400", ElementNaming.Sanitize("Фасад_600x400"));
        Assert.AreEqual("Yaschik_GTV", ElementNaming.Sanitize("Ящик GTV"));
        Assert.AreEqual("Dver", ElementNaming.Sanitize("Дверь"), "мягкий знак исчезает, «_» за него не ставим");
    }

    [Test]
    public void Sanitize_CollapsesAndTrimsSeparators()
    {
        Assert.AreEqual("a_b", ElementNaming.Sanitize("a   b"), "повторы схлопываются");
        Assert.AreEqual("Board", ElementNaming.Sanitize("  Board  "), "края обрезаются");
        Assert.AreEqual(ElementNaming.Fallback, ElementNaming.Sanitize("   "), "нечего оставить");
        Assert.AreEqual(ElementNaming.Fallback, ElementNaming.Sanitize(null));
    }

    // ── Уникальность ────────────────────────────────────────────────────

    [Test]
    public void Normalize_AppendsSuffixStartingFromOne()
    {
        MakePart("Board");
        Assert.AreEqual("Board_1", ElementNaming.Normalize("Board"));

        MakePart("Board_1");
        Assert.AreEqual("Board_2", ElementNaming.Normalize("Board"));
    }

    [Test]
    public void Normalize_IsCaseInsensitive()
    {
        MakePart("Board");
        Assert.AreEqual("board_1", ElementNaming.Normalize("board"));
    }

    [Test]
    public void Normalize_IgnoresOwnNameWhenExceptGiven()
    {
        var el = MakePart("Board");
        Assert.AreEqual("Board", ElementNaming.Normalize("Board", el),
            "переименование самого себя в то же имя не должно давать «Board_1»");
    }

    [Test]
    public void Factory_MakesEveryCreatedNameUnique()
    {
        var a = MakePart("Полка");
        var b = MakePart("Полка");
        var c = MakePart("Полка");

        Assert.AreEqual("Polka", a.PartName);
        Assert.AreEqual("Polka_1", b.PartName);
        Assert.AreEqual("Polka_2", c.PartName);
    }

    [Test]
    public void Duplicate_KeepsBaseNameAndGetsSuffix()
    {
        var src = MakePart("Shelf");
        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);

        Assert.AreEqual("Shelf_1", dupGo.GetComponent<KitchenElement>().PartName);
    }

    [Test]
    public void Duplicate_IncrementsTrailingNumberInsteadOfNesting()
    {
        var src = MakePart("Shelf_5");
        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);

        Assert.AreEqual("Shelf_6",
            dupGo.GetComponent<KitchenElement>().PartName,
            "клон Shelf_5 должен дать Shelf_6, а не Shelf_5_1");
    }

    [Test]
    public void Duplicate_SkipsTakenIncrementedNames()
    {
        MakePart("Shelf_5");
        MakePart("Shelf_6");

        var src = PartRegistry.GetAll().Find(e => e.PartName == "Shelf_5");
        Assert.IsNotNull(src);
        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);

        Assert.AreEqual("Shelf_7",
            dupGo.GetComponent<KitchenElement>().PartName,
            "Shelf_6 занято — должно дать Shelf_7");
    }

    [Test]
    public void Duplicate_FromNestedName_UnnestsOneLevel()
    {
        var src = MakePart("Polka_1_1");
        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);

        Assert.AreEqual("Polka_1_2",
            dupGo.GetComponent<KitchenElement>().PartName,
            "Polka_1_1 → base=Polka_1, num=1 → Polka_1_2");
    }

    // ── Дубликат: материал ─────────────────────────────────────────────

    private FacadeElement MakeFacade(string name) =>
        MakeFactoryFacade(name, new Vector3Int(600, 400, 18), Vector3.zero);

    [Test]
    public void Duplicate_Part_PreservesMaterial()
    {
        var src = MakePart("Shelf");
        MaterialManager.ApplyById(src, "oak");
        Assert.AreEqual("oak", src.MaterialId);

        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);
        var dup = dupGo.GetComponent<KitchenElement>();

        Assert.AreEqual("oak", dup.MaterialId,
            "дубликат детали должен сохранять материал оригинала");
    }

    [Test]
    public void Duplicate_FacadeElement_PreservesMaterial()
    {
        var src = MakeFacade("Front");
        MaterialManager.ApplyById(src, "oak");
        Assert.AreEqual("oak", src.MaterialId);

        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);
        var dup = dupGo.GetComponent<KitchenElement>();

        Assert.AreEqual("oak", dup.MaterialId,
            "дубликат фасада должен сохранять материал оригинала");
    }

    [Test]
    public void Duplicate_FacadeElement_PreservesGaps()
    {
        var src = MakeFacade("Front");
        // CreateFacade defaults are 2,2,2,2 — change to something non‑default.
        src.GapLeft = 3;
        src.GapRight = 4;
        src.GapTop = 5;
        src.GapBottom = 6;

        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);
        var dup = dupGo.GetComponent<FacadeElement>();

        Assert.AreEqual(3, dup.GapLeft);
        Assert.AreEqual(4, dup.GapRight);
        Assert.AreEqual(5, dup.GapTop);
        Assert.AreEqual(6, dup.GapBottom);
    }

    // ── Загрузка проекта ────────────────────────────────────────────────

    private static ProjectData ProjectOf(params ElementData[] items)
    {
        var data = new ProjectData(new List<ElementData>(items));
        data.basePlateValid = false;
        return data;
    }

    private static ElementData Board(string name) => new ElementData
    {
        name = name,
        dimensionsMM = new[] { 800, 400, 18 },
        position = new[] { 0f, 0f, 0f },
        rotation = new[] { 0f, 0f, 0f, 1f },
    };

    [Test]
    public void RestoreScene_SanitizesNamesAndResolvesCollisions()
    {
        var mgr = new SaveLoadManagerInstance();
        var created = mgr.RestoreScene(ProjectOf(Board("Фасад 600"), Board("Фасад 600")));
        foreach (var go in created) _spawned.Add(go);

        Assert.AreEqual(2, created.Count);
        Assert.AreEqual("Fasad_600", created[0].GetComponent<KitchenElement>().PartName);
        Assert.AreEqual("Fasad_601", created[1].GetComponent<KitchenElement>().PartName);
    }

    /// <summary>Ключевой инвариант: имя — ключ связи, поэтому чистка имён при
    /// загрузке обязана увести за собой ссылку ящика на фасад.</summary>
    [Test]
    public void RestoreScene_RemapsDrawerToFacadeLink()
    {
        var facade = Board("Фасад 600");
        facade.isFacade = true;

        var drawer = Board("Ящик 1");
        drawer.isDrawer = true;
        drawer.drawerAttachedFacadeName = "Фасад 600";

        var mgr = new SaveLoadManagerInstance();
        var created = mgr.RestoreScene(ProjectOf(facade, drawer));
        foreach (var go in created) _spawned.Add(go);

        var loadedFacade = created[0].GetComponent<KitchenElement>();
        var loadedDrawer = created[1].GetComponent<DrawerElement>();

        Assert.AreEqual("Fasad_600", loadedFacade.PartName);
        Assert.AreEqual("Fasad_600", loadedDrawer.AttachedFacadeName,
            "ссылка должна указывать на НОВОЕ имя фасада");
    }

    /// <summary>RestoreScene зовут и на неполном наборе, поэтому ссылку на
    /// элемент, которого в пачке нет, трогать нельзя — иначе частичное
    /// восстановление тихо рвало бы связи.</summary>
    [Test]
    public void RestoreScene_LeavesUnmappedLinkUntouched()
    {
        var drawer = Board("Ящик 1");
        drawer.isDrawer = true;
        drawer.drawerAttachedFacadeName = "MyFacade";

        var mgr = new SaveLoadManagerInstance();
        var created = mgr.RestoreScene(ProjectOf(drawer));
        foreach (var go in created) _spawned.Add(go);

        Assert.AreEqual("MyFacade", created[0].GetComponent<DrawerElement>().AttachedFacadeName);
    }
}
