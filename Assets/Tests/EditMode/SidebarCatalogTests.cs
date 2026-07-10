using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

public class SidebarCatalogTests
{
    [Test]
    public void Build_HasTwoThicknessGroups_AndRoom()
    {
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(3, groups.Count);
        Assert.AreEqual("16 мм", groups[0].title);
        Assert.AreEqual("18 мм", groups[1].title);
        Assert.AreEqual("Помещение", groups[2].title);
    }

    [Test]
    public void ThicknessGroups_HaveFiveSizes_WithCorrectThickness()
    {
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(5, groups[0].items.Count);
        Assert.AreEqual(5, groups[1].items.Count);
        foreach (var it in groups[0].items)
            Assert.AreEqual(16, it.dims.z, "группа «16 мм» — толщина 16");
        foreach (var it in groups[1].items)
            Assert.AreEqual(18, it.dims.z, "группа «18 мм» — толщина 18");
    }

    [Test]
    public void Room_ContainsKorob_600Cube()
    {
        var groups = SidebarCatalog.Build();
        var korob = groups[2].items[0];

        Assert.AreEqual("Короб", korob.name);
        Assert.AreEqual(new Vector3Int(600, 600, 600), korob.dims);
        Assert.IsFalse(korob.isWall);
    }

    [Test]
    public void Room_ContainsWall_MarkedAsWall()
    {
        var groups = SidebarCatalog.Build();
        var wall = groups[2].items.Find(it => it.name == "Стена");

        Assert.IsTrue(wall.isWall, "элемент «Стена» помечен как стена");
        Assert.AreEqual(new Vector3Int(2000, 2500, 100), wall.dims);
    }
}
