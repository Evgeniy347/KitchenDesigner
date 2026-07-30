using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class PartDataTests
{
    [Test]
    public void Constructor_Defaults_AreSane()
    {
        var data = new PartData();
        Assert.AreEqual("Board", data.PartName);
        Assert.AreEqual(new Vector3Int(800, 400, 18), data.DimensionsMM);
        Assert.IsTrue(data.Movable);
        Assert.AreEqual(0, data.GroupId);
        Assert.AreEqual(MaterialCatalog.DefaultId, data.MaterialId);
        Assert.AreEqual(0, data.GapLeft);
        Assert.AreEqual(0, data.GapRight);
        Assert.AreEqual(0, data.GapTop);
        Assert.AreEqual(0, data.GapBottom);
        Assert.AreEqual(0, data.GapMM);
        Assert.IsFalse(data.Transparent);
    }

    [Test]
    public void Transparent_DefaultsFalse()
    {
        var data = new PartData();
        Assert.IsFalse(data.Transparent);
    }

    [Test]
    public void Transparent_Setter()
    {
        var data = new PartData();
        data.Transparent = true;
        Assert.IsTrue(data.Transparent);
        data.Transparent = false;
        Assert.IsFalse(data.Transparent);
    }

    [Test]
    public void ClampDimensions_ZeroOrNegative_ClampsToMinimum()
    {
        Assert.AreEqual(new Vector3Int(1, 1, 1), PartData.ClampDimensions(new Vector3Int(0, -5, 0)));
        Assert.AreEqual(new Vector3Int(1, 1, 1), PartData.ClampDimensions(new Vector3Int(0, 0, 0)));
    }

    [Test]
    public void ClampDimensions_PositiveValues_Unchanged()
    {
        Assert.AreEqual(new Vector3Int(800, 400, 18), PartData.ClampDimensions(new Vector3Int(800, 400, 18)));
        Assert.AreEqual(new Vector3Int(1, 2, 3), PartData.ClampDimensions(new Vector3Int(1, 2, 3)));
    }

    [Test]
    public void DimensionsMM_Setter_ClampsAndStores()
    {
        var data = new PartData();
        data.DimensionsMM = new Vector3Int(0, -1, 0);
        Assert.AreEqual(new Vector3Int(1, 1, 1), data.DimensionsMM);
    }

    [Test]
    public void MaterialId_EmptyOrNull_ReturnsDefault()
    {
        var data = new PartData();
        data.MaterialId = null!;
        Assert.AreEqual(MaterialCatalog.DefaultId, data.MaterialId);
        data.MaterialId = "";
        Assert.AreEqual(MaterialCatalog.DefaultId, data.MaterialId);
    }

    [Test]
    public void MaterialId_CustomValue_Stored()
    {
        var data = new PartData();
        data.MaterialId = "oak";
        Assert.AreEqual("oak", data.MaterialId);
    }

    [Test]
    public void PartName_Null_Defaults()
    {
        var data = new PartData();
        data.PartName = null!;
        Assert.AreEqual("Board", data.PartName);
    }

    [Test]
    public void Movable_Setter_Updates()
    {
        var data = new PartData();
        data.Movable = false;
        Assert.IsFalse(data.Movable);
        data.Movable = true;
        Assert.IsTrue(data.Movable);
    }

    [Test]
    public void GroupId_Setter_Updates()
    {
        var data = new PartData();
        data.GroupId = 42;
        Assert.AreEqual(42, data.GroupId);
    }

    [Test]
    public void GapProperties_Default_Zero()
    {
        var data = new PartData();
        Assert.AreEqual(0, data.GapLeft);
        Assert.AreEqual(0, data.GapRight);
        Assert.AreEqual(0, data.GapTop);
        Assert.AreEqual(0, data.GapBottom);
    }

    [Test]
    public void GapProperties_ClampToNonNegative()
    {
        var data = new PartData();
        data.GapLeft = -5;
        Assert.AreEqual(0, data.GapLeft);
        data.GapRight = -1;
        Assert.AreEqual(0, data.GapRight);
        data.GapTop = -100;
        Assert.AreEqual(0, data.GapTop);
        data.GapBottom = -3;
        Assert.AreEqual(0, data.GapBottom);
    }

    [Test]
    public void GapMM_SumOfAllGaps()
    {
        var data = new PartData();
        data.GapLeft = 2;
        data.GapRight = 3;
        data.GapTop = 4;
        data.GapBottom = 5;
        data.GapFront = 6;
        data.GapBack = 7;
        Assert.AreEqual(27, data.GapMM);
    }

    [Test]
    public void SetGap_WritesItsOwnSide()
    {
        var data = new PartData();

        foreach (var side in GapSides.All) data.SetGap(side, 0);
        data.SetGap(GapSide.Front, 5);

        Assert.AreEqual(5, data.GapFront);
        Assert.AreEqual(5, data.GapMM, "записалась ровно одна сторона");
    }

    [Test]
    public void ToString_IncludesNameAndDimensions()
    {
        var data = new PartData();
        data.PartName = "TestBoard";
        data.DimensionsMM = new Vector3Int(600, 400, 18);
        var str = data.ToString();
        Assert.That(str, Does.Contain("TestBoard"));
        Assert.That(str, Does.Contain("600"));
        Assert.That(str, Does.Contain("400"));
        Assert.That(str, Does.Contain("18"));
    }

    [Test]
    public void KitchenElement_ExposesSameData()
    {
        var go = new GameObject("Test");
        var element = go.AddComponent<KitchenElement>();
        element.PartName = "Custom";
        element.DimensionsMM = new Vector3Int(600, 720, 18);
        element.Movable = false;
        element.GroupId = 5;
        element.MaterialId = "cherry";
        element.Transparent = true;

        var data = element.Data;
        Assert.AreEqual("Custom", data.PartName);
        Assert.AreEqual(new Vector3Int(600, 720, 18), data.DimensionsMM);
        Assert.IsFalse(data.Movable);
        Assert.AreEqual(5, data.GroupId);
        Assert.AreEqual("cherry", data.MaterialId);
        Assert.IsTrue(data.Transparent);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void KitchenElement_DelegatesPropertiesToData()
    {
        var go = new GameObject("Test");
        var element = go.AddComponent<KitchenElement>();
        element.Data.PartName = "ViaData";
        element.Data.DimensionsMM = new Vector3Int(400, 300, 16);
        element.Data.Movable = false;
        element.Data.GroupId = 99;
        element.Data.Transparent = true;

        Assert.AreEqual("ViaData", element.PartName);
        Assert.AreEqual(new Vector3Int(400, 300, 16), element.DimensionsMM);
        Assert.IsFalse(element.Movable);
        Assert.AreEqual(99, element.GroupId);
        Assert.IsTrue(element.Transparent);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FacadeElement_UsesPartDataGaps()
    {
        var go = new GameObject("Facade");
        var facade = go.AddComponent<FacadeElement>();
        facade.Data.GapLeft = 3;
        facade.Data.GapRight = 4;
        facade.Data.GapTop = 5;
        facade.Data.GapBottom = 6;

        Assert.AreEqual(3, facade.GapLeft);
        Assert.AreEqual(4, facade.GapRight);
        Assert.AreEqual(5, facade.GapTop);
        Assert.AreEqual(6, facade.GapBottom);
        Assert.AreEqual(18, facade.GapMM);

        Object.DestroyImmediate(go);
    }
}
