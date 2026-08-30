using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Варочная поверхность: две коробки (плита 5 мм сверху и короб выреза
/// внутрь столешницы), редактируемые размеры, захват столешницы сверху и отрыв
/// вниз, проём в пласти, магнит выреза к боковинам и фасадам, ошибка при наезде
/// короба на боковину.</summary>
public class CooktopElementTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _guard;

    [SetUp]
    public void SetUp() => _guard = ProjectLoadStateGuard.Capture();

    private const float ToU = AppConstants.MM_TO_UNITS;
    private const int TopThicknessMM = 38;

    [TearDown]
    public void TearDown()
    {
        _guard?.Restore();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    private KitchenElement CreateCountertop(int widthMM = 1200, int depthMM = 650, string name = "Countertop")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        el.DimensionsMM = new Vector3Int(widthMM, depthMM, TopThicknessMM);
        PartRegistry.Register(el);
        return el;
    }

    // Корпус под столешницей: верх ровно под её нижней пластью (−19 мм), чтобы
    // сам корпус со столешницей не пересекался и в нарушения не попадал —
    // иначе тесты про наезд ВЫРЕЗА ничего не проверяли бы.
    private const float UnderTopY = -0.369f;   // центр детали высотой 700 мм

    /// <summary>Боковина под столешницей: стоит вертикально, тянется вниз.</summary>
    private KitchenElement CreateSidePanel(float x, string name = "Side")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        el.DimensionsMM = new Vector3Int(560, 700, 18);
        el.transform.position = new Vector3(x, UnderTopY, 0f);
        PartRegistry.Register(el);
        return el;
    }

    /// <summary>Фасад под столешницей: помехой не считается, но магнитит.</summary>
    private FacadeElement CreateFacade(float z, string name = "Facade")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<FacadeElement>();
        el.PartName = name;
        el.DimensionsMM = new Vector3Int(600, 700, 18);
        el.transform.position = new Vector3(0f, UnderTopY, z);
        PartRegistry.Register(el);
        return el;
    }

    private CooktopElement CreateCooktop(Vector3 position)
    {
        var go = new GameObject("Cooktop");
        _spawned.Add(go);
        go.transform.position = position;
        var cooktop = go.AddComponent<CooktopElement>();
        cooktop.PartName = "Cooktop";
        cooktop.DimensionsMM = new Vector3Int(
            CooktopElement.DEFAULT_WIDTH_MM, CooktopElement.DEFAULT_HEIGHT_MM, CooktopElement.DEFAULT_DEPTH_MM);
        PartRegistry.Register(cooktop);
        return cooktop;
    }

    private float TopSurfaceY(KitchenElement top) =>
        top.transform.position.y + TopThicknessMM * 0.5f * ToU;

    private CooktopElement CreateSeatedCooktop(KitchenElement top, float localX = 0f)
    {
        var topSurfaceY = TopSurfaceY(top);
        var cooktop = CreateCooktop(new Vector3(localX, topSurfaceY + 0.05f, 0f));
        cooktop.SnapToPart();
        Assert.IsTrue(cooktop.IsAttached, "варочная должна сесть на столешницу");
        return cooktop;
    }

    // ── Размеры и вырез ───────────────────────────────────────────────

    [Test]
    public void Defaults_PlateAndBody_AddUpToTotalHeight()
    {
        var cooktop = CreateCooktop(Vector3.zero);

        Assert.AreEqual(CooktopElement.DEFAULT_WIDTH_MM, cooktop.WidthMM);
        Assert.AreEqual(CooktopElement.DEFAULT_DEPTH_MM, cooktop.DepthMM);
        Assert.AreEqual(CooktopElement.DEFAULT_HEIGHT_MM, cooktop.HeightMM);
        Assert.AreEqual(CooktopElement.DEFAULT_CUTOUT_WIDTH_MM, cooktop.CutoutWidthMM);
        Assert.AreEqual(CooktopElement.DEFAULT_CUTOUT_DEPTH_MM, cooktop.CutoutDepthMM);
        // Высота — общая: плита 5 мм плюс короб.
        Assert.AreEqual(CooktopElement.DEFAULT_HEIGHT_MM - CooktopElement.RIM_HEIGHT_MM,
            cooktop.BodyHeightMM);
    }

    [Test]
    public void Geometry_TwoBoxes_PlateOnTop_BodyCenteredInside()
    {
        var cooktop = CreateCooktop(Vector3.zero);
        cooktop.WidthMM = 600;
        cooktop.DepthMM = 520;
        cooktop.HeightMM = 85;
        cooktop.CutoutWidthMM = 560;
        cooktop.CutoutDepthMM = 490;

        var top = cooktop.transform.Find("Top");
        var body = cooktop.transform.Find("Body");
        Assert.IsNotNull(top, "верхняя плита");
        Assert.IsNotNull(body, "короб выреза");

        // Плита: габарит из свойств, толщина всегда 5 мм, вся она НАД пластью.
        Assert.AreEqual(600 * ToU, top!.localScale.x, 1e-5f);
        Assert.AreEqual(CooktopElement.RIM_HEIGHT_MM * ToU, top.localScale.y, 1e-5f);
        Assert.AreEqual(520 * ToU, top.localScale.z, 1e-5f);
        Assert.AreEqual(CooktopElement.RIM_HEIGHT_MM * 0.5f * ToU, top.localPosition.y, 1e-5f);

        // Короб: размер выреза, высота = общая − плита, стоит по центру плиты и
        // уходит вниз от неё.
        Assert.AreEqual(560 * ToU, body!.localScale.x, 1e-5f);
        Assert.AreEqual(80 * ToU, body.localScale.y, 1e-5f);
        Assert.AreEqual(490 * ToU, body.localScale.z, 1e-5f);
        Assert.AreEqual(0f, body.localPosition.x, 1e-5f);
        Assert.AreEqual(0f, body.localPosition.z, 1e-5f);
        Assert.AreEqual(-80 * 0.5f * ToU, body.localPosition.y, 1e-5f);
    }

    [Test]
    public void Cutout_ClampedToPlate_KeepsRimOverlap()
    {
        var cooktop = CreateCooktop(Vector3.zero);
        cooktop.WidthMM = 600;
        cooktop.DepthMM = 600;

        cooktop.CutoutWidthMM = 5000;   // шире плиты — борту нечем перекрыть срез
        Assert.AreEqual(600 - 2 * CooktopElement.MIN_RIM_OVERLAP_MM, cooktop.CutoutWidthMM);

        cooktop.CutoutDepthMM = 1;      // уже минимума
        Assert.AreEqual(CooktopElement.MIN_CUTOUT_MM, cooktop.CutoutDepthMM);
    }

    [Test]
    public void ShrinkingPlate_ShrinksCutoutWithIt()
    {
        var cooktop = CreateCooktop(Vector3.zero);
        cooktop.WidthMM = 600;
        cooktop.CutoutWidthMM = 560;

        cooktop.WidthMM = 400;

        Assert.AreEqual(400 - 2 * CooktopElement.MIN_RIM_OVERLAP_MM, cooktop.CutoutWidthMM,
            "вырез не может остаться шире ужавшейся плиты");
    }

    /// <summary>Временно мелкая плита не имеет права СТЕРЕТЬ вырез. Ровно это и
    /// происходило: Awake зовёт ApplyDimensions на заготовке PartData
    /// (800×400×18), вырез схлопывался до минимума ещё до того, как фабрика
    /// выставит настоящий габарит, и в окне свойств стояла «глубина выреза 90».</summary>
    [Test]
    public void TemporarySmallPlate_DoesNotDestroyCutout()
    {
        var cooktop = CreateCooktop(Vector3.zero);
        cooktop.CutoutWidthMM = CooktopElement.DEFAULT_CUTOUT_WIDTH_MM;
        cooktop.CutoutDepthMM = CooktopElement.DEFAULT_CUTOUT_DEPTH_MM;

        // Заготовка PartData: толщина 18 мм по глубине.
        cooktop.DimensionsMM = new Vector3Int(800, 400, 18);
        // …и сразу настоящий габарит, как это делает фабрика.
        cooktop.DimensionsMM = new Vector3Int(
            CooktopElement.DEFAULT_WIDTH_MM, CooktopElement.DEFAULT_HEIGHT_MM, CooktopElement.DEFAULT_DEPTH_MM);

        Assert.AreEqual(CooktopElement.DEFAULT_CUTOUT_WIDTH_MM, cooktop.CutoutWidthMM);
        Assert.AreEqual(CooktopElement.DEFAULT_CUTOUT_DEPTH_MM, cooktop.CutoutDepthMM,
            "вырез обязан вернуться, когда плита снова стала нормальной");
    }

    [Test]
    public void Height_ClampedToPlatePlusMinimalBody()
    {
        var cooktop = CreateCooktop(Vector3.zero);
        cooktop.HeightMM = 1;
        Assert.AreEqual(CooktopElement.RIM_HEIGHT_MM + CooktopElement.MIN_BODY_HEIGHT_MM,
            cooktop.HeightMM);
        Assert.AreEqual(CooktopElement.MIN_BODY_HEIGHT_MM, cooktop.BodyHeightMM);
    }

    // ── Проём в столешнице ────────────────────────────────────────────

    [Test]
    public void SeatedCooktop_CutsHoleInCountertop()
    {
        var top = CreateCountertop(1200, 650);
        var cooktop = CreateSeatedCooktop(top);

        Assert.IsTrue(top.HasCutout(cooktop), "варочная режет проём в столешнице");

        var rects = top.CutoutHoleRects();
        Assert.AreEqual(1, rects.Count);
        // Доли считаются от габаритов детали в осях её плоскости (1200×650).
        Assert.AreEqual(cooktop.CutoutWidthMM / 1200f, rects[0].xMax - rects[0].xMin, 1e-4f);
        Assert.AreEqual(cooktop.CutoutDepthMM / 650f, rects[0].yMax - rects[0].yMin, 1e-4f);
    }

    [Test]
    public void ChangingCutout_ResizesHole()
    {
        var top = CreateCountertop(1200, 650);
        var cooktop = CreateSeatedCooktop(top);

        cooktop.CutoutWidthMM = 300;
        cooktop.SnapToPart();

        var rect = top.CutoutHoleRects()[0];
        Assert.AreEqual(300 / 1200f, rect.xMax - rect.xMin, 1e-4f);
    }

    [Test]
    public void ReleasedCooktop_ClosesHole()
    {
        var top = CreateCountertop();
        var cooktop = CreateSeatedCooktop(top);
        Assert.IsTrue(top.HasCutout(cooktop));

        cooktop.transform.position -= new Vector3(0f, CooktopElement.SNAP_RELEASE_MM * 1.5f * ToU, 0f);
        cooktop.SnapToPart();

        Assert.IsFalse(cooktop.IsAttached);
        Assert.AreEqual(0, top.AttachedCutouts.Count, "проём закрылся");
    }

    // ── Захват столешницы ─────────────────────────────────────────────

    [Test]
    public void Cooktop_FitsCountertop()
    {
        var top = CreateCountertop(610, 610);
        var cooktop = CreateCooktop(new Vector3(0f, TopSurfaceY(top) + 0.05f, 0f));
        Assert.IsTrue(cooktop.IsSuitableHost(top),
            "столешница 610×610 годится под вырез 490×490 с отступом 30 мм");
        cooktop.SnapToPart();
        Assert.IsTrue(cooktop.IsAttached);
    }

    [Test]
    public void StandardCutout_FitsThePlain600Module_OrTheEdgeMarginIsTooStrict()
    {
        var top = CreateCountertop(600, 600);
        var cooktop = CreateCooktop(new Vector3(0f, TopSurfaceY(top) + 0.05f, 0f));
        Assert.IsTrue(cooktop.IsSuitableHost(top),
            "штатная ниша 560 в столешнице модуля 600 оставляет ровно 20 мм за вырезом: "
            + "MIN_EDGE_MM строже 20 отказал бы самой типовой врезке");
    }

    [Test]
    public void MinPartSize_FollowsCutout()
    {
        var cooktop = CreateCooktop(Vector3.zero);
        Assert.AreEqual(cooktop.CutoutWidthMM + 2 * CooktopElement.MIN_EDGE_MM, cooktop.MinPartWidthMM);
        Assert.AreEqual(cooktop.CutoutDepthMM + 2 * CooktopElement.MIN_EDGE_MM, cooktop.MinPartDepthMM);

        cooktop.CutoutWidthMM = 200;
        Assert.AreEqual(200 + 2 * CooktopElement.MIN_EDGE_MM, cooktop.MinPartWidthMM,
            "уменьшили вырез — варочная влезает в более узкую столешницу");
    }

    [Test]
    public void HoveringHigh_DoesNotAttach()
    {
        var top = CreateCountertop();
        var cooktop = CreateCooktop(new Vector3(0f, TopSurfaceY(top) + (CooktopElement.SNAP_CATCH_MM + 50) * ToU, 0f));
        cooktop.SnapToPart();
        Assert.IsFalse(cooktop.IsAttached);
    }

    [Test]
    public void LoweredFromAbove_AttachesOnce_ThenReleasesWhenPulledThrough()
    {
        var top = CreateCountertop();
        var topY = TopSurfaceY(top);
        var cooktop = CreateCooktop(new Vector3(0f, topY + 0.3f, 0f));
        cooktop.SnapToPart();
        Assert.IsFalse(cooktop.IsAttached);

        int attachedAtStep = -1;
        for (int step = 1; step <= 8 && attachedAtStep < 0; step++)
        {
            cooktop.transform.position -= new Vector3(0f, 0.05f, 0f);
            cooktop.SnapToPart();
            if (cooktop.IsAttached) attachedAtStep = step;
        }

        Assert.Greater(attachedAtStep, 0);
        Assert.AreEqual(topY, cooktop.transform.position.y, 1e-4f);

        cooktop.transform.position -= new Vector3(0f, CooktopElement.SNAP_RELEASE_MM * 1.5f * ToU, 0f);
        cooktop.SnapToPart();
        Assert.IsFalse(cooktop.IsAttached);
        Assert.Less(cooktop.transform.position.y, topY);
    }

    [Test]
    public void CooktopBelowCountertop_DoesNotAttachFromUnderneath()
    {
        var top = CreateCountertop();
        var cooktop = CreateCooktop(new Vector3(0f, TopSurfaceY(top) - 0.4f, 0f));
        cooktop.SnapToPart();
        Assert.IsFalse(cooktop.IsAttached);
    }

    [Test]
    public void CooktopBesideCountertop_DoesNotAttach()
    {
        var top = CreateCountertop();
        var cooktop = CreateCooktop(new Vector3(3f, TopSurfaceY(top) + 0.02f, 0f));
        cooktop.SnapToPart();
        Assert.IsFalse(cooktop.IsAttached);
    }

    [Test]
    public void VerticalBoard_IsNotSuitableHost()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var upright = go.AddComponent<KitchenElement>();
        upright.PartName = "Side";
        upright.DimensionsMM = new Vector3Int(1200, 600, 18);
        PartRegistry.Register(upright);

        var cooktop = CreateCooktop(Vector3.zero);
        Assert.IsFalse(cooktop.IsSuitableHost(upright));
    }

    [Test]
    public void TooSmallBoard_IsNotSuitableHost()
    {
        var cooktop = CreateCooktop(Vector3.zero);
        var small = CreateCountertop(cooktop.MinPartWidthMM - 1, cooktop.MinPartDepthMM);
        Assert.IsFalse(cooktop.IsSuitableHost(small));
    }

    [Test]
    public void MovingPart_CarriesCooktopAlong()
    {
        var top = CreateCountertop(2000, 1200);
        var cooktop = CreateSeatedCooktop(top, 0.2f);

        var before = cooktop.transform.position;
        int offsetBefore = cooktop.OffsetXMM;

        var delta = new Vector3(1.5f, 0f, 0.7f);
        top.transform.position += delta;
        cooktop.SnapToPart();

        Assert.AreEqual(offsetBefore, cooktop.OffsetXMM);
        var expected = before + delta;
        Assert.AreEqual(expected.x, cooktop.transform.position.x, 1e-4f);
        Assert.AreEqual(expected.y, cooktop.transform.position.y, 1e-4f);
        Assert.AreEqual(expected.z, cooktop.transform.position.z, 1e-4f);
    }

    [Test]
    public void MovingCooktop_MovesHoleInCountertop()
    {
        var top = CreateCountertop(2000, 1200);
        var cooktop = CreateSeatedCooktop(top, 0.2f);

        var before = top.CutoutHoleRects()[0];
        int offsetBefore = cooktop.OffsetXMM;

        // Рантайм-цепочка перетаскивания: drag пишет в трансформ →
        // SceneChangeTracker в LateUpdate двигает PoseVersion → в следующем
        // кадре Update зовёт SnapToPart, который копит дрейф и пересобирает
        // проём. Тест гонит ту же цепочку явными вызовами.
        cooktop.transform.position += new Vector3(0.3f, 0f, 0f);
        SceneChangeTracker.Poll();
        cooktop.Update();

        Assert.AreEqual(offsetBefore + 300, cooktop.OffsetXMM,
            "смещение от центра столешницы обязано накопиться из дрейфа");
        var after = top.CutoutHoleRects()[0];
        Assert.AreEqual(before.xMin + 300f / 2000f, after.xMin, 1e-4f,
            "проём в столешнице обязан переехать вместе с варочной");
        Assert.AreEqual(before.xMax + 300f / 2000f, after.xMax, 1e-4f);
        Assert.AreEqual(before.yMin, after.yMin, 1e-4f);
        Assert.AreEqual(before.yMax, after.yMax, 1e-4f);
    }

    // ── Магнит к боковинам и фасадам ──────────────────────────────────

    [Test]
    public void CutoutSnapsFlush_ToSidePanelUnderCountertop()
    {
        var top = CreateCountertop(2000, 1200);
        var side = CreateSidePanel(0f);           // грани боковины: x = ±9 мм

        // Ставим варочную так, чтобы левый край выреза не дошёл до боковины
        // 12 мм — это внутри полосы магнита (20 мм).
        var cooktop = CreateCooktop(new Vector3(0f, TopSurfaceY(top) + 0.02f, 0f));
        int flushOffset = 9 + cooktop.CutoutWidthMM / 2;
        cooktop.transform.position = new Vector3((flushOffset + 12) * ToU,
            TopSurfaceY(top) + 0.02f, 0f);
        cooktop.SnapToPart();

        Assert.IsTrue(cooktop.IsAttached);
        Assert.AreEqual(flushOffset, cooktop.OffsetXMM,
            "край выреза обязан встать заподлицо с гранью боковины");
        Assert.IsFalse(cooktop.BodyBlocked(top, cooktop.OffsetXMM, cooktop.OffsetYMM),
            "заподлицо — это касание, а не наложение");
        Assert.IsNotNull(side);
    }

    [Test]
    public void CutoutSnapsFlush_ToFacadeUnderCountertop()
    {
        var top = CreateCountertop(2000, 1200);
        // Фасад стоит поперёк глубины: его грани по Z = 300 ± 9 мм.
        CreateFacade(0.3f);

        var cooktop = CreateCooktop(Vector3.zero);
        // Ближняя грань фасада — z = 291 мм; заподлицо центр выреза стоит на
        // 291 − половина глубины. Локальная ось b столешницы смотрит против
        // мировой Z (пласть повёрнута на −90° по X), отсюда знак смещения.
        int flushZ = 300 - 9 - cooktop.CutoutDepthMM / 2;
        cooktop.transform.position = new Vector3(0f, TopSurfaceY(top) + 0.02f,
            (flushZ - 12) * ToU);
        cooktop.SnapToPart();

        Assert.IsTrue(cooktop.IsAttached);
        Assert.AreEqual(-flushZ, cooktop.OffsetYMM,
            "край выреза встаёт заподлицо и с фасадом");
    }

    [Test]
    public void FarNeighbour_DoesNotMagnetiseCutout()
    {
        var top = CreateCountertop(2000, 1200);
        CreateSidePanel(0f);

        var cooktop = CreateCooktop(Vector3.zero);
        int flushOffset = 9 + cooktop.CutoutWidthMM / 2;
        // 60 мм до заподлицо — втрое дальше полосы магнита.
        cooktop.transform.position = new Vector3((flushOffset + 60) * ToU,
            TopSurfaceY(top) + 0.02f, 0f);
        cooktop.SnapToPart();

        Assert.AreEqual(flushOffset + 60, cooktop.OffsetXMM,
            "далёкая боковина варочную к себе не тянет");
    }

    // ── Коллизии ──────────────────────────────────────────────────────

    [Test]
    public void BodyBlocked_WhenSidePanelCrossesIt()
    {
        var top = CreateCountertop(2000, 1200);
        CreateSidePanel(0f);

        var cooktop = CreateSeatedCooktop(top, 0.6f);

        Assert.IsTrue(cooktop.BodyBlocked(top, 0, 0), "боковина проходит сквозь короб выреза");
        Assert.IsFalse(cooktop.BodyBlocked(top, 500, 0));
    }

    [Test]
    public void Validator_CooktopOverSidePanel_IsViolation()
    {
        var top = CreateCountertop(2000, 1200);
        var side = CreateSidePanel(0f);

        // Садим варочную ровно над боковиной: короб выреза её пересекает.
        var cooktop = CreateCooktop(new Vector3(0f, TopSurfaceY(top) + 0.02f, 0f));
        cooktop.SnapToPart();
        Assert.IsTrue(cooktop.IsAttached, "варочная всё равно садится — движение не блокируем");
        Assert.AreEqual(0, cooktop.OffsetXMM, "и остаётся там, куда её поставили");

        var result = ConstraintValidator.Validate(new List<KitchenElement> { top, side, cooktop });

        Assert.IsTrue(result.violations.Contains(cooktop), "наезд короба на боковину — ошибка");
        Assert.IsTrue(result.violations.Contains(side), "боковина краснеет вместе с ней");
        Assert.IsFalse(result.violations.Contains(top), "своя столешница — не ошибка");
    }

    [Test]
    public void Validator_CooktopOnCountertop_NoViolation()
    {
        var top = CreateCountertop();
        var cooktop = CreateSeatedCooktop(top);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { top, cooktop });

        Assert.IsFalse(result.violations.Contains(cooktop),
            "варочная стоит на столешнице — это не пересечение");
    }

    [Test]
    public void Validator_CooktopOverFacade_NoViolation()
    {
        var top = CreateCountertop(2000, 1200);
        var facade = CreateFacade(0f);

        var cooktop = CreateCooktop(new Vector3(0f, TopSurfaceY(top) + 0.02f, 0f));
        cooktop.SnapToPart();

        var result = ConstraintValidator.Validate(new List<KitchenElement> { top, facade, cooktop });

        Assert.IsFalse(result.violations.Contains(cooktop),
            "фасад навесной — короб уходит за него, это не ошибка");
    }

    [Test]
    public void Validator_SidePanelBesideCutout_NoViolation()
    {
        var top = CreateCountertop(2000, 1200);
        var side = CreateSidePanel(0f);

        var cooktop = CreateCooktop(Vector3.zero);
        int flushOffset = 9 + cooktop.CutoutWidthMM / 2;
        cooktop.transform.position = new Vector3((flushOffset + 12) * ToU,
            TopSurfaceY(top) + 0.02f, 0f);
        cooktop.SnapToPart();

        var result = ConstraintValidator.Validate(new List<KitchenElement> { top, side, cooktop });

        Assert.IsFalse(result.violations.Contains(cooktop),
            "вырез встал заподлицо с боковиной — касание, а не наложение");
    }

    [Test]
    public void Validator_DetachedCooktop_HasNoBodyChecks()
    {
        var side = CreateSidePanel(0f);
        // Варочная висит в воздухе прямо в боковине, ни к чему не привязана.
        var cooktop = CreateCooktop(new Vector3(0f, UnderTopY, 0f));
        Assert.IsFalse(cooktop.IsAttached);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { side, cooktop });

        Assert.IsFalse(result.violations.Contains(cooktop),
            "не врезана — короба «внутрь детали» ещё не существует");
    }

    // ── Сохранение и декор ────────────────────────────────────────────

    [Test]
    public void FromElement_Cooktop_StoresHostOffsetsAndCutout()
    {
        var top = CreateCountertop();
        var cooktop = CreateSeatedCooktop(top, 0.2f);
        cooktop.WidthMM = 600;
        cooktop.HeightMM = 70;
        cooktop.DepthMM = 520;
        cooktop.CutoutWidthMM = 560;
        cooktop.CutoutDepthMM = 490;

        var d = ElementData.FromElement(cooktop);

        Assert.IsTrue(d.isCooktop);
        Assert.IsFalse(d.isSink);
        Assert.AreEqual(top.PartName, d.cooktopAttachedPartName);
        Assert.AreEqual(cooktop.OffsetXMM, d.cooktopOffsetXMM);
        Assert.AreEqual(cooktop.OffsetYMM, d.cooktopOffsetYMM);
        Assert.AreEqual(560, d.cooktopCutoutWidthMM);
        Assert.AreEqual(490, d.cooktopCutoutDepthMM);
        Assert.AreEqual(new[] { 600, 70, 520 }, d.dimensionsMM);
    }

    [Test]
    public void Decor_SurvivesGeometryRebuild()
    {
        var cooktop = CreateCooktop(Vector3.zero);
        var def = new MaterialDef("test-oak", "Тестовый дуб", "ЛДСП", new Color(0.6f, 0.4f, 0.2f));
        MaterialCatalog.Register(def);

        MaterialManager.Apply(cooktop, def);
        var mat = cooktop.transform.Find("Top").GetComponent<MeshRenderer>().sharedMaterial;
        Assert.AreEqual(mat, cooktop.transform.Find("Body").GetComponent<MeshRenderer>().sharedMaterial,
            "декор ложится на обе коробки");

        // Ресайз (и любая другая пересборка геометрии) не имеет права сбросить
        // декор на штатное чёрное стекло — ровно это и терялось при перезапуске.
        cooktop.WidthMM = 600;

        Assert.AreEqual("test-oak", cooktop.MaterialId);
        Assert.AreEqual(mat, cooktop.transform.Find("Top").GetComponent<MeshRenderer>().sharedMaterial);
        Assert.AreEqual(mat, cooktop.transform.Find("Body").GetComponent<MeshRenderer>().sharedMaterial);

        MaterialCatalog.Reset();
    }

    [Test]
    public void ApplyById_UnknownDecor_KeepsIdForLaterLoad()
    {
        var cooktop = CreateCooktop(Vector3.zero);

        // Каталог ещё не приехал (WebGL) — декора с таким id в нём нет.
        MaterialManager.ApplyById(cooktop, "decor-from-save");

        Assert.AreEqual("decor-from-save", cooktop.MaterialId,
            "id из сейва нельзя терять: иначе перезапуск стирает текстуру");
    }
}
