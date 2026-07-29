using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class DrawerFactoryTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        MaterialCatalog.Register(new MaterialDef("gtv_anthracite", "Антрацит (GTV)", "Металл", new Color(0.25f, 0.25f, 0.27f)));
        MaterialCatalog.Register(new MaterialDef("gtv_white", "Белый (GTV)", "Металл", new Color(0.92f, 0.92f, 0.90f)));
        MaterialCatalog.Register(new MaterialDef("gtv_black", "Чёрный (GTV)", "Металл", new Color(0.10f, 0.10f, 0.11f)));
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

    [Test]
    public void CreateDrawer_ReturnsGameObject_WithDrawerElement()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, "TestDrawer", Vector3.zero);
        _spawned.Add(go);

        Assert.IsNotNull(go);
        Assert.AreEqual("TestDrawer", go.name);
        Assert.IsNotNull(go.GetComponent<DrawerElement>());
        Assert.AreEqual("KitchenElement", go.tag);
    }

    [Test]
    public void CreateDrawer_SetsAllProperties()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, "TestDrawer", Vector3.zero);
        _spawned.Add(go);

        var drawer = go.GetComponent<DrawerElement>();
        Assert.IsNotNull(drawer);
        Assert.AreEqual(DrawerType.A, drawer.Type);
        Assert.AreEqual(350, drawer.NominalLength);
        Assert.AreEqual(DrawerColor.Anthracite, drawer.Color);
        Assert.AreEqual(400, drawer.InternalWidth);
    }

    [Test]
    public void CreateDrawer_RegistersInPartRegistry()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.B, 300, DrawerColor.White, 500, "RegistryDrawer", Vector3.zero);
        _spawned.Add(go);

        var all = PartRegistry.GetAll();
        Assert.IsTrue(all.Exists(e => e.PartName == "RegistryDrawer"));
    }

    [Test]
    public void CreateDrawer_AppliesColorMaterial()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.C, 400, DrawerColor.Black, 600, "ColorDrawer", Vector3.zero);
        _spawned.Add(go);

        var drawer = go.GetComponent<DrawerElement>();
        Assert.AreEqual(DrawerConstants.GetColorMaterialId(DrawerColor.Black), drawer.MaterialId);
    }

    [Test]
    public void CreateDrawer_DefaultName_EmptyString()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 250, DrawerColor.Anthracite, 300, "", Vector3.zero);
        _spawned.Add(go);

        // \u0418\u043C\u044F \u043F\u043E \u0443\u043C\u043E\u043B\u0447\u0430\u043D\u0438\u044E \u043F\u0440\u043E\u0445\u043E\u0434\u0438\u0442 \u0447\u0435\u0440\u0435\u0437 ElementNaming: \u00AB\u042F\u0449\u0438\u043A GTV\u00BB \u2192 \u00ABYaschik_GTV\u00BB.
        Assert.AreEqual("Yaschik_GTV", go.name);
    }

    [Test]
    public void CreateDrawer_DefaultName_Null()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.B, 500, DrawerColor.White, 450, null!, Vector3.zero);
        _spawned.Add(go);

        // \u0418\u043C\u044F \u043F\u043E \u0443\u043C\u043E\u043B\u0447\u0430\u043D\u0438\u044E \u043F\u0440\u043E\u0445\u043E\u0434\u0438\u0442 \u0447\u0435\u0440\u0435\u0437 ElementNaming: \u00AB\u042F\u0449\u0438\u043A GTV\u00BB \u2192 \u00ABYaschik_GTV\u00BB.
        Assert.AreEqual("Yaschik_GTV", go.name);
    }

    [Test]
    public void DestroyElement_RemovesDrawerFromRegistry()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 300, DrawerColor.Anthracite, 400, "ToDestroy", Vector3.zero);
        _spawned.Add(go);

        Assert.IsTrue(PartRegistry.GetAll().Exists(e => e.PartName == "ToDestroy"));

        ElementFactory.DestroyElement(go);
        Assert.IsFalse(PartRegistry.GetAll().Exists(e => e.PartName == "ToDestroy"));
    }

    [Test]
    public void Duplicate_Drawer_PreservesProperties()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.C, 450, DrawerColor.Black, 550, "SrcDrawer", Vector3.zero);
        _spawned.Add(go);

        var src = go.GetComponent<DrawerElement>();
        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);

        var dup = dupGo.GetComponent<DrawerElement>();
        Assert.IsNotNull(dup);
        Assert.AreEqual(DrawerType.C, dup.Type);
        Assert.AreEqual(450, dup.NominalLength);
        Assert.AreEqual(DrawerColor.Black, dup.Color);
        Assert.AreEqual(550, dup.InternalWidth);
    }

    [Test]
    public void Duplicate_Drawer_PreservesDoubleFlags()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, "DoubleSrc", Vector3.zero);
        _spawned.Add(go);

        var src = go.GetComponent<DrawerElement>();
        src.IsDouble = true;
        src.IsUpperDrawer = true;
        src.PairedDrawerName = "Paired123";
        src.AttachedFacadeName = "Facade456";

        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);

        var dup = dupGo.GetComponent<DrawerElement>();
        Assert.IsTrue(dup.IsDouble);
        Assert.IsTrue(dup.IsUpperDrawer);
        // Связи по именам НЕ копируются: копия не должна «красть» пару/фасад
        // оригинала (иначе цикл копии двигал бы чужой парный ящик).
        Assert.IsEmpty(dup.PairedDrawerName);
        Assert.IsEmpty(dup.AttachedFacadeName);
    }

    [Test]
    public void Duplicate_Drawer_HasDifferentName()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 300, DrawerColor.White, 400, "UniqueDrawer", Vector3.zero);
        _spawned.Add(go);

        var src = go.GetComponent<DrawerElement>();
        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);

        var dup = dupGo.GetComponent<DrawerElement>();
        // Копия берёт имя оригинала, а занятость разрешает ElementNaming суффиксом «_N».
        Assert.AreEqual("UniqueDrawer_1", dup.PartName);
        Assert.AreNotEqual(src.PartName, dup.PartName);
    }

    [Test]
    public void Duplicate_Drawer_PreservesMaterial()
    {
        // CreateDrawer ставит материал по цвету; если пользователь
        // переопределил материал — дубликат должен его сохранить.
        var go = ElementFactory.CreateDrawer(DrawerType.C, 450, DrawerColor.Black, 550, "MaterialDrawer", Vector3.zero);
        _spawned.Add(go);

        var src = go.GetComponent<DrawerElement>();
        Assert.AreEqual(DrawerConstants.GetColorMaterialId(DrawerColor.Black), src.MaterialId);

        // Переопределяем на не-цветовой декор.
        MaterialManager.ApplyById(src, "oak");
        Assert.AreEqual("oak", src.MaterialId);

        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);
        var dup = dupGo.GetComponent<DrawerElement>();

        Assert.AreEqual("oak", dup.MaterialId,
            "дубликат ящика должен сохранять материал, даже если он не цветовой");
    }

    [Test]
    public void DestroyElement_NonDrawerStillWorks()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "PlainBoard", Vector3.zero);
        _spawned.Add(go);

        Assert.IsTrue(PartRegistry.GetAll().Exists(e => e.PartName == "PlainBoard"));

        ElementFactory.DestroyElement(go);
        Assert.IsFalse(PartRegistry.GetAll().Exists(e => e.PartName == "PlainBoard"));
    }
}
