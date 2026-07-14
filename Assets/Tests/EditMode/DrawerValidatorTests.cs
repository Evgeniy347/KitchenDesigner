using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class DrawerValidatorTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private const int WallThicknessMM = 18;
    private const int WallHeightMM = 720;
    // AABB ящика — контурный бокс проёма (зазор направляющих уже внутри),
    // поэтому правильная установка — стенка вплотную к контуру (зазор 0).
    private const float ClearanceMM = 0f;

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

    private DrawerElement MakeDrawer(string name, DrawerType type, int length, int width, DrawerColor color, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var d = go.AddComponent<DrawerElement>();
        d.PartName = name;
        d.Type = type;
        d.NominalLength = length;
        d.InternalWidth = width;
        d.Color = color;
        PartRegistry.Register(d);
        _spawned.Add(go);
        return d;
    }

    private KitchenElement MakeBoard(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private List<KitchenElement> All() => PartRegistry.GetAll();

    private static float WallX(float drawerInternalWidthMM, float clearanceMM, float wallHalfThicknessMM)
    {
        return (drawerInternalWidthMM * 0.5f + clearanceMM + wallHalfThicknessMM) * AppConstants.MM_TO_UNITS;
    }

    [Test]
    public void ValidateSideWalls_NoLeftWall_ReturnsError()
    {
        var drawer = MakeDrawer("Drawer", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));
        MakeBoard("RightWall", new Vector3Int(WallThicknessMM, WallHeightMM, 350),
            new Vector3(WallX(400, ClearanceMM, WallThicknessMM * 0.5f), 0f, 0f));

        var result = DrawerValidator.ValidateSideWalls(drawer, All());

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains("левой", result.Errors[0]);
    }

    [Test]
    public void ValidateSideWalls_NoRightWall_ReturnsError()
    {
        var drawer = MakeDrawer("Drawer", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));
        MakeBoard("LeftWall", new Vector3Int(WallThicknessMM, WallHeightMM, 350),
            new Vector3(-WallX(400, ClearanceMM, WallThicknessMM * 0.5f), 0f, 0f));

        var result = DrawerValidator.ValidateSideWalls(drawer, All());

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains("правой", result.Errors[0]);
    }

    [Test]
    public void ValidateSideWalls_BothWallsPresent_ReturnsOk()
    {
        var drawer = MakeDrawer("Drawer", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));
        float wallX = WallX(400, ClearanceMM, WallThicknessMM * 0.5f);
        MakeBoard("LeftWall", new Vector3Int(WallThicknessMM, WallHeightMM, 350), new Vector3(-wallX, 0f, 0f));
        MakeBoard("RightWall", new Vector3Int(WallThicknessMM, WallHeightMM, 350), new Vector3(wallX, 0f, 0f));

        var result = DrawerValidator.ValidateSideWalls(drawer, All());

        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void ValidateSideWalls_TooFarLeft_ReturnsError()
    {
        var drawer = MakeDrawer("Drawer", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));
        float tooFarMM = DrawerValidator.MAX_CLEARANCE_PER_SIDE_MM + 1f;
        float wallX = WallX(400, tooFarMM, WallThicknessMM * 0.5f);
        MakeBoard("LeftWall", new Vector3Int(WallThicknessMM, WallHeightMM, 350), new Vector3(-wallX, 0f, 0f));

        var result = DrawerValidator.ValidateSideWalls(drawer, All());

        Assert.IsFalse(result.IsValid);
    }

    [Test]
    public void ValidateSideWalls_TooCloseLeft_ReturnsError()
    {
        var drawer = MakeDrawer("Drawer", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));
        float tooCloseMM = DrawerValidator.MIN_CLEARANCE_PER_SIDE_MM - 1f;
        float wallX = WallX(400, tooCloseMM, WallThicknessMM * 0.5f);
        MakeBoard("LeftWall", new Vector3Int(WallThicknessMM, WallHeightMM, 350), new Vector3(-wallX, 0f, 0f));

        var result = DrawerValidator.ValidateSideWalls(drawer, All());

        Assert.IsFalse(result.IsValid);
    }

    [Test]
    public void ValidateSideWalls_ThinWall_ReturnsError()
    {
        var drawer = MakeDrawer("Drawer", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));
        int thinMM = Mathf.RoundToInt(DrawerValidator.MIN_WALL_THICKNESS_MM) - 1;
        float wallX = WallX(400, ClearanceMM, thinMM * 0.5f);
        MakeBoard("LeftWall", new Vector3Int(thinMM, WallHeightMM, 350), new Vector3(-wallX, 0f, 0f));

        var result = DrawerValidator.ValidateSideWalls(drawer, All());

        Assert.IsFalse(result.IsValid);
    }

    [Test]
    public void ValidateSideWalls_ThickWall_ReturnsOk()
    {
        var drawer = MakeDrawer("Drawer", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));
        float wallX = WallX(400, ClearanceMM, 18 * 0.5f);
        MakeBoard("LeftWall", new Vector3Int(18, WallHeightMM, 350), new Vector3(-wallX, 0f, 0f));
        MakeBoard("RightWall", new Vector3Int(18, WallHeightMM, 350), new Vector3(wallX, 0f, 0f));

        var result = DrawerValidator.ValidateSideWalls(drawer, All());

        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void ValidateSideWalls_WallShiftedAwayInZ_NotCounted()
    {
        var drawer = MakeDrawer("Drawer", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));
        float wallX = WallX(400, ClearanceMM, WallThicknessMM * 0.5f);
        // Обе «стенки» на правильном расстоянии по X, но в 2 метрах по Z —
        // ящик между ними не стоит, стенками они не считаются.
        MakeBoard("FarLeft", new Vector3Int(WallThicknessMM, WallHeightMM, 350), new Vector3(-wallX, 0f, 2f));
        MakeBoard("FarRight", new Vector3Int(WallThicknessMM, WallHeightMM, 350), new Vector3(wallX, 0f, 2f));

        var result = DrawerValidator.ValidateSideWalls(drawer, All());

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(2, result.Errors.Count, "обе стенки не найдены");
    }

    [Test]
    public void ValidateSideWalls_WallShiftedAwayInY_NotCounted()
    {
        var drawer = MakeDrawer("Drawer", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));
        float wallX = WallX(400, ClearanceMM, WallThicknessMM * 0.5f);
        // «Стенка» целиком выше ящика (второй ярус) — не считается.
        MakeBoard("HighLeft", new Vector3Int(WallThicknessMM, WallHeightMM, 350),
            new Vector3(-wallX, 1.5f, 0f));

        var result = DrawerValidator.ValidateSideWalls(drawer, All());

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains("левой", result.Errors[0]);
    }

    // ── ValidateCabinetFit: AABB двойного ящика считается по паре ────────

    private (DrawerElement lower, DrawerElement upper) MakeDoublePair()
    {
        // Шаг пары — высота контурного бокса (мин. проём типа A = 115 мм → 0.115).
        float h = DrawerConstants.GetMinOpeningHeight(DrawerType.A) * AppConstants.MM_TO_UNITS;
        var lower = MakeDrawer("Lower", DrawerType.A, 350, 400, DrawerColor.Anthracite,
            new Vector3(0f, h * 0.5f, 0f));
        var upper = MakeDrawer("Upper", DrawerType.A, 350, 400, DrawerColor.Anthracite,
            new Vector3(0f, h * 1.5f, 0f));
        lower.IsDouble = true; upper.IsDouble = true;
        upper.IsUpperDrawer = true;
        lower.PairedDrawerName = "Upper";
        upper.PairedDrawerName = "Lower";
        return (lower, upper);
    }

    [Test]
    public void ValidateCabinetFit_PairInsideCabinet_ReturnsOk()
    {
        var (lower, _) = MakeDoublePair();
        // Дно под парой и крыша над парой (пара занимает 0..0.230 по Y).
        MakeBoard("Bottom", new Vector3Int(600, 18, 600), new Vector3(0f, -0.009f, 0f));
        MakeBoard("Top", new Vector3Int(600, 18, 600), new Vector3(0f, 0.248f, 0f));

        var result = DrawerValidator.ValidateCabinetFit(lower, All());

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    [Test]
    public void ValidateCabinetFit_TopPanelCutsThroughPair_ReturnsError()
    {
        var (lower, _) = MakeDoublePair();
        MakeBoard("Bottom", new Vector3Int(600, 18, 600), new Vector3(0f, -0.009f, 0f));
        // «Крыша» на высоте 0.1 — внутри нижнего контура, НИЖЕ верха пары (0.230).
        // Если бы AABB считалась только по нижнему ящику, панель сошла бы за верх
        // корпуса; по AABB пары верх корпуса не найден.
        MakeBoard("MidPanel", new Vector3Int(600, 18, 600), new Vector3(0f, 0.109f, 0f));

        var result = DrawerValidator.ValidateCabinetFit(lower, All());

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains("верх", result.Errors[0]);
    }

    [Test]
    public void ValidateCabinetFit_SingleDrawer_Skipped()
    {
        var drawer = MakeDrawer("Single", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));

        var result = DrawerValidator.ValidateCabinetFit(drawer, All());

        Assert.IsTrue(result.IsValid, "одиночный ящик не проверяется на вместимость");
    }

    // ── FreeHeightAboveMM: место под верхний ящик пары ───────────────────

    [Test]
    public void FreeHeightAbove_NoPanels_ReturnsMax()
    {
        var drawer = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.0575f, 0f));
        Assert.AreEqual(float.MaxValue, DrawerValidator.FreeHeightAboveMM(drawer, All()));
    }

    [Test]
    public void FreeHeightAbove_PanelAbove_ReturnsGapMM()
    {
        // Контур типа A: верх на y = 0.115. Панель 18 мм с низом на y = 0.215 → 100 мм.
        var drawer = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.0575f, 0f));
        MakeBoard("Top", new Vector3Int(600, 18, 600), new Vector3(0f, 0.224f, 0f));

        float free = DrawerValidator.FreeHeightAboveMM(drawer, All());
        Assert.AreEqual(100f, free, 0.5f);
    }

    [Test]
    public void FreeHeightAbove_IgnoresPanelNotOverlappingInPlan()
    {
        var drawer = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.0575f, 0f));
        // Панель выше, но в двух метрах в стороне — не мешает.
        MakeBoard("Far", new Vector3Int(600, 18, 600), new Vector3(2f, 0.224f, 0f));

        Assert.AreEqual(float.MaxValue, DrawerValidator.FreeHeightAboveMM(drawer, All()));
    }

    [Test]
    public void ValidateAll_EmptyScene_ReturnsErrors()
    {
        var drawer = MakeDrawer("Drawer", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));

        var result = DrawerValidator.ValidateAll(drawer, All());

        Assert.IsFalse(result.IsValid);
        Assert.Greater(result.Errors.Count, 1);
    }

    [Test]
    public void ValidateAll_PerfectSetup_ReturnsOk()
    {
        var drawer = MakeDrawer("Drawer", DrawerType.A, 350, 400, DrawerColor.Anthracite, new Vector3(0f, 0.08f, 0f));
        float wallX = WallX(400, ClearanceMM, WallThicknessMM * 0.5f);
        MakeBoard("LeftWall", new Vector3Int(WallThicknessMM, WallHeightMM, 350), new Vector3(-wallX, 0f, 0f));
        MakeBoard("RightWall", new Vector3Int(WallThicknessMM, WallHeightMM, 350), new Vector3(wallX, 0f, 0f));

        var result = DrawerValidator.ValidateAll(drawer, All());

        Assert.IsTrue(result.IsValid);
    }
}
