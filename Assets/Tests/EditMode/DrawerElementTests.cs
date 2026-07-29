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
        // Габарит элемента — контурный бокс: высота = мин. проём корпуса.
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.Type = DrawerType.B;
        Assert.AreEqual(DrawerConstants.GetMinOpeningHeight(DrawerType.B), d.DimensionsMM.y);
        Assert.AreEqual(147, d.DimensionsMM.y);
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
    public void Drawer_SetDimensionsMM_AcceptsOnlyWidth()
    {
        // Ручки ресайза/undo пишут DimensionsMM напрямую: принимается только
        // ширина (LW), высота и глубина пересчитываются из типа и длины.
        var d = MakeDrawer("D", DrawerType.B, 400, 400, DrawerColor.Anthracite);
        d.DimensionsMM = new Vector3Int(555, 999, 777);

        Assert.AreEqual(555, d.InternalWidth, "ширина принята");
        Assert.AreEqual(555, d.DimensionsMM.x);
        Assert.AreEqual(DrawerConstants.GetMinOpeningHeight(DrawerType.B), d.DimensionsMM.y,
            "высота вернулась к контуру типа");
        Assert.AreEqual(400, d.DimensionsMM.z, "глубина вернулась к номинальной длине");
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
        Assert.AreEqual(0.115f, d.transform.localScale.y, 1e-5f); // мин. проём типа A
        Assert.AreEqual(0.350f, d.transform.localScale.z, 1e-5f);
    }

    [Test]
    public void Drawer_Vertices_MatchExpected()
    {
        var d = MakeDrawerAt("D", DrawerType.A, 350, 400, DrawerColor.Anthracite, Vector3.zero);
        var verts = d.GetVertices();

        Assert.AreEqual(8, verts.Length);
        float halfX = 0.200f;
        float halfY = 0.0575f; // половина мин. проёма типа A (115 мм)
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
        Assert.DoesNotThrow(() => { d.PairedDrawerName = null!; });
    }

    [Test]
    public void Drawer_NullFacadeName_DoesNotThrow()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        Assert.DoesNotThrow(() => { d.AttachedFacadeName = null!; });
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

    // ═══════════════════════════════════════════════════════════════
    // SYSTEM CONVERSION (GTV ↔ Movento)
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Drawer_DefaultSystem_IsGtv()
    {
        var d = MakeDrawer("D", DrawerType.C, 350, 400, DrawerColor.Anthracite);
        Assert.AreEqual(DrawerSystem.Gtv, d.System);
    }

    [Test]
    public void Drawer_SwitchToMovento_PreservesInternalWidth()
    {
        var d = MakeDrawer("D", DrawerType.C, 350, 564, DrawerColor.Anthracite);
        d.System = DrawerSystem.Movento;
        Assert.AreEqual(564, d.InternalWidth, "InternalWidth не должен меняться при смене системы");
    }

    [Test]
    public void Drawer_SwitchToMovento_RebuildsMesh()
    {
        var d = MakeDrawer("D", DrawerType.C, 350, 564, DrawerColor.Anthracite);
        var meshBefore = d.GetComponent<MeshFilter>().sharedMesh;
        d.System = DrawerSystem.Movento;
        var meshAfter = d.GetComponent<MeshFilter>().sharedMesh;
        Assert.IsNotNull(meshAfter, "меш должен пересобраться");
        Assert.AreNotSame(meshBefore, meshAfter, "меш Movento отличается от GTV");
    }

    [Test]
    public void Drawer_SwitchBackToGtv_PreservesInternalWidth()
    {
        var d = MakeDrawer("D", DrawerType.C, 350, 564, DrawerColor.Anthracite);
        d.System = DrawerSystem.Movento;
        d.System = DrawerSystem.Gtv;
        Assert.AreEqual(564, d.InternalWidth, "InternalWidth не теряется при Gtv→Movento→Gtv");
        Assert.AreEqual(DrawerSystem.Gtv, d.System);
    }

    // ═══════════════════════════════════════════════════════════════
    // RESIZE → INTERNAL WIDTH SYNC
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Drawer_SetDimensionsMM_InternalWidthFollows()
    {
        // При ресайзе ручками DimensionsMM.x меняется → InternalWidth синхронизируется
        var d = MakeDrawer("D", DrawerType.C, 350, 400, DrawerColor.Anthracite);
        d.DimensionsMM = new Vector3Int(500, 999, 777);
        Assert.AreEqual(500, d.InternalWidth, "InternalWidth = DimensionsMM.x после ресайза");
        Assert.AreEqual(500, d.DimensionsMM.x, "DimensionsMM.x = InternalWidth");
    }

    [Test]
    public void Drawer_SetDimensionsMM_Movento_InternalWidthFollows()
    {
        var d = MakeDrawer("D", DrawerType.C, 350, 400, DrawerColor.Anthracite);
        d.System = DrawerSystem.Movento;
        d.DimensionsMM = new Vector3Int(600, 999, 777);
        Assert.AreEqual(600, d.InternalWidth, "InternalWidth = DimensionsMM.x после ресайза Movento");
        Assert.AreEqual(600, d.DimensionsMM.x);
    }

    [Test]
    public void Drawer_ResizeViaDimensionsMM_PreservesTypeHeight()
    {
        var d = MakeDrawer("D", DrawerType.C, 350, 400, DrawerColor.Anthracite);
        int expectedH = DrawerConstants.GetMinOpeningHeight(DrawerType.C);
        d.DimensionsMM = new Vector3Int(500, 999, 777);
        Assert.AreEqual(expectedH, d.DimensionsMM.y, "высота всегда от типа");
        Assert.AreEqual(350, d.DimensionsMM.z, "глубина всегда от номинальной длины");
    }

    [Test]
    public void Drawer_ResizeViaDimensionsMM_Movento_PreservesTypeHeight()
    {
        var d = MakeDrawer("D", DrawerType.D, 450, 400, DrawerColor.Anthracite);
        d.System = DrawerSystem.Movento;
        int expectedH = DrawerConstants.GetMinOpeningHeight(DrawerType.D);
        d.DimensionsMM = new Vector3Int(700, 999, 555);
        Assert.AreEqual(expectedH, d.DimensionsMM.y);
        Assert.AreEqual(450, d.DimensionsMM.z);
        Assert.AreEqual(700, d.InternalWidth);
    }

    [Test]
    public void Drawer_MultipleResizes_InternalWidthStaysConsistent()
    {
        var d = MakeDrawer("D", DrawerType.B, 300, 400, DrawerColor.Anthracite);
        int[] widths = { 500, 350, 600, 250, 800 };
        foreach (int w in widths)
        {
            d.DimensionsMM = new Vector3Int(w, 0, 0);
            Assert.AreEqual(w, d.InternalWidth, $"после ресайза до {w} мм");
            Assert.AreEqual(w, d.DimensionsMM.x);
        }
    }

    [Test]
    public void Drawer_Movento_MultipleResizes_InternalWidthStaysConsistent()
    {
        var d = MakeDrawer("D", DrawerType.B, 300, 400, DrawerColor.Anthracite);
        d.System = DrawerSystem.Movento;
        int[] widths = { 500, 350, 600, 250, 800 };
        foreach (int w in widths)
        {
            d.DimensionsMM = new Vector3Int(w, 0, 0);
            Assert.AreEqual(w, d.InternalWidth, $"Movento: после ресайза до {w} мм");
            Assert.AreEqual(w, d.DimensionsMM.x);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // INTERNAL WIDTH → DIMENSIONS MM ROUND-TRIP
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Drawer_InternalWidthToDimensionsMM_RoundTrip()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.InternalWidth = 550;
        Assert.AreEqual(550, d.DimensionsMM.x, "DimensionsMM.x = InternalWidth после установки");
        Assert.AreEqual(550, d.InternalWidth, "InternalWidth не изменился обратно");
    }

    [Test]
    public void Drawer_Movento_InternalWidthToDimensionsMM_RoundTrip()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.System = DrawerSystem.Movento;
        d.InternalWidth = 550;
        Assert.AreEqual(550, d.DimensionsMM.x);
        Assert.AreEqual(550, d.InternalWidth);
    }

    [Test]
    public void Drawer_ScaleMatchesDimensionsMM_AfterResize()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.DimensionsMM = new Vector3Int(600, 0, 0);
        Assert.AreEqual(0.600f, d.transform.localScale.x, 1e-5f);
    }

    [Test]
    public void Drawer_Movento_ScaleMatchesDimensionsMM_AfterResize()
    {
        var d = MakeDrawer("D", DrawerType.A, 350, 400, DrawerColor.Anthracite);
        d.System = DrawerSystem.Movento;
        d.DimensionsMM = new Vector3Int(600, 0, 0);
        Assert.AreEqual(0.600f, d.transform.localScale.x, 1e-5f);
    }

    [Test]
    public void Drawer_SystemSwitchThenResize_InternalWidthCorrect()
    {
        var d = MakeDrawer("D", DrawerType.C, 400, 564, DrawerColor.Anthracite);
        d.System = DrawerSystem.Movento;
        Assert.AreEqual(564, d.InternalWidth, "после конвертации");
        d.DimensionsMM = new Vector3Int(480, 0, 0);
        Assert.AreEqual(480, d.InternalWidth, "после конвертации + ресайза");
        Assert.AreEqual(480, d.DimensionsMM.x);
    }

    [Test]
    public void Drawer_SystemSwitchBackAndForth_Resize_InternalWidthCorrect()
    {
        var d = MakeDrawer("D", DrawerType.C, 400, 564, DrawerColor.Anthracite);
        d.System = DrawerSystem.Movento;
        d.System = DrawerSystem.Gtv;
        d.DimensionsMM = new Vector3Int(320, 0, 0);
        Assert.AreEqual(320, d.InternalWidth);
        Assert.AreEqual(320, d.DimensionsMM.x);
        d.System = DrawerSystem.Movento;
        Assert.AreEqual(320, d.InternalWidth, "после Gtv→Movento→Gtv→resize→Movento");
    }

    // ═══════════════════════════════════════════════════════════════
    // DOUBLE DRAWER + SHARED FACADE — COLLISION REGRESSION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Двойной ящик с общим фасадом должен открываться полностью в BothOpen,
    /// без блокировки верхнего ящика фасадом (коллизия закрытой позы vs открытой).</summary>
    [Test]
    public void DoubleDrawer_WithSharedFacade_BothOpen_BothDrawersFullyOpen()
    {
        var rot = Quaternion.Euler(0f, 270f, 0f);

        // Нижний ящик
        var goL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(goL);
        goL.transform.SetPositionAndRotation(new Vector3(1.315f, 0.52f, -2.162f), rot);
        var lower = goL.AddComponent<DrawerElement>();
        lower.PartName = "A3_DD_Lower";
        lower.Type = DrawerType.A;
        lower.NominalLength = 500;
        lower.InternalWidth = 564;
        lower.Color = DrawerColor.Anthracite;

        // Верхний ящик
        var goU = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(goU);
        goU.transform.SetPositionAndRotation(new Vector3(1.315f, 0.635f, -2.162f), rot);
        var upper = goU.AddComponent<DrawerElement>();
        upper.PartName = "A3_DD_Upper";
        upper.Type = DrawerType.A;
        upper.NominalLength = 500;
        upper.InternalWidth = 564;
        upper.Color = DrawerColor.Anthracite;

        // Фасад
        var goF = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(goF);
        goF.transform.SetPositionAndRotation(new Vector3(1.036f, 0.64f, -2.162f), rot);
        var facade = goF.AddComponent<FacadeElement>();
        facade.PartName = "A3_Facade";
        facade.DimensionsMM = new Vector3Int(596, 356, 18);
        facade.GapLeft = 2;
        facade.GapRight = 2;
        facade.GapTop = 2;
        facade.GapBottom = 2;
        facade.Mode = DoorMode.DrawerOut;

        // Регистрация (Awake уже зарегистрировал, но PartRegistry.Register идемпотентен)
        PartRegistry.Register(lower);
        PartRegistry.Register(upper);
        PartRegistry.Register(facade);

        lower.IsDouble = true;
        upper.IsDouble = true;
        upper.IsUpperDrawer = true;

        lower.PairedDrawerName = upper.PartName;
        upper.PairedDrawerName = lower.PartName;

        lower.AttachedFacadeName = facade.PartName;

        float closedLowerX = lower.transform.position.x;
        float closedUpperX = upper.transform.position.x;
        float closedFacadeX = facade.transform.position.x;

        lower.CycleDoubleState(); // → BothOpen (syncs both)
        Assert.AreEqual(DoubleDrawerState.BothOpen, lower.DoubleState);

        lower.StepAnimation(1f);
        upper.StepAnimation(1f);
        facade.StepDoor(1f);

        Assert.IsTrue(lower.IsOpen, "нижний открыт");
        Assert.IsTrue(upper.IsOpen, "верхний открыт");
        Assert.IsTrue(facade.IsOpen, "фасад открыт");

        float expectedSlide = DrawerConstants.DRAWER_SLIDE_METERS;

        Assert.AreEqual(1f, lower.AnimProgress, 0.01f, "нижний ящик открылся полностью");
        Assert.AreEqual(1f, upper.AnimProgress, 0.01f, "верхний ящик открылся полностью");
        Assert.AreEqual(1f, facade.DoorProgress, 0.01f, "фасад открылся полностью");

        Assert.AreEqual(closedLowerX - expectedSlide, lower.transform.position.x, 0.01f,
            "нижний ящик сместился на полный ход");
        Assert.AreEqual(closedUpperX - expectedSlide, upper.transform.position.x, 0.01f,
            "верхний ящик сместился на полный ход");
        Assert.AreEqual(closedFacadeX - expectedSlide, facade.transform.position.x, 0.01f,
            "фасад сместился на полный ход");
    }
}
