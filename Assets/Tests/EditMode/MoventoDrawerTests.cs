using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Ящик Movento: раскрой деревянного короба по формулам Blum, раскладка в
/// спецификацию отдельными деталями (в отличие от цельной строки GTV) и
/// сохранение системы выдвижения через сериализацию.
/// </summary>
public class MoventoDrawerTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        MaterialCatalog.RegisterDynamic(new MaterialDef("gtv_anthracite", "Антрацит (GTV)", "Металл", new Color(0.25f, 0.25f, 0.27f)));
        MaterialCatalog.RegisterDynamic(new MaterialDef("gtv_white", "Белый (GTV)", "Металл", new Color(0.92f, 0.92f, 0.90f)));
        MaterialCatalog.RegisterDynamic(new MaterialDef("gtv_black", "Чёрный (GTV)", "Металл", new Color(0.10f, 0.10f, 0.11f)));
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

    private DrawerElement MakeMovento(string name, DrawerType type, int nl, int lw)
    {
        var go = ElementFactory.CreateDrawer(type, nl, DrawerColor.Anthracite, lw, name, Vector3.zero, DrawerSystem.Movento);
        _spawned.Add(go);
        return go.GetComponent<DrawerElement>();
    }

    // ── 1. Формулы раскроя ──────────────────────────────────────────────

    [Test]
    public void ComputeParts_500x568_TypeA_MatchesBlumFormulas()
    {
        // LW = 568 (корпус 600, боковины 16), NL = 500, тип A (высота 86).
        var parts = MoventoDrawerMesh.ComputeParts(568, DrawerType.A, 500).ToList();
        Assert.AreEqual(5, parts.Count, "короб Movento = 5 деталей (2 боковины, перед, задник, дно)");

        var sides = parts.Where(p => p.suffix == MoventoDrawerMesh.SUFFIX_SIDE).ToList();
        Assert.AreEqual(2, sides.Count, "две боковины");
        // Боковина: длина NL−10 = 490, высота 86, толщина 16.
        Assert.AreEqual(new Vector3Int(490, 86, 16), sides[0].dimsMM, "боковина = (NL−10)×H×16");

        var front = parts.First(p => p.suffix == MoventoDrawerMesh.SUFFIX_FRONT);
        var back = parts.First(p => p.suffix == MoventoDrawerMesh.SUFFIX_BACK);
        // Перед/задник: ширина LW−74 = 494, высота 86−14−16 = 56 (стоят на дне), толщина 16.
        Assert.AreEqual(new Vector3Int(494, 56, 16), front.dimsMM, "перед = (LW−74)×(H−30)×16");
        Assert.AreEqual(new Vector3Int(494, 56, 16), back.dimsMM, "задник = (LW−74)×(H−30)×16");

        var bottom = parts.First(p => p.suffix == MoventoDrawerMesh.SUFFIX_BOTTOM);
        // Дно: ширина LW−74 = 494, глубина на всю боковину NL−10 = 490, толщина 16.
        Assert.AreEqual(new Vector3Int(494, 490, 16), bottom.dimsMM, "дно = (LW−74)×(NL−10)×16");
    }

    [Test]
    public void ComputeParts_HeightFollowsType()
    {
        int hC = DrawerConstants.GetTypeHeight(DrawerType.C);
        var parts = MoventoDrawerMesh.ComputeParts(568, DrawerType.C, 500).ToList();
        var side = parts.First(p => p.suffix == MoventoDrawerMesh.SUFFIX_SIDE);
        Assert.AreEqual(hC, side.dimsMM.y, "высота детали = высота типа");
    }

    [Test]
    public void ComputeBoxes_ReturnsFivePanels()
    {
        var boxes = MoventoDrawerMesh.ComputeBoxes(568, DrawerType.A, 500);
        Assert.AreEqual(5, boxes.Count, "меш короба Movento — 5 панелей");
    }

    [Test]
    public void ComputeParts_600x500_TypeD_MatchesReferenceCutList()
    {
        // Эталон конструктора Blum: проём LW = 600 «в свету», NL = 500,
        // тип D (200), ниша под скрытой направляющей 14 мм.
        var parts = MoventoDrawerMesh.ComputeParts(600, DrawerType.D, 500).ToList();

        var side = parts.First(p => p.suffix == MoventoDrawerMesh.SUFFIX_SIDE);
        Assert.AreEqual(new Vector3Int(490, 200, 16), side.dimsMM, "боковина = (NL−10)×H×16");

        // Перед и задник стоят НА дне: высота = H − ниша − толщина дна = 200−14−16.
        var front = parts.First(p => p.suffix == MoventoDrawerMesh.SUFFIX_FRONT);
        var back = parts.First(p => p.suffix == MoventoDrawerMesh.SUFFIX_BACK);
        Assert.AreEqual(new Vector3Int(526, 170, 16), front.dimsMM, "перед = (LW−74)×(H−30)×16");
        Assert.AreEqual(new Vector3Int(526, 170, 16), back.dimsMM, "задник = (LW−74)×(H−30)×16");

        // Дно идёт на всю длину боковины — перед и задник лежат на нём.
        var bottom = parts.First(p => p.suffix == MoventoDrawerMesh.SUFFIX_BOTTOM);
        Assert.AreEqual(new Vector3Int(526, 490, 16), bottom.dimsMM, "дно = (LW−74)×(NL−10)×16");
    }

    [Test]
    public void ComputeBoxes_BottomLiftedByNiche_PanelsStandOnBottom()
    {
        int lift = DrawerConstants.GetBottomLift(DrawerType.D);
        int niche = DrawerConstants.MOVENTO_BOTTOM_NICHE;
        int t = DrawerConstants.MOVENTO_BOARD_THICKNESS;
        var boxes = MoventoDrawerMesh.ComputeBoxes(600, DrawerType.D, 500);

        var side = boxes.First(b => b.name.StartsWith(MoventoDrawerMesh.SUFFIX_SIDE));
        var bottom = boxes.First(b => b.name == MoventoDrawerMesh.SUFFIX_BOTTOM);
        var front = boxes.First(b => b.name == MoventoDrawerMesh.SUFFIX_FRONT);
        var back = boxes.First(b => b.name == MoventoDrawerMesh.SUFFIX_BACK);

        // Боковина уходит до самого низа короба, дно приподнято над ней —
        // просвет между ними и есть ниша, туда встаёт скрытая направляющая.
        Assert.AreEqual(lift, side.minMM.y, 0.01f, "боковина — до низа короба");
        Assert.AreEqual(lift + niche, bottom.minMM.y, 0.01f, "дно приподнято на глубину ниши");
        Assert.AreEqual(lift + niche + t, front.minMM.y, 0.01f, "перед стоит на дне");
        Assert.AreEqual(lift + niche + t, back.minMM.y, 0.01f, "задник стоит на дне");

        // Верх у всех четырёх панелей общий — короб ровный сверху.
        Assert.AreEqual(side.minMM.y + side.sizeMM.y, front.minMM.y + front.sizeMM.y, 0.01f,
            "перед и боковина заканчиваются на одной высоте");
    }

    // ── 2. Спецификация: Movento раскладывается, GTV — нет ───────────────

    [Test]
    public void Spec_MoventoDrawer_DecomposesIntoParts()
    {
        var drawer = MakeMovento("ЯщикM", DrawerType.A, 500, 568);

        var result = SpecificationManager.Build(new List<KitchenElement> { drawer });

        // 3 строки: боковина ×2, перед+задник ×2 (одинаковый размер), дно ×1.
        Assert.AreEqual(3, result.lines.Count, "боковины / перед+задник / дно");
        Assert.AreEqual(5, result.totalCount, "всего 5 деталей");

        foreach (var line in result.lines)
            Assert.IsTrue(line.name.Contains("·"), $"деталь должна иметь суффикс: {line.name}");

        var bottom = result.lines.First(l => l.name.EndsWith("·" + MoventoDrawerMesh.SUFFIX_BOTTOM));
        Assert.AreEqual(new Vector3Int(494, 490, 16), bottom.dimensionsMM, "размер дна в спецификации");
        Assert.AreEqual(1, bottom.count);

        var side = result.lines.First(l => l.name.EndsWith("·" + MoventoDrawerMesh.SUFFIX_SIDE));
        Assert.AreEqual(2, side.count, "боковины группируются в count 2");
        Assert.AreEqual(new Vector3Int(490, 86, 16), side.dimensionsMM);
    }

    [Test]
    public void Spec_GtvDrawer_StaysSingleLine()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 500, DrawerColor.Anthracite, 568, "GtvDrawer", Vector3.zero);
        _spawned.Add(go);
        var drawer = go.GetComponent<DrawerElement>();

        var result = SpecificationManager.Build(new List<KitchenElement> { drawer });

        Assert.AreEqual(1, result.lines.Count, "GTV — одна строка (покупной короб)");
        Assert.IsFalse(result.lines[0].name.Contains("·"), "имя GTV без суффикса детали");
        Assert.AreEqual("GtvDrawer", result.lines[0].name);
        Assert.AreEqual(drawer.DimensionsMM, result.lines[0].dimensionsMM, "габарит GTV — контурный бокс");
    }

    [Test]
    public void Spec_TwoIdenticalMovento_PartsGroupAcrossDrawers()
    {
        var a = MakeMovento("MA", DrawerType.A, 500, 568);
        var b = MakeMovento("MB", DrawerType.A, 500, 568);

        var result = SpecificationManager.Build(new List<KitchenElement> { a, b });

        // Те же 3 группы, но количество удваивается: боковины 4, перед+задник 4, дно 2.
        Assert.AreEqual(3, result.lines.Count, "одинаковые детали двух ящиков сходятся в общие группы");
        Assert.AreEqual(10, result.totalCount, "5 деталей × 2 ящика");
    }

    // ── 3. Система: фабрика, имя, сериализация ───────────────────────────

    [Test]
    public void Factory_MoventoSystem_SetAndNamed()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 500, DrawerColor.Anthracite, 568, "", Vector3.zero, DrawerSystem.Movento);
        _spawned.Add(go);
        var drawer = go.GetComponent<DrawerElement>();

        Assert.AreEqual(DrawerSystem.Movento, drawer.System);
        // Имя по умолчанию «Ящик Movento» приводится к латинице (ElementNaming) —
        // важно, что система именно Movento и это отражено в имени.
        StringAssert.Contains("Movento", go.name);
    }

    [Test]
    public void ElementData_PersistsSystem()
    {
        var movento = MakeMovento("M", DrawerType.A, 500, 568);
        var gtvGo = ElementFactory.CreateDrawer(DrawerType.A, 500, DrawerColor.Anthracite, 568, "G", Vector3.zero);
        _spawned.Add(gtvGo);

        Assert.AreEqual(1, ElementData.FromElement(movento).drawerSystem, "Movento → 1");
        Assert.AreEqual(0, ElementData.FromElement(gtvGo.GetComponent<DrawerElement>()).drawerSystem, "GTV → 0");
    }

    [Test]
    public void Serialization_MoventoSystem_SurvivesRoundTrip()
    {
        MakeMovento("RT", DrawerType.B, 500, 568);

        var elements = _spawned.Select(g => g.GetComponent<KitchenElement>()).ToList();
        var data = SaveLoadManager.CaptureScene(elements);
        var json = SaveLoadManager.Serialize(data);
        var loaded = SaveLoadManager.Deserialize(json);

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        SaveLoadManager.RestoreScene(loaded!);

        var restored = Object.FindObjectsByType<DrawerElement>(FindObjectsSortMode.None);
        Assert.AreEqual(1, restored.Length);
        Assert.AreEqual(DrawerSystem.Movento, restored[0].System, "система переживает сохранение/загрузку");
    }
}
