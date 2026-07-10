using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

public class SidebarCatalogTests
{
    [Test]
    public void Build_HasBoardFacadeDrawerAndRoom()
    {
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(4, groups.Count);
        Assert.AreEqual("детали", groups[0].title);
        Assert.AreEqual("Фасады", groups[1].title);
        Assert.AreEqual("Ящики GTV", groups[2].title);
        Assert.AreEqual("Помещение", groups[3].title);
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

        Assert.AreEqual(4, groups[1].items.Count);
        foreach (var it in groups[1].items)
        {
            Assert.IsTrue(it.isFacade, "элемент группы «Фасады» помечен как фасад");
            Assert.AreEqual(18, it.dims.z, "толщина фасада 18 мм");
        }
        Assert.AreEqual(2, groups[1].items.FindAll(it => it.isAssembled).Count, "2 сборных фасада");
    }

    [Test]
    public void DrawerGroup_HasOneDefaultItem()
    {
        var groups = SidebarCatalog.Build();
        Assert.AreEqual(1, groups[2].items.Count);
        var it = groups[2].items[0];
        Assert.IsTrue(it.isDrawer);
        Assert.AreEqual("A", it.drawerType);
        Assert.AreEqual(350, it.drawerLength);
    }

    [Test]
    public void Room_ContainsKorob_600Cube()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[3];
        var korob = room.items[0];

        Assert.AreEqual("Короб", korob.name);
        Assert.AreEqual(new Vector3Int(600, 600, 600), korob.dims);
        Assert.IsFalse(korob.isWall);
    }

    [Test]
    public void Room_ContainsWall_MarkedAsWall()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[3];
        var wall = room.items.Find(it => it.name == "Стена");

        Assert.IsTrue(wall.isWall, "элемент «Стена» помечен как стена");
        Assert.AreEqual(new Vector3Int(2000, 2500, 100), wall.dims);
    }

    [Test]
    public void Build_Room_ContainsRoomSettings()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[3];
        var settings = room.items.Find(it => it.name == "Размеры помещения");
        Assert.IsNotNull(settings);
    }
}
