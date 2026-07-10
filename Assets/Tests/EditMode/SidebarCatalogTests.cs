using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

public class SidebarCatalogTests
{
    [Test]
    public void Build_HasBoardFacadeAndRoom()
    {
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(3, groups.Count);
        Assert.AreEqual("детали", groups[0].title);
        Assert.AreEqual("Фасады", groups[1].title);
        Assert.AreEqual("Помещение", groups[2].title);
    }

    [Test]
    public void BoardGroup_HasTenItems_FiveEachThickness()
    {
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(10, groups[0].items.Count);
        int count16 = 0, count18 = 0;
        foreach (var it in groups[0].items)
        {
            if (it.dims.z == 16) count16++;
            if (it.dims.z == 18) count18++;
        }
        Assert.AreEqual(5, count16, "5 деталей толщиной 16 мм");
        Assert.AreEqual(5, count18, "5 деталей толщиной 18 мм");
    }

    [Test]
    public void FacadeGroup_HasPlainAndAssembled_Thickness18()
    {
        var groups = SidebarCatalog.Build();

        // 2 обычных фасада + 2 сборных.
        Assert.AreEqual(4, groups[1].items.Count);
        foreach (var it in groups[1].items)
        {
            Assert.IsTrue(it.isFacade, "элемент группы «Фасады» помечен как фасад");
            Assert.AreEqual(18, it.dims.z, "толщина фасада 18 мм");
        }
        Assert.AreEqual(2, groups[1].items.FindAll(it => it.isAssembled).Count, "2 сборных фасада");
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
