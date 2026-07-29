using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Варочная поверхность: захват столешницы сверху и отрыв вниз, коллизии
/// с боковинами и ящиками под столешницей. Выреза в пласти не делает.</summary>
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

    private KitchenElement CreateCountertop(int widthMM = 1200, int depthMM = 600, string name = "Countertop")
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

    private KitchenElement CreateSidePanel(float x, string name = "Side")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        el.DimensionsMM = new Vector3Int(560, 700, 18);
        el.transform.position = new Vector3(x, -0.35f, 0f);
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
            CooktopElement.WIDTH_MM, CooktopElement.TOTAL_HEIGHT_MM, CooktopElement.DEPTH_MM);
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

    [Test]
    public void Cooktop_FitsSingle600Module()
    {
        Assert.LessOrEqual(CooktopElement.MinPartWidthMM, CooktopElement.WIDTH_MM);
        Assert.LessOrEqual(CooktopElement.MinPartDepthMM, CooktopElement.DEPTH_MM);
        Assert.LessOrEqual(CooktopElement.MinPartWidthMM, 600);
        Assert.LessOrEqual(CooktopElement.MinPartDepthMM, 600);
        Assert.AreEqual(CooktopElement.RIM_HEIGHT_MM + CooktopElement.BODY_DEPTH_MM,
            CooktopElement.TOTAL_HEIGHT_MM);
    }

    [Test]
    public void Cooktop_FitsCountertop()
    {
        var top = CreateCountertop(600, 600);
        Assert.IsTrue(CooktopElement.IsSuitableHost(top), "столешница модуля 600 годится под варочную");
        var cooktop = CreateSeatedCooktop(top);
        Assert.IsTrue(cooktop.IsAttached);
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
        Assert.IsFalse(CooktopElement.IsSuitableHost(upright));
    }

    [Test]
    public void TooSmallBoard_IsNotSuitableHost()
    {
        var small = CreateCountertop(CooktopElement.WIDTH_MM - 1, 600);
        Assert.IsFalse(CooktopElement.IsSuitableHost(small));
    }

    [Test]
    public void BodyBlocked_WhenSidePanelCrossesIt()
    {
        var top = CreateCountertop(2000, 1200);
        CreateSidePanel(0f);

        var cooktop = CreateCooktop(new Vector3(0.6f, TopSurfaceY(top) + 0.02f, 0f));
        cooktop.SnapToPart();
        Assert.IsTrue(cooktop.IsAttached);

        Assert.IsTrue(cooktop.BodyBlocked(top, 0, 0), "боковина проходит сквозь тело варочной");
        Assert.IsFalse(cooktop.BodyBlocked(top, 500, 0));
    }

    [Test]
    public void MovingCooktop_BlockedBySidePanel()
    {
        var top = CreateCountertop(2000, 1200);
        // Боковина на offset=100mm от центра столешницы
        CreateSidePanel(0.1f);
        // Варочная справа от боковины
        var cooktop = CreateSeatedCooktop(top, 0.5f);
        int startOffset = cooktop.OffsetXMM;
        Assert.Greater(startOffset, 0);

        // Двигаем влево, к боковине — должны упереться и не налезть на неё.
        for (int step = 0; step < 15; step++)
        {
            cooktop.transform.position -= new Vector3(0.04f, 0f, 0f);
            cooktop.SnapToPart();
        }

        Assert.IsTrue(cooktop.IsAttached);
        // Варочная НЕ должна наехать на боковину (OffsetXMM должно быть
        // достаточно большим, чтобы тело варочной не перекрывало боковину)
        Assert.IsFalse(cooktop.BodyBlocked(top, cooktop.OffsetXMM, cooktop.OffsetYMM),
            $"тело варочной при offset {cooktop.OffsetXMM}мм НЕ должно пересекать боковину");
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
    public void FromElement_Cooktop_StoresHostAndOffsets()
    {
        var top = CreateCountertop();
        var cooktop = CreateSeatedCooktop(top, 0.2f);

        var d = ElementData.FromElement(cooktop);

        Assert.IsTrue(d.isCooktop);
        Assert.IsFalse(d.isSink);
        Assert.AreEqual(top.PartName, d.cooktopAttachedPartName);
        Assert.AreEqual(cooktop.OffsetXMM, d.cooktopOffsetXMM);
        Assert.AreEqual(cooktop.OffsetYMM, d.cooktopOffsetYMM);
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
    public void TwoCountertops_WithCooktop_HaveNoViolation()
    {
        var top1 = CreateCountertop(1200, 600, "Top1");
        var top2Go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(top2Go);
        var top2 = top2Go.AddComponent<KitchenElement>();
        top2.PartName = "Top2";
        top2.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        top2.DimensionsMM = new Vector3Int(1200, 600, TopThicknessMM);
        top2.transform.position = new Vector3(1.2f, 0f, 0f);
        PartRegistry.Register(top2);

        var cooktop = CreateCooktop(new Vector3(0.2f, TopSurfaceY(top1) + 0.02f, 0f));
        cooktop.SnapToPart();

        var result = ConstraintValidator.Validate(new List<KitchenElement> { top1, top2, cooktop });
        Assert.IsFalse(result.violations.Contains(cooktop));
    }
}
