using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class SidebarCatalogTests
{
    /// <summary>Индекс группы «Помещение»: она последняя, и её номер сдвигается
    /// каждый раз, когда перед ней появляется новая группа.</summary>
    private const int RoomIndex = 5;

    [Test]
    public void Build_HasSixGroups()
    {
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(6, groups.Count);
        Assert.AreEqual("детали", groups[0].title);
        Assert.AreEqual("Фасады", groups[1].title);
        Assert.AreEqual("Ящики", groups[2].title);
        Assert.AreEqual("Мебель", groups[3].title);
        Assert.AreEqual("Техника", groups[4].title);
        Assert.AreEqual("Помещение", groups[RoomIndex].title);
    }

    [Test]
    public void ApplianceGroup_ShortLabelIsT()
    {
        var groups = SidebarCatalog.Build();
        Assert.AreEqual("Т", groups[4].shortLabel, "свёрнутый сайдбар подписывает «Технику» буквой Т");
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
        Assert.AreEqual(10, groups[3].items.Count,
            "девятой в «Мебели» встала кровать, десятым — пуфик");
        var it = groups[3].items.Find(i => i.name == "Прямоугольный стол");
        Assert.IsNotNull(it);
        Assert.IsTrue(it.isFurniture);
        Assert.AreEqual(new Vector3Int(2000, 750, 1000), it.dims);
    }

    [Test]
    public void FurnitureGroup_HasChair()
    {
        var groups = SidebarCatalog.Build();
        var it = groups[3].items.Find(i => i.name == "Стул");
        Assert.IsNotNull(it, "стул обязан быть в сайдбаре: иначе завести его можно только "
            + "через MCP");
        Assert.IsTrue(it.isChair,
            "и именно флагом стула: SidebarUI ветвится по этим флагам, и стул с флагом "
            + "табуретки завёлся бы табуреткой");
        Assert.AreEqual(new Vector3Int(ChairElement.DefaultWidthMM,
            ChairElement.DefaultHeightMM, ChairElement.DefaultDepthMM), it.dims,
            "габариты стула в каталоге обязаны совпадать с его собственными значениями "
            + "по умолчанию, иначе сайдбар и MCP заводят разные стулья");
    }

    [Test]
    public void FurnitureGroup_HasSofa()
    {
        var groups = SidebarCatalog.Build();
        var it = groups[3].items.Find(i => i.name == "Диван");
        Assert.IsNotNull(it, "диван обязан быть в сайдбаре: иначе завести его можно только "
            + "через MCP");
        Assert.IsTrue(it.isSofa,
            "и именно флагом дивана: SidebarUI ветвится по этим флагам, и диван с флагом "
            + "стула завёлся бы стулом — без подушек и без основания");
        Assert.AreEqual(new Vector3Int(SofaElement.DefaultWidthMM,
            SofaElement.DefaultHeightMM, SofaElement.DefaultDepthMM), it.dims,
            "габариты дивана в каталоге обязаны совпадать с его собственными значениями "
            + "по умолчанию, иначе сайдбар и MCP заводят разные диваны");
    }

    [Test]
    public void FurnitureGroup_HasBed()
    {
        var groups = SidebarCatalog.Build();
        var it = groups[3].items.Find(i => i.name == "Кровать");
        Assert.IsNotNull(it, "кровать обязана быть в сайдбаре: иначе завести её можно "
            + "только через MCP");
        Assert.IsTrue(it.isBed,
            "и именно флагом кровати: SidebarUI ветвится по этим флагам, и кровать с чужим "
            + "флагом завелась бы другим типом — без матраса, подушек и спинки");
        Assert.AreEqual(new Vector3Int(BedElement.DefaultWidthMM,
            BedElement.DefaultHeightMM, BedElement.DefaultDepthMM), it.dims,
            "габариты кровати в каталоге обязаны совпадать с её собственными значениями "
            + "по умолчанию, иначе сайдбар и MCP заводят разные кровати");
    }

    [Test]
    public void FurnitureGroup_HasPouffe()
    {
        var groups = SidebarCatalog.Build();
        var it = groups[3].items.Find(i => i.name == "Пуфик");
        Assert.IsNotNull(it, "пуфик обязан быть в сайдбаре: иначе завести его можно "
            + "только через MCP");
        Assert.IsTrue(it.isPouffe,
            "и именно своим флагом: SidebarUI ветвится по этим флагам, и пуфик с чужим "
            + "флагом завёлся бы табуреткой — на ножках и с жёстким сиденьем");
        Assert.AreEqual(new Vector3Int(PouffeElement.DefaultWidthMM,
            PouffeElement.DefaultHeightMM, PouffeElement.DefaultDepthMM), it.dims,
            "габариты пуфика в каталоге обязаны совпадать с его собственными значениями "
            + "по умолчанию, иначе сайдбар и MCP заводят разные пуфики");
    }

    [Test]
    public void FurnitureGroup_HasNoCooktop()
    {
        var groups = SidebarCatalog.Build();
        Assert.IsFalse(groups[3].items.Exists(i => i.name == "Варочная поверхность"),
            "варочная переехала в «Технику» — в «Мебели» её быть не должно");
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
    public void FurnitureGroup_HasStool()
    {
        var groups = SidebarCatalog.Build();
        var it = groups[3].items.Find(i => i.name == "Табуретка");
        Assert.IsNotNull(it);
        Assert.IsTrue(it.isStool);
        Assert.AreEqual(new Vector3Int(StoolElement.DefaultWidthMM,
            StoolElement.DefaultHeightMM, StoolElement.DefaultDepthMM), it.dims,
            "габариты табуретки в каталоге обязаны совпадать с её собственными "
            + "значениями по умолчанию, иначе сайдбар и MCP заводят разные табуретки");
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
    public void FurnitureGroup_HasSink()
    {
        var groups = SidebarCatalog.Build();
        var it = groups[3].items.Find(i => i.name == "Мойка");
        Assert.IsTrue(it.isSink, "элемент «Мойка» помечен как мойка");
        Assert.AreEqual(new Vector3Int(
            SinkElement.OUTER_WIDTH_MM, SinkElement.TotalHeightMM, SinkElement.OUTER_DEPTH_MM), it.dims);
    }

    [Test]
    public void Room_ContainsKorob_600Cube()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[RoomIndex];
        var korob = room.items[0];

        Assert.AreEqual("Короб", korob.name);
        Assert.AreEqual(new Vector3Int(600, 600, 600), korob.dims);
        Assert.IsFalse(korob.isWall);
    }

    [Test]
    public void Room_ContainsWall_MarkedAsWall()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[RoomIndex];
        var wall = room.items.Find(it => it.name == "Стена");

        Assert.IsTrue(wall.isWall, "элемент «Стена» помечен как стена");
        Assert.AreEqual(new Vector3Int(2000, 2500, 100), wall.dims);
    }

    [Test]
    public void Build_Room_DoesNotContainRoomSettings()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[RoomIndex];
        Assert.IsFalse(room.items.Exists(it => it.name == "Размеры помещения"),
            "пункт «Размеры помещения» удалён: пол теперь отдельный элемент");
    }

    [Test]
    public void Room_ContainsFloor_MarkedAsFloor()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[RoomIndex];
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
        var room = groups[RoomIndex];
        var lamp = room.items.Find(it => it.name == "Источник света");

        Assert.IsTrue(lamp.isLightSource, "элемент «Источник света» помечен как источник света");
    }

    /// <summary>Высота кнопки палитры обязана вмещать все строки её названия.
    /// Пока высота была жёстко 26 px, длинные имена техники переносились по
    /// словам и рисовались ЗА кнопкой, налезая на соседний пункт.</summary>
    [Test]
    public void ItemHeight_FitsEveryCatalogName()
    {
        foreach (var g in SidebarCatalog.Build())
            foreach (var it in g.items)
            {
                float need = SidebarUI.ItemLines(it.name) * SidebarUI.ItemFont * DropdownItemFit.LineHeightFactor;
                Assert.GreaterOrEqual(SidebarUI.ItemHeight(it.name), need,
                    $"пункт «{it.name}» ниже своего текста — вторая строка вылезет наружу");
            }
    }

    [Test]
    public void ItemHeight_LongApplianceNamesTakeTwoRows()
    {
        // Имена с моделью (Bosch) длинные и обязаны переноситься и быть выше
        // однострочных; общая «Варочная поверхность» помещается в одну строку.
        foreach (var name in SidebarCatalog.Build()[4].items.ConvertAll(it => it.name))
        {
            bool longName = name.Length > 20;
            if (longName)
            {
                Assert.AreEqual(2, SidebarUI.ItemLines(name), $"«{name}» не влезает в одну строку панели");
                Assert.Greater(SidebarUI.ItemHeight(name), 26f, $"кнопка «{name}» должна быть выше однострочной");
            }
            else
            {
                Assert.AreEqual(1, SidebarUI.ItemLines(name), $"короткое «{name}» в одну строку");
                Assert.AreEqual(26f, SidebarUI.ItemHeight(name), $"короткая кнопка «{name}» однострочная");
            }
        }
    }

    [Test]
    public void ItemHeight_ShortNameStaysSingleRow()
    {
        Assert.AreEqual(1, SidebarUI.ItemLines("Полка"));
        Assert.AreEqual(26f, SidebarUI.ItemHeight("Полка"), "короткое имя не делает список выше");
    }

    [Test]
    public void ApplianceGroup_ItemsInOrder_GenericCooktop_Model_Oven_Dishwasher()
    {
        var items = SidebarCatalog.Build()[4].items;

        Assert.AreEqual(4, items.Count);
        Assert.IsTrue(items[0].isCooktop);
        Assert.AreEqual("", items[0].applianceModel,
            "варочная свободного размера идёт первой и модели не имеет: её габариты "
            + "пользователь правит сам");
        Assert.AreEqual(CooktopElement.MODEL_BOSCH_PUE611BB5E, items[1].applianceModel);
        Assert.AreEqual(OvenElement.MODEL, items[2].applianceModel);
        Assert.AreEqual(DishwasherElement.MODEL, items[3].applianceModel);
    }

    [Test]
    public void ApplianceGroup_ModelSizes_ComeFromTheElement_NotFromTheCatalog()
    {
        var items = SidebarCatalog.Build()[4].items;

        Assert.AreEqual(CooktopElement.ModelDimensionsMM(CooktopElement.MODEL_BOSCH_PUE611BB5E),
            items[1].dims);
        Assert.AreEqual(OvenElement.ModelDimensionsMM, items[2].dims);
        Assert.AreEqual(DishwasherElement.ModelDimensionsMM, items[3].dims,
            "габариты готовой модели берутся у производителя (IFixedSizeElement): "
            + "своя копия чисел в каталоге разошлась бы с элементом, а поля Ш/В/Г "
            + "в окне свойств у такого прибора серые и исправить расхождение нечем");
    }

    [Test]
    public void ApplianceModels_AndCatalogItems_ListTheSameModels()
    {
        var inCatalog = new System.Collections.Generic.List<string>();
        foreach (var g in SidebarCatalog.Build())
            foreach (var it in g.items)
                if (!string.IsNullOrEmpty(it.applianceModel)) inCatalog.Add(it.applianceModel);

        var missingInCatalog = new System.Collections.Generic.List<string>();
        foreach (var model in ApplianceModels.All)
            if (!inCatalog.Contains(model)) missingInCatalog.Add(model);

        var unknownModels = new System.Collections.Generic.List<string>();
        foreach (var model in inCatalog)
            if (!ApplianceModels.IsKnown(model)) unknownModels.Add(model);

        Assert.IsEmpty(missingInCatalog,
            "модель есть в ApplianceModels.All, но её не добавить из сайдбара — "
            + "прибор существует и не заказывается: " + string.Join(", ", missingInCatalog));
        Assert.IsEmpty(unknownModels,
            "пункт каталога ссылается на модель, которой нет в ApplianceModels.All — "
            + "фиксированный размер для неё не сработает: " + string.Join(", ", unknownModels));
    }
}
