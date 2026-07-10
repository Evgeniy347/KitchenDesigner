using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class DrawerElementTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

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

    private DrawerElement MakeDrawer(string name, DrawerType type, int length, int width, DrawerColor color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
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

    private DrawerElement MakeDrawerAt(string name, DrawerType type, int length, int width, DrawerColor color, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
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

    private DrawerElement MakeDoubleDrawer(string name, DrawerType type, int length, int width, DrawerColor color, bool isUpper)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var d = go.AddComponent<DrawerElement>();
        d.PartName = name;
        d.Type = type;
        d.NominalLength = length;
        d.InternalWidth = width;
        d.Color = color;
        d.IsDouble = true;
        d.IsUpperDrawer = isUpper;
        PartRegistry.Register(d);
        _spawned.Add(go);
        return d;
    }

    // ═══════════════════════════════════════════════════════════════
    // BASIC PROPERTIES
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Drawer_DefaultType_IsA()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.AreEqual(DrawerType.A, d.Type);
    }

    [Test]
    public void Drawer_DefaultLength_Is350()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.AreEqual(350, d.NominalLength);
    }

    [Test]
    public void Drawer_DefaultColor_IsAnthracite()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.AreEqual(DrawerColor.Anthracite, d.Color);
    }

    [Test]
    public void Drawer_DefaultWidth_Is400()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.AreEqual(400, d.InternalWidth);
    }

    [Test]
    public void Drawer_DefaultIsNotDouble()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.IsFalse(d.IsDouble);
    }

    [Test]
    public void Drawer_DefaultIsNotUpper()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.IsFalse(d.IsUpperDrawer);
    }

    [Test]
    public void Drawer_PairedDrawerName_EmptyByDefault()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.IsTrue(string.IsNullOrEmpty(d.PairedDrawerName));
    }

    [Test]
    public void Drawer_AttachedFacadeName_EmptyByDefault()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.IsTrue(string.IsNullOrEmpty(d.AttachedFacadeName));
    }

    // ═══════════════════════════════════════════════════════════════
    // DIMENSIONS
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Drawer_SetType_ChangesHeightY()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.Type = DrawerType.B;
        Assert.AreEqual(120, d.DimensionsMM.y);
    }

    [Test]
    public void Drawer_SetLength_ChangesDepthZ()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.NominalLength = 500;
        Assert.AreEqual(500, d.DimensionsMM.z);
    }

    [Test]
    public void Drawer_SetWidth_ChangesWidthX()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.InternalWidth = 600;
        Assert.AreEqual(600, d.DimensionsMM.x);
    }

    [Test]
    public void Drawer_InvalidLength_KeepsPrevious()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.NominalLength = 200;
        Assert.AreEqual(350, d.NominalLength);
    }

    [Test]
    public void Drawer_ValidLength_Accepts()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.NominalLength = 450;
        Assert.AreEqual(450, d.NominalLength);
    }

    [Test]
    public void Drawer_AllValidLengths_AreAccepted()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        foreach (var len in DrawerConstants.ValidLengths)
        {
            d.NominalLength = len;
            Assert.AreEqual(len, d.NominalLength, $"Length {len} should be accepted");
        }
    }

    [Test]
    public void Drawer_NegativeWidth_Clamped()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.InternalWidth = -50;
        Assert.GreaterOrEqual(d.InternalWidth, 100);
    }

    [Test]
    public void Drawer_ApplyDimensions_SetsCorrectScale()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.AreEqual(0.400f, d.transform.localScale.x, 1e-5f);
        Assert.AreEqual(0.086f, d.transform.localScale.y, 1e-5f);
        Assert.AreEqual(0.350f, d.transform.localScale.z, 1e-5f);
    }

    [Test]
    public void Drawer_Vertices_MatchExpected()
    {
        var d = MakeDrawerAt("D", DrawerType.A, 350, 400, DrawerColor.Anthracite, Vector3.zero);
        var verts = d.GetVertices();

        Assert.AreEqual(8, verts.Length);
        float halfX = 0.200f;
        float halfY = 0.043f;
        float halfZ = 0.175f;
        foreach (var v in verts)
        {
            Assert.IsTrue(Mathf.Abs(v.x) <= halfX + 1e-4f, $"v.x={v.x} exceeds {halfX}");
            Assert.IsTrue(Mathf.Abs(v.y) <= halfY + 1e-4f, $"v.y={v.y} exceeds {halfY}");
            Assert.IsTrue(Mathf.Abs(v.z) <= halfZ + 1e-4f, $"v.z={v.z} exceeds {halfZ}");
        }
    }

    [Test]
    public void Drawer_GetFaces_ReturnsSixFaces()
    {
        var d = MakeDrawerAt("D", DrawerType.A, 350, 400, DrawerColor.Anthracite, Vector3.zero);
        var faces = d.GetFaces();
        Assert.AreEqual(6, faces.Length);
    }

    // ═══════════════════════════════════════════════════════════════
    // SINGLE DRAWER ANIMATION
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Drawer_InitialState_Closed()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.IsFalse(d.IsOpen);
        Assert.AreEqual(0f, d.AnimProgress, 1e-5f);
    }

    [Test]
    public void Drawer_SetOpenTrue_ChangesState()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.SetOpen(true);
        Assert.IsTrue(d.IsOpen);
    }

    [Test]
    public void Drawer_StepAnimation_MovesDrawer()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        var closedPos = d.transform.position;
        d.SetOpen(true);
        d.StepAnimation(0.5f);
        Assert.AreNotEqual(closedPos, d.transform.position);
    }

    [Test]
    public void Drawer_StepAnimation_SlidesForward()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        var closedPos = d.transform.position;
        d.SetOpen(true);
        d.StepAnimation(1f);
        Assert.Greater(d.transform.position.z, closedPos.z);
    }

    [Test]
    public void Drawer_FullOpenAnimation_ReachesMaxSlide()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        var closedPos = d.transform.position;
        d.SetOpen(true);
        d.StepAnimation(1f);
        Assert.AreEqual(closedPos.z + 0.4f, d.transform.position.z, 1e-3f);
    }

    [Test]
    public void Drawer_CloseAnimation_ReturnsToClosed()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        var closedPos = d.transform.position;
        d.SetOpen(true);
        d.StepAnimation(1f);
        d.SetOpen(false);
        d.StepAnimation(1f);
        Assert.AreEqual(closedPos, d.transform.position);
    }

    [Test]
    public void Drawer_ClosedPosition_EqualsTransformWhenClosed()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.AreEqual(d.transform.position, d.ClosedPosition);
    }

    [Test]
    public void Drawer_ForceClose_ResetsState()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        var closedPos = d.transform.position;
        d.SetOpen(true);
        d.StepAnimation(1f);
        d.ForceClose();
        Assert.IsFalse(d.IsOpen);
        Assert.AreEqual(closedPos, d.transform.position);
    }

    [Test]
    public void Drawer_ToggleOpen_TogglesState()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.ToggleOpen();
        Assert.IsTrue(d.IsOpen);
        d.ToggleOpen();
        Assert.IsFalse(d.IsOpen);
    }

    [Test]
    public void Drawer_Easing_NotLinear()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        var closedZ = d.transform.position.z;
        d.SetOpen(true);

        float quarterDuration = 0.1f;
        d.StepAnimation(quarterDuration);

        float midSlide = d.transform.position.z - closedZ;
        float fullSlide = 0.4f;
        float delta = Mathf.Abs(midSlide - fullSlide * 0.5f);
        Assert.Greater(delta, 0.001f, "halfway should not be exactly 0.5 of full slide (sine easing)");
    }

    // ═══════════════════════════════════════════════════════════════
    // DOUBLE DRAWER
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void DoubleDrawer_DefaultState_Closed()
    {
        var d = MakeDrawer("DD", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.IsDouble = true;
        Assert.IsFalse(d.IsOpen);
        Assert.AreEqual(0f, d.AnimProgress, 1e-5f);
    }

    [Test]
    public void DoubleDrawer_IsDouble_True()
    {
        var d = MakeDrawer("DD", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.IsDouble = true;
        Assert.IsTrue(d.IsDouble);
    }

    [Test]
    public void DoubleDrawer_CycleState_ClosedToBothOpen()
    {
        var d = MakeDrawer("DD", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.IsDouble = true;
        d.CycleDoubleState();
        Assert.AreEqual(DoubleDrawerState.BothOpen, d.DoubleState);
    }

    [Test]
    public void DoubleDrawer_CycleState_BothOpenToLowerOnly()
    {
        var d = MakeDrawer("DD", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.IsDouble = true;
        d.CycleDoubleState();
        d.CycleDoubleState();
        Assert.AreEqual(DoubleDrawerState.LowerOnly, d.DoubleState);
    }

    [Test]
    public void DoubleDrawer_CycleState_LowerOnlyToClosed()
    {
        var d = MakeDrawer("DD", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.IsDouble = true;
        d.CycleDoubleState();
        d.CycleDoubleState();
        d.CycleDoubleState();
        Assert.AreEqual(DoubleDrawerState.Closed, d.DoubleState);
    }

    [Test]
    public void DoubleDrawer_CycleCompletesInThreeSteps()
    {
        var d = MakeDrawer("DD", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.IsDouble = true;
        d.CycleDoubleState();
        Assert.AreEqual(DoubleDrawerState.BothOpen, d.DoubleState);
        d.CycleDoubleState();
        Assert.AreEqual(DoubleDrawerState.LowerOnly, d.DoubleState);
        d.CycleDoubleState();
        Assert.AreEqual(DoubleDrawerState.Closed, d.DoubleState);
    }

    [Test]
    public void DoubleDrawer_Upper_AnimatesInBothOpen()
    {
        var upper = MakeDoubleDrawer("Upper_DD", DrawerType.A, 350, 400, DrawerColor.Anthracite, isUpper: true);
        var lower = MakeDoubleDrawer("Lower_DD", DrawerType.A, 350, 400, DrawerColor.Anthracite, isUpper: false);
        upper.PairedDrawerName = lower.PartName;
        lower.PairedDrawerName = upper.PartName;

        upper.CycleDoubleState();

        upper.StepAnimation(1f);
        lower.StepAnimation(1f);

        Assert.IsTrue(upper.IsOpen);
    }

    // Семантика состояний: Closed→оба закрыты, BothOpen→оба открыты,
        // LowerOnly→только нижний открыт (верхний закрыт, «Закрыть верхний»).
        [Test]
        public void DoubleDrawer_BothOpen_LowerAndUpperAreOpen()
        {
            var upper = MakeDoubleDrawer("Upper_DD", DrawerType.A, 350, 400, DrawerColor.Anthracite, isUpper: true);
            var lower = MakeDoubleDrawer("Lower_DD", DrawerType.A, 350, 400, DrawerColor.Anthracite, isUpper: false);
            upper.PairedDrawerName = lower.PartName;
            lower.PairedDrawerName = upper.PartName;

            upper.CycleDoubleState(); // Closed → BothOpen (синхронизирует оба)
            upper.StepAnimation(1f);
            lower.StepAnimation(1f);

            Assert.IsTrue(upper.IsOpen, "верхний открыт в BothOpen");
            Assert.IsTrue(lower.IsOpen, "нижний открыт в BothOpen (цикл верхнего синхронизирует пару)");
        }

        [Test]
        public void DoubleDrawer_LowerOnly_LowerOpenUpperClosed()
        {
            var upper = MakeDoubleDrawer("Upper_DD", DrawerType.A, 350, 400, DrawerColor.Anthracite, isUpper: true);
            var lower = MakeDoubleDrawer("Lower_DD", DrawerType.A, 350, 400, DrawerColor.Anthracite, isUpper: false);
            upper.PairedDrawerName = lower.PartName;
            lower.PairedDrawerName = upper.PartName;

            upper.CycleDoubleState(); // → BothOpen
            upper.StepAnimation(1f); lower.StepAnimation(1f);

            upper.CycleDoubleState(); // → LowerOnly (закрыть верхний)
            upper.StepAnimation(1f); lower.StepAnimation(1f);

            Assert.IsFalse(upper.IsOpen, "верхний закрыт в LowerOnly");
            Assert.IsTrue(lower.IsOpen, "нижний открыт в LowerOnly");
        }

        [Test]
        public void DoubleDrawer_CycleFromLower_SyncsUpper()
        {
            var upper = MakeDoubleDrawer("Upper_DD", DrawerType.A, 350, 400, DrawerColor.Anthracite, isUpper: true);
            var lower = MakeDoubleDrawer("Lower_DD", DrawerType.A, 350, 400, DrawerColor.Anthracite, isUpper: false);
            upper.PairedDrawerName = lower.PartName;
            lower.PairedDrawerName = upper.PartName;

            // Цикл по нижнему тоже должен синхронизировать верхний.
            lower.CycleDoubleState(); // → BothOpen
            upper.StepAnimation(1f); lower.StepAnimation(1f);

            Assert.IsTrue(upper.IsOpen, "верхний открылся после цикла нижнего");
            Assert.IsTrue(lower.IsOpen, "нижний открыт в BothOpen");
        }

    // ═══════════════════════════════════════════════════════════════
    // EDGE CASES
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Drawer_NullPairedName_DoesNotThrow()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.DoesNotThrow(() => { d.PairedDrawerName = null; });
    }

    [Test]
    public void Drawer_NullFacadeName_DoesNotThrow()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.DoesNotThrow(() => { d.AttachedFacadeName = null; });
    }

    [Test]
    public void Drawer_MinWidth_100()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.InternalWidth = 100;
        Assert.AreEqual(100, d.InternalWidth);
    }

    [Test]
    public void Drawer_RapidToggle_NoError()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.DoesNotThrow(() =>
        {
            d.SetOpen(true);
            d.SetOpen(false);
            d.SetOpen(true);
        });
    }
}
