using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Полный round-trip всех свойств DrawerElement через capture → serialize → file → deserialize → restore.
/// </summary>
public class DrawerRoundTripTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
        {
            if (go != null) Object.DestroyImmediate(go);
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

    // ── helpers ─────────────────────────────────────────────────────────

    private static string TempPath() =>
        Path.Combine(Application.temporaryCachePath, $"rt_drawer_{System.Guid.NewGuid():N}.json");

    private DrawerElement GetDrawer(GameObject go)
    {
        var el = go.GetComponent<DrawerElement>();
        _spawned.Add(go);
        PartRegistry.Register(el);
        return el;
    }

    private DrawerElement MakeDrawer(string name, DrawerType type, int length, DrawerColor color, int width, Vector3 pos)
    {
        var go = ElementFactory.CreateDrawer(type, length, color, width, name, pos);
        return GetDrawer(go);
    }

    private ProjectData FullRoundTrip()
    {
        var path = TempPath();
        var elements = new List<KitchenElement>();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) elements.Add(el);
        }

        var original = SaveLoadManager.CaptureScene(elements);
        SaveLoadManager.SaveToFile(path, original);

        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();

        var loaded = SaveLoadManager.LoadFromFile(path);
        Assert.IsNotNull(loaded, "LoadFromFile should not return null");
        SaveLoadManager.RestoreScene(loaded);

        File.Delete(path);
        return loaded;
    }

    private ProjectData MemoryRoundTrip()
    {
        var elements = new List<KitchenElement>();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) elements.Add(el);
        }
        var data = SaveLoadManager.CaptureScene(elements);
        var json = SaveLoadManager.Serialize(data);
        return SaveLoadManager.Deserialize(json);
    }

    // ── 1. Basic properties ─────────────────────────────────────────────

    [Test]
    public void Drawer_BasicProperties_FullRoundTrip()
    {
        MakeDrawer("TestDrawer", DrawerType.A, 350, DrawerColor.Anthracite, 400, new Vector3(1, 2, 3));

        FullRoundTrip();

        var restored = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(1, restored.Length, "should restore exactly one element");

        var d = restored[0] as DrawerElement;
        Assert.IsNotNull(d, "restored should be DrawerElement");
        Assert.AreEqual("TestDrawer", d.PartName, "name");
        Assert.AreEqual(DrawerType.A, d.Type, "type");
        Assert.AreEqual(350, d.NominalLength, "length");
        Assert.AreEqual(DrawerColor.Anthracite, d.Color, "color");
        Assert.AreEqual(400, d.InternalWidth, "width");
        Assert.AreEqual(1f, d.transform.position.x, 0.001f, "pos.x");
        Assert.AreEqual(2f, d.transform.position.y, 0.001f, "pos.y");
        Assert.AreEqual(3f, d.transform.position.z, 0.001f, "pos.z");
    }

    // ── 2. Type variants ────────────────────────────────────────────────

    [Test]
    public void Drawer_TypeB_FullRoundTrip()
    {
        MakeDrawer("TypeB", DrawerType.B, 350, DrawerColor.Anthracite, 400, Vector3.zero);

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual(DrawerType.B, d.Type, "type B should survive");
    }

    [Test]
    public void Drawer_TypeD_FullRoundTrip()
    {
        MakeDrawer("TypeD", DrawerType.D, 350, DrawerColor.Anthracite, 400, Vector3.zero);

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual(DrawerType.D, d.Type, "type D should survive");
    }

    // ── 3. Length variants ──────────────────────────────────────────────

    [Test]
    public void Drawer_Length_250_FullRoundTrip()
    {
        MakeDrawer("Len250", DrawerType.A, 250, DrawerColor.Anthracite, 400, Vector3.zero);

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual(250, d.NominalLength, "length 250 should survive");
    }

    [Test]
    public void Drawer_Length_600_FullRoundTrip()
    {
        MakeDrawer("Len600", DrawerType.A, 600, DrawerColor.Anthracite, 400, Vector3.zero);

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual(600, d.NominalLength, "length 600 should survive");
    }

    // ── 4. Color variants ───────────────────────────────────────────────

    [Test]
    public void Drawer_ColorWhite_FullRoundTrip()
    {
        MakeDrawer("White", DrawerType.A, 350, DrawerColor.White, 400, Vector3.zero);

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual(DrawerColor.White, d.Color, "white color should survive");
    }

    [Test]
    public void Drawer_ColorBlack_FullRoundTrip()
    {
        MakeDrawer("Black", DrawerType.A, 350, DrawerColor.Black, 400, Vector3.zero);

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual(DrawerColor.Black, d.Color, "black color should survive");
    }

    // ── 5. Width variant ────────────────────────────────────────────────

    [Test]
    public void Drawer_InternalWidth_FullRoundTrip()
    {
        MakeDrawer("Wide", DrawerType.A, 350, DrawerColor.Anthracite, 750, Vector3.zero);

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual(750, d.InternalWidth, "width 750 should survive");
    }

    // ── 6. Double-drawer flags ──────────────────────────────────────────

    [Test]
    public void Drawer_DoubleFlags_FullRoundTrip()
    {
        var drawer = MakeDrawer("Double", DrawerType.A, 350, DrawerColor.Anthracite, 400, Vector3.zero);
        drawer.IsDouble = true;
        drawer.IsUpperDrawer = true;

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.IsTrue(d.IsDouble, "IsDouble should survive");
        Assert.IsTrue(d.IsUpperDrawer, "IsUpperDrawer should survive");
    }

    // ── 7. Paired/attached names ────────────────────────────────────────

    [Test]
    public void Drawer_PairedName_FullRoundTrip()
    {
        var drawer = MakeDrawer("Pair", DrawerType.A, 350, DrawerColor.Anthracite, 400, Vector3.zero);
        drawer.PairedDrawerName = "OtherDrawer";

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual("OtherDrawer", d.PairedDrawerName, "paired name should survive");
    }

    [Test]
    public void Drawer_AttachedFacadeName_FullRoundTrip()
    {
        var drawer = MakeDrawer("WithFacade", DrawerType.A, 350, DrawerColor.Anthracite, 400, Vector3.zero);
        drawer.AttachedFacadeName = "MyFacade";

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual("MyFacade", d.AttachedFacadeName, "attached facade name should survive");
    }

    // ── 8. DoubleState ──────────────────────────────────────────────────

    [Test]
    public void Drawer_DoubleState_FullRoundTrip()
    {
        var drawer = MakeDrawer("StateDrawer", DrawerType.A, 350, DrawerColor.Anthracite, 400, Vector3.zero);
        drawer.DoubleState = DoubleDrawerState.BothOpen;

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual(DoubleDrawerState.BothOpen, d.DoubleState, "double state should survive");
    }

    [Test]
    public void Drawer_SingleOpenState_FullRoundTrip()
    {
        var drawer = MakeDrawer("OpenSingle", DrawerType.A, 350, DrawerColor.Anthracite, 400, Vector3.zero);
        drawer.SetOpen(true);

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.IsTrue(d.IsOpen, "открытость одиночного ящика переживает сохранение (doorOpen)");
    }

    [Test]
    public void Drawer_SingleClosedState_FullRoundTrip()
    {
        MakeDrawer("ClosedSingle", DrawerType.A, 350, DrawerColor.Anthracite, 400, Vector3.zero);

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.IsFalse(d.IsOpen, "закрытый ящик остаётся закрытым после загрузки");
    }

    // ── 9. Closed pose ──────────────────────────────────────────────────

    [Test]
    public void Drawer_ClosedPose_FullRoundTrip()
    {
        var pos = new Vector3(0.5f, 0.2f, -1.0f);
        var drawer = MakeDrawer("ClosedPose", DrawerType.A, 350, DrawerColor.Anthracite, 400, pos);

        Assert.AreEqual(drawer.transform.position, drawer.ClosedPosition, "ClosedPosition should match transform.position when closed");

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual(d.transform.position, d.ClosedPosition, "ClosedPosition should match transform.position after restore");
    }

    [Test]
    public void Drawer_OpenedState_ClosedPoseSurvived()
    {
        var pos = new Vector3(0.5f, 0.2f, -1.0f);
        var drawer = MakeDrawer("OpenDrawer", DrawerType.A, 350, DrawerColor.Anthracite, 400, pos);
        var closedPos = drawer.transform.position;

        drawer.SetOpen(true);
        drawer.StepAnimation(1f);
        Assert.IsTrue(drawer.IsOpen, "should be open before save");

        FullRoundTrip();

        var d = Object.FindObjectsByType<KitchenElement>()[0] as DrawerElement;
        Assert.IsNotNull(d);
        Assert.AreEqual(closedPos.x, d.ClosedPosition.x, 0.001f, "closed pos.x survives open drawer");
        Assert.AreEqual(closedPos.y, d.ClosedPosition.y, 0.001f, "closed pos.y survives open drawer");
        Assert.AreEqual(closedPos.z, d.ClosedPosition.z, 0.001f, "closed pos.z survives open drawer");
    }

    // ── 10. JSON structure ──────────────────────────────────────────────

    [Test]
    public void Drawer_JsonContainsDrawerFields()
    {
        MakeDrawer("JsonDrawer", DrawerType.A, 350, DrawerColor.Anthracite, 400, Vector3.zero);
        var data = MemoryRoundTrip();
        var json = SaveLoadManager.Serialize(data);

        Assert.IsTrue(json.Contains("\"isDrawer\""), "isDrawer field");
        Assert.IsTrue(json.Contains("\"drawerType\""), "drawerType field");
    }

    // ── 11. All types / colors / lengths through memory round-trip ──────

    [Test]
    public void Drawer_MemoryRoundTrip_AllTypes()
    {
        var types = new[] { DrawerType.A, DrawerType.B, DrawerType.C, DrawerType.D };

        foreach (var type in types)
        {
            var drawer = MakeDrawer("TypeTest", type, 350, DrawerColor.Anthracite, 400, Vector3.zero);
            var restored = MemoryRoundTrip();
            Assert.AreEqual((int)type, restored.elements[0].drawerType, $"drawerType for {type} should survive");
            Object.DestroyImmediate(drawer.gameObject);
            _spawned.Clear();
            PartRegistry.Clear();
        }
    }

    [Test]
    public void Drawer_MemoryRoundTrip_AllColors()
    {
        var colors = new[] { DrawerColor.Anthracite, DrawerColor.White, DrawerColor.Black };

        foreach (var color in colors)
        {
            var drawer = MakeDrawer("ColorTest", DrawerType.A, 350, color, 400, Vector3.zero);
            var restored = MemoryRoundTrip();
            Assert.AreEqual((int)color, restored.elements[0].drawerColor, $"drawerColor for {color} should survive");
            Object.DestroyImmediate(drawer.gameObject);
            _spawned.Clear();
            PartRegistry.Clear();
        }
    }

    [Test]
    public void Drawer_MemoryRoundTrip_AllLengths()
    {
        foreach (var len in DrawerConstants.ValidLengths)
        {
            var drawer = MakeDrawer("LenTest", DrawerType.A, len, DrawerColor.Anthracite, 400, Vector3.zero);
            var restored = MemoryRoundTrip();
            Assert.AreEqual(len, restored.elements[0].drawerNominalLength, $"nominal length {len} should survive");
            Object.DestroyImmediate(drawer.gameObject);
            _spawned.Clear();
            PartRegistry.Clear();
        }
    }
}
