using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>Предупреждения анализа сцены: почти-касание (недожатый снэп),
/// зазоры фасада, ящик без фасада.</summary>
public class SceneAnalyzerTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private KitchenElement MakeBoard(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = dims;
        el.transform.position = pos;
        PartRegistry.Register(el);
        _spawned.Add(go);
        return el;
    }

    private FacadeElement MakeFacade(string name, int gapL, int gapR, int gapT, int gapB)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var f = go.AddComponent<FacadeElement>();
        f.PartName = name;
        f.DimensionsMM = new Vector3Int(400, 700, 18);
        f.GapLeft = gapL; f.GapRight = gapR; f.GapTop = gapT; f.GapBottom = gapB;
        PartRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    private DrawerElement MakeDrawer(string name, string facade, bool isDouble = false, bool isUpper = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var d = go.AddComponent<DrawerElement>();
        d.PartName = name;
        d.Type = DrawerType.C;
        d.NominalLength = 500;
        d.InternalWidth = 400;
        d.IsDouble = isDouble;
        d.IsUpperDrawer = isUpper;
        d.AttachedFacadeName = facade;
        PartRegistry.Register(d);
        _spawned.Add(go);
        return d;
    }

    private static bool Has(List<AnalysisIssue> issues, string code) =>
        issues.Exists(i => i.Code == code);

    // ── Почти касание ────────────────────────────────────────────────

    [Test]
    public void NearContact_SmallGap_Warns()
    {
        // Две пласти 600×400 разнесены по Z на 5 мм (тонкая щель).
        MakeBoard("a", new Vector3Int(600, 400, 18), Vector3.zero);
        MakeBoard("b", new Vector3Int(600, 400, 18), new Vector3(0, 0, 0.023f)); // 9+5+9 мм

        Assert.IsTrue(Has(SceneAnalyzer.Analyze(), IssueCatalog.CodeNearContact));
    }

    [Test]
    public void NearContact_LargeGap_NoWarning()
    {
        // Зазор 12 мм — визуально заметен, не предупреждаем (порог 8 мм).
        MakeBoard("a", new Vector3Int(600, 400, 18), Vector3.zero);
        MakeBoard("b", new Vector3Int(600, 400, 18), new Vector3(0, 0, 0.030f)); // 9+12+9 мм

        Assert.IsFalse(Has(SceneAnalyzer.Analyze(), IssueCatalog.CodeNearContact));
    }

    // ── Зазоры фасада ────────────────────────────────────────────────

    [Test]
    public void Facade_ZeroGapOnSide_Warns()
    {
        MakeFacade("fac", gapL: 0, gapR: 2, gapT: 2, gapB: 2);
        Assert.IsTrue(Has(SceneAnalyzer.Analyze(), IssueCatalog.CodeFacadeGap));
    }

    [Test]
    public void Facade_AllGapsOk_NoWarning()
    {
        MakeFacade("fac", gapL: 1, gapR: 2, gapT: 2, gapB: 2);
        Assert.IsFalse(Has(SceneAnalyzer.Analyze(), IssueCatalog.CodeFacadeGap));
    }

    // ── Ящик без фасада ──────────────────────────────────────────────

    [Test]
    public void Drawer_NoFacade_Warns()
    {
        MakeDrawer("d", facade: "");
        Assert.IsTrue(Has(SceneAnalyzer.Analyze(), IssueCatalog.CodeDrawerNoFacade));
    }

    [Test]
    public void Drawer_WithFacade_NoWarning()
    {
        MakeDrawer("d", facade: "some_facade");
        Assert.IsFalse(Has(SceneAnalyzer.Analyze(), IssueCatalog.CodeDrawerNoFacade));
    }

    [Test]
    public void UpperDoubleDrawer_NoFacade_NotWarned()
    {
        // Верхняя половина двойного ящика штатно без своего фасада.
        MakeDrawer("d_upper", facade: "", isDouble: true, isUpper: true);
        Assert.IsFalse(Has(SceneAnalyzer.Analyze(), IssueCatalog.CodeDrawerNoFacade));
    }

    // ── Фасад ящика оторвался (DRW-02) ───────────────────────────────

    [Test]
    public void DrawerFacade_InContact_NoWarning()
    {
        // Ящик type A: 400×86×350 мм, фронт в z=0.175.
        // Фасад 400×86×18 мм вплотную: задняя грань фасада (−Z) на z=0.175.
        var goD = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, "d", new Vector3(0f, 0.043f, 0f));
        var goF = ElementFactory.CreateFacade(new Vector3Int(400, 86, 18), "f", new Vector3(0f, 0.043f, 0.184f), 2, 2, 2, 2);
        _spawned.Add(goD);
        _spawned.Add(goF);
        var drawer = goD.GetComponent<DrawerElement>();
        drawer.AttachedFacadeName = "f";

        Assert.IsFalse(Has(SceneAnalyzer.Analyze(), IssueCatalog.CodeDrawerFacadeOrphaned));
    }

    [Test]
    public void DrawerFacade_Detached_Warns()
    {
        // Фасад далеко от ящика — контакта нет → DRW-02.
        var goD = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, "d", new Vector3(0f, 0.043f, 0f));
        var goF = ElementFactory.CreateFacade(new Vector3Int(400, 86, 18), "f", new Vector3(0f, 0.043f, 0.5f), 2, 2, 2, 2);
        _spawned.Add(goD);
        _spawned.Add(goF);
        var drawer = goD.GetComponent<DrawerElement>();
        drawer.AttachedFacadeName = "f";

        Assert.IsTrue(Has(SceneAnalyzer.Analyze(), IssueCatalog.CodeDrawerFacadeOrphaned));
    }

    [Test]
    public void DrawerFacade_NoSuchFacade_NoWarning()
    {
        // AttachedFacadeName указывает на несуществующий фасад — DRW-02 не срабатывает
        // (за это отвечает DRW-01, но здесь он не сработает т.к. имя НЕ пустое).
        var goD = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, "d", new Vector3(0f, 0.043f, 0f));
        _spawned.Add(goD);
        var drawer = goD.GetComponent<DrawerElement>();
        drawer.AttachedFacadeName = "ghost_facade";

        Assert.IsFalse(Has(SceneAnalyzer.Analyze(), IssueCatalog.CodeDrawerFacadeOrphaned));
    }
}
