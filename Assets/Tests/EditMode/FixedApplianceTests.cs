using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Встраиваемая техника с фиксированными габаритами: общий признак
/// <see cref="IFixedSizeElement"/> и первый прибор — индукционная варочная
/// поверхность Bosch Serie 4 PUE611BB5E (стекло 592×522, высота 51, ниша
/// врезки 560×490, docs/APPLIANCES-BRIEF.md §1).
///
/// Проверяется ровно то, что отличает готовую модель от свободного элемента:
/// размеры не поддаются правке НИ ОДНИМ путём, ручки ресайза не строятся,
/// модель переживает сохранение и дублирование, а общая «Варочная
/// поверхность» из группы «Техника» осталась полностью редактируемой.
/// </summary>
public class FixedApplianceTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

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

    private CooktopElement Make(string model, string name = "Hob")
    {
        var go = ElementFactory.CreateCooktop(name, Vector3.zero, model);
        _spawned.Add(go);
        return go.GetComponent<CooktopElement>();
    }

    // ── Габариты модели ─────────────────────────────────────────────────

    [Test]
    public void BoschPreset_HasManufacturerDimensions()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E);

        Assert.AreEqual(new Vector3Int(592, 51, 522), hob.DimensionsMM, "стекло 592×522, общая высота 51");
        Assert.AreEqual(560, hob.CutoutWidthMM, "ниша врезки по ширине");
        Assert.AreEqual(490, hob.CutoutDepthMM, "ниша врезки по глубине");
        Assert.AreEqual(51 - CooktopElement.RIM_HEIGHT_MM, hob.BodyHeightMM, "короб = общая высота − плита");
    }

    [Test]
    public void BoschPreset_IsFixedSize()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E);

        Assert.IsTrue(hob.HasFixedSize);
        Assert.IsTrue(FixedSize.IsFixed(hob), "общий признак техники видит пресет");
        Assert.AreEqual(CooktopElement.MODEL_BOSCH_PUE611BB5E, hob.Model);
    }

    [Test]
    public void FreeCooktop_IsNotFixedSize()
    {
        var hob = Make("");

        Assert.IsFalse(hob.HasFixedSize);
        Assert.IsFalse(FixedSize.IsFixed(hob), "«Варочная поверхность» из «Мебели» осталась свободной");
        Assert.AreEqual(new Vector3Int(
            CooktopElement.DEFAULT_WIDTH_MM, CooktopElement.DEFAULT_HEIGHT_MM, CooktopElement.DEFAULT_DEPTH_MM),
            hob.DimensionsMM);
    }

    [Test]
    public void UnknownModel_FallsBackToFreeCooktop()
    {
        var hob = Make("Bosch NOSUCHMODEL");

        Assert.IsFalse(hob.HasFixedSize, "неизвестную модель не за что запирать");
        Assert.AreEqual(CooktopElement.DEFAULT_WIDTH_MM, hob.WidthMM);
    }

    // ── Правка запрещена любым путём ────────────────────────────────────

    [Test]
    public void BoschPreset_ResizeIsIgnored()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E);

        hob.DimensionsMM = new Vector3Int(800, 120, 700);

        Assert.AreEqual(new Vector3Int(592, 51, 522), hob.DimensionsMM,
            "габарит производителя возвращается на место в ApplyDimensions");
    }

    [Test]
    public void BoschPreset_SingleAxisEditsAreIgnored()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E);

        hob.WidthMM = 900;
        hob.HeightMM = 200;
        hob.DepthMM = 400;

        Assert.AreEqual(592, hob.WidthMM);
        Assert.AreEqual(51, hob.HeightMM);
        Assert.AreEqual(522, hob.DepthMM);
    }

    [Test]
    public void BoschPreset_CutoutIsIgnored()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E);

        hob.CutoutWidthMM = 300;
        hob.CutoutDepthMM = 300;

        Assert.AreEqual(560, hob.CutoutWidthMM);
        Assert.AreEqual(490, hob.CutoutDepthMM);
    }

    [Test]
    public void FreeCooktop_StillResizes()
    {
        var hob = Make("");

        hob.DimensionsMM = new Vector3Int(700, 70, 560);
        hob.CutoutWidthMM = 600;

        Assert.AreEqual(new Vector3Int(700, 70, 560), hob.DimensionsMM, "свободная варочная тянется как раньше");
        Assert.AreEqual(600, hob.CutoutWidthMM);
    }

    [Test]
    public void FixedAppliance_HasNoResizeHandles()
    {
        var fixedHob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E, "Fixed");
        var freeHob = Make("", "Free");

        Assert.IsFalse(ResizeHandleManager.SupportsHandleResize(fixedHob), "мышь не обходит окно свойств");
        // Врезная техника не тянется за грань и без фиксированной модели —
        // проверка сторожит именно то, что признак ничего не сломал.
        Assert.IsFalse(ResizeHandleManager.SupportsHandleResize(freeHob));
    }

    // ── Геометрия рисунка ───────────────────────────────────────────────

    [Test]
    public void BoschPreset_DrawsFourBurnersAndControlPanel()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E);

        var names = new List<string>();
        foreach (Transform child in hob.transform)
            if (child.gameObject.activeSelf) names.Add(child.name);

        CollectionAssert.AreEquivalent(
            new[] { "Top", "Body", "Burner1", "Burner2", "Burner3", "Burner4", "Panel" }, names,
            "плита, короб, 4 конфорки и панель управления");
    }

    [Test]
    public void BoschPreset_BurnerDiametersMatchTheDatasheet()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E);
        float toU = AppConstants.MM_TO_UNITS;

        var diameters = new List<int>();
        foreach (Transform child in hob.transform)
            if (child.name.StartsWith("Burner"))
                diameters.Add(Mathf.RoundToInt(child.localScale.x / toU));

        diameters.Sort();
        CollectionAssert.AreEqual(new[] { 145, 180, 180, 210 }, diameters);
    }

    [Test]
    public void BoschPreset_BurnersLieOnTheGlass()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E);
        float toU = AppConstants.MM_TO_UNITS;
        float halfW = 592 * 0.5f, halfD = 522 * 0.5f;

        foreach (Transform child in hob.transform)
        {
            if (!child.name.StartsWith("Burner") && child.name != "Panel") continue;
            float x = child.localPosition.x / toU, z = child.localPosition.z / toU;
            float rx = child.localScale.x * 0.5f / toU, rz = child.localScale.z * 0.5f / toU;
            Assert.LessOrEqual(Mathf.Abs(x) + rx, halfW, child.name + " не свисает со стекла по ширине");
            Assert.LessOrEqual(Mathf.Abs(z) + rz, halfD, child.name + " не свисает со стекла по глубине");
            Assert.Greater(child.localPosition.y, CooktopElement.RIM_HEIGHT_MM * toU * 0.99f,
                child.name + " лежит НА стекле, а не внутри плиты");
        }
    }

    [Test]
    public void RootScaleStaysUnit()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E);

        Assert.AreEqual(Vector3.one, hob.transform.localScale,
            "корень единичный — иначе дети масштабируются дважды");
    }

    // ── Каталог ─────────────────────────────────────────────────────────

    [Test]
    public void Catalog_ApplianceGroup_HasBoschCooktop()
    {
        var group = SidebarCatalog.Build().Find(g => g.title == "Техника");

        var item = group.items.Find(i => i.applianceModel == CooktopElement.MODEL_BOSCH_PUE611BB5E);
        Assert.IsTrue(item.kind == SidebarItemKind.Cooktop, "пункт «Техники» — варочная поверхность");
        Assert.AreEqual(new Vector3Int(592, 51, 522), item.dims, "в каталоге размеры производителя");
    }

    [Test]
    public void Catalog_ApplianceGroup_HasGenericCooktop()
    {
        var group = SidebarCatalog.Build().Find(g => g.title == "Техника");
        var item = group.items.Find(i => i.name == "Варочная поверхность");

        Assert.IsNotNull(item, "общая варочная переехала из «Мебели» в «Технику»");
        Assert.IsTrue(item.kind == SidebarItemKind.Cooktop, "общая варочная помечена как Cooktop");
        Assert.AreEqual("", item.applianceModel, "у общей варочной нет модели — модель только у Bosch-пункта");
    }

    // ── Сериализация ────────────────────────────────────────────────────

    [Test]
    public void BoschPreset_SurvivesSaveLoadRoundTrip()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E, "Hob1");
        hob.transform.position = new Vector3(1f, 0.9f, 2f);

        var path = Path.Combine(Application.temporaryCachePath, $"rt_hob_{System.Guid.NewGuid():N}.json");
        var saved = SaveLoadManager.CaptureScene(new List<KitchenElement> { hob });
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
        Assert.AreEqual(CooktopElement.MODEL_BOSCH_PUE611BB5E, restored!.Model, "модель пережила round-trip");
        Assert.IsTrue(restored.HasFixedSize, "и вместе с ней замок размеров");
        Assert.AreEqual(new Vector3Int(592, 51, 522), restored.DimensionsMM);
        Assert.AreEqual(560, restored.CutoutWidthMM);
        Assert.AreEqual(490, restored.CutoutDepthMM);
    }

    [Test]
    public void ElementData_CarriesModel()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E);

        var data = ElementCapture.FromElement(hob);

        Assert.IsTrue(data.isCooktop);
        Assert.AreEqual(CooktopElement.MODEL_BOSCH_PUE611BB5E, data.cooktopModel);
    }

    [Test]
    public void Duplicate_KeepsTheModel()
    {
        var hob = Make(CooktopElement.MODEL_BOSCH_PUE611BB5E, "Hob2");

        var copyGo = ElementFactory.Duplicate(hob);
        _spawned.Add(copyGo);
        var copy = copyGo.GetComponent<CooktopElement>();

        Assert.AreEqual(CooktopElement.MODEL_BOSCH_PUE611BB5E, copy.Model, "копия — тот же прибор");
        Assert.AreEqual(new Vector3Int(592, 51, 522), copy.DimensionsMM);
    }
}
