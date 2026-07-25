using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class SidebarCatalogTests
{
    [Test]
    public void Build_HasFiveGroups()
    {
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(5, groups.Count);
        Assert.AreEqual("детали", groups[0].title);
        Assert.AreEqual("Фасады", groups[1].title);
        Assert.AreEqual("Ящики", groups[2].title);
        Assert.AreEqual("Мебель", groups[3].title);
        Assert.AreEqual("Помещение", groups[4].title);
    }

    [Test]
    public void BoardGroup_HasShelfRadialShelfAndPanel()
    {
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(3, groups[0].items.Count);
        var regular = groups[0].items[0];
        Assert.AreEqual("Полка", regular.name);
        Assert.AreEqual(new Vector3Int(600, 400, 16), regular.dims);
        Assert.IsFalse(regular.isRadialShelf);
        Assert.IsFalse(regular.isPanel);

        var radial = groups[0].items[1];
        Assert.AreEqual("Радиусная полка", radial.name);
        Assert.AreEqual(new Vector3Int(600, 400, 16), radial.dims);
        Assert.IsTrue(radial.isRadialShelf);
    }

    [Test]
    public void BoardGroup_PanelItem_IsThinWithOneMillimetreGaps()
    {
        var panel = SidebarCatalog.Build()[0].items[2];

        Assert.AreEqual("ДВП/ХДФ", panel.name);
        Assert.IsTrue(panel.isPanel);
        Assert.IsFalse(panel.isFacade, "ДВП не фасад — она не открывается");
        Assert.AreEqual(3, panel.dims.z, "тонкая панель");
        // Технологический зазор: в паз заходит номинал, зазор остаётся в детали.
        Assert.AreEqual(PanelElement.DEFAULT_GAP_MM, panel.gapLeft);
        Assert.AreEqual(PanelElement.DEFAULT_GAP_MM, panel.gapRight);
        Assert.AreEqual(PanelElement.DEFAULT_GAP_MM, panel.gapTop);
        Assert.AreEqual(PanelElement.DEFAULT_GAP_MM, panel.gapBottom);
    }

    [Test]
    public void FacadeGroup_HasPlainAndAssembled_Thickness18()
    {
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(2, groups[1].items.Count);
        foreach (var it in groups[1].items)
        {
            Assert.IsTrue(it.isFacade, "элемент группы «Фасады» помечен как фасад");
            Assert.AreEqual(18, it.dims.z, "толщина фасада 18 мм");
        }

        var plain = groups[1].items.Find(it => it.name == "Фасад щитовой");
        Assert.IsFalse(plain.isAssembled, "щитовой фасад — не сборный");
        Assert.AreEqual(new Vector3Int(600, 716, 18), plain.dims, "размеры щитового по умолчанию");

        var assembled = groups[1].items.Find(it => it.name == "Фасад сборный");
        Assert.IsTrue(assembled.isAssembled, "сборный фасад помечен как сборный");
        Assert.AreEqual(new Vector3Int(600, 716, 18), assembled.dims);

        Assert.AreEqual(1, groups[1].items.FindAll(it => it.isAssembled).Count, "1 сборный фасад");
    }

    [Test]
    public void DrawerGroup_HasGtvAndMoventoItems()
    {
        var groups = SidebarCatalog.Build();
        Assert.AreEqual(2, groups[2].items.Count, "два ящика: GTV и Movento");

        var gtv = groups[2].items[0];
        Assert.IsTrue(gtv.isDrawer);
        Assert.AreEqual("gtv", gtv.drawerSystem);
        Assert.AreEqual("A", gtv.drawerType);
        Assert.AreEqual(350, gtv.drawerLength);

        var movento = groups[2].items[1];
        Assert.IsTrue(movento.isDrawer);
        Assert.AreEqual("movento", movento.drawerSystem);
        Assert.AreEqual("Ящик Movento", movento.name);
    }

    [Test]
    public void FurnitureGroup_HasTable()
    {
        var groups = SidebarCatalog.Build();
        Assert.AreEqual(3, groups[3].items.Count);
        var it = groups[3].items.Find(i => i.name == "Прямоугольный стол");
        Assert.IsNotNull(it);
        Assert.IsTrue(it.isFurniture);
        Assert.AreEqual(new Vector3Int(2000, 750, 1000), it.dims);
    }

    [Test]
    public void FurnitureGroup_HasRadiusTable()
    {
        var groups = SidebarCatalog.Build();
        var it = groups[3].items.Find(i => i.name == "Радиусный стол");
        Assert.IsNotNull(it);
        Assert.IsTrue(it.isRadiusTable);
        Assert.AreEqual(new Vector3Int(2000, 750, 1000), it.dims);
    }

    [Test]
    public void FurnitureGroup_HasPillar()
    {
        var groups = SidebarCatalog.Build();
        var it = groups[3].items.Find(i => i.name == "Ножка");
        Assert.IsNotNull(it);
        Assert.IsTrue(it.isPillar);
        Assert.AreEqual(PillarElement.MidHeightMM_Default, it.pillarMidHeightMM);
    }

    [Test]
    public void Room_ContainsKorob_600Cube()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[4];
        var korob = room.items[0];

        Assert.AreEqual("Короб", korob.name);
        Assert.AreEqual(new Vector3Int(600, 600, 600), korob.dims);
        Assert.IsFalse(korob.isWall);
    }

    [Test]
    public void Room_ContainsWall_MarkedAsWall()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[4];
        var wall = room.items.Find(it => it.name == "Стена");

        Assert.IsTrue(wall.isWall, "элемент «Стена» помечен как стена");
        Assert.AreEqual(new Vector3Int(2000, 2500, 100), wall.dims);
    }

    [Test]
    public void Build_Room_DoesNotContainRoomSettings()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[4];
        Assert.IsFalse(room.items.Exists(it => it.name == "Размеры помещения"),
            "пункт «Размеры помещения» удалён: пол теперь отдельный элемент");
    }

    [Test]
    public void Room_ContainsFloor_MarkedAsFloor()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[4];
        var floor = room.items.Find(it => it.name == "Пол");

        Assert.IsTrue(floor.isFloor, "элемент «Пол» помечен как пол");
        Assert.AreEqual(new Vector3Int(
            FloorElement.DEFAULT_SIZE_MM,
            FloorElement.DEFAULT_THICKNESS_MM,
            FloorElement.DEFAULT_SIZE_MM), floor.dims);
    }

    [Test]
    public void Room_ContainsLightSource_MarkedAsLightSource()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[4];
        var lamp = room.items.Find(it => it.name == "Источник света");

        Assert.IsTrue(lamp.isLightSource, "элемент «Источник света» помечен как источник света");
    }
}
