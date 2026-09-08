using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class SidebarCatalogTests
{
    /// <summary>Индекс группы «Помещение»: она последняя, и её номер сдвигается
    /// каждый раз, когда перед ней появляется новая группа.</summary>
    private const int RoomIndex = 6;

    /// <summary>Индекс группы «Сантехника»: заведена унитазами, дальше в неё
    /// лягут ванна, смеситель и душ соседних сессий.</summary>
    private const int SanitaryIndex = 5;

    [Test]
    public void Build_HasSevenGroups()
    {
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(7, groups.Count,
            "шестой встала «Сантехника» — перед «Помещением», чтобы комната осталась последней");
        Assert.AreEqual("детали", groups[0].title);
        Assert.AreEqual("Фасады", groups[1].title);
        Assert.AreEqual("Ящики", groups[2].title);
        Assert.AreEqual("Мебель", groups[3].title);
        Assert.AreEqual("Техника", groups[4].title);
        Assert.AreEqual("Сантехника", groups[SanitaryIndex].title);
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
        Assert.IsFalse(regular.kind == SidebarItemKind.RadialShelf);
        Assert.IsFalse(regular.kind == SidebarItemKind.Panel);

        var radial = groups[0].items[1];
        Assert.AreEqual("Радиусная полка", radial.name);
        Assert.AreEqual(new Vector3Int(600, 400, 16), radial.dims);
        Assert.IsTrue(radial.kind == SidebarItemKind.RadialShelf);
    }

    [Test]
    public void BoardGroup_PanelItem_IsThin()
    {
        var panel = SidebarCatalog.Build()[0].items[2];

        Assert.AreEqual("ДВП/ХДФ", panel.name);
        Assert.IsTrue(panel.kind == SidebarItemKind.Panel);
        Assert.IsFalse(panel.kind == SidebarItemKind.Facade
            || panel.kind == SidebarItemKind.AssembledFacade, "ДВП не фасад — она не открывается");
        Assert.AreEqual(3, panel.dims.z, "тонкая панель");

        // Технологический зазор (в паз заходит номинал, зазор остаётся в детали) задан
        // ОДИН раз — на PanelElement.DEFAULT_GAP_MM, и приезжает через умолчание фабрики.
        // Копии этого числа в каталоге были мертвы у сборного фасада и живы у щитового:
        // одно и то же поле то доезжало, то нет. Что зазор доезжает до готовой панели,
        // держит FacadeFloorSinkReproTests; что каталог не заводит собственных чисел,
        // держит SidebarSpawnRouterTests.EveryFieldOfACatalogItem_ReachesTheSpawner.
    }

    [Test]
    public void FacadeGroup_HasPlainAndAssembled_Thickness18()
    {
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(2, groups[1].items.Count);
        foreach (var it in groups[1].items)
        {
            Assert.IsTrue(it.kind == SidebarItemKind.Facade || it.kind == SidebarItemKind.AssembledFacade,
                "элемент группы «Фасады» помечен как фасад");
            Assert.AreEqual(18, it.dims.z, "толщина фасада 18 мм");
        }

        var plain = groups[1].items.Find(it => it.name == "Фасад щитовой");
        Assert.IsFalse(plain.kind == SidebarItemKind.AssembledFacade, "щитовой фасад — не сборный");
        Assert.AreEqual(new Vector3Int(600, 716, 18), plain.dims, "размеры щитового по умолчанию");

        var assembled = groups[1].items.Find(it => it.name == "Фасад сборный");
        Assert.IsTrue(assembled.kind == SidebarItemKind.AssembledFacade, "сборный фасад помечен как сборный");
        Assert.AreEqual(new Vector3Int(600, 716, 18), assembled.dims);

        Assert.AreEqual(1, groups[1].items.FindAll(it => it.kind == SidebarItemKind.AssembledFacade).Count, "1 сборный фасад");
    }

    [Test]
    public void DrawerGroup_HasGtvAndMoventoItems()
    {
        var groups = SidebarCatalog.Build();
        Assert.AreEqual(2, groups[2].items.Count, "два ящика: GTV и Movento");

        var gtv = groups[2].items[0];
        Assert.IsTrue(gtv.kind == SidebarItemKind.Drawer);
        Assert.AreEqual("gtv", gtv.drawerSystem);
        Assert.AreEqual("A", gtv.drawerType);
        Assert.AreEqual(350, gtv.drawerLength);

        var movento = groups[2].items[1];
        Assert.IsTrue(movento.kind == SidebarItemKind.Drawer);
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
        Assert.IsTrue(it.kind == SidebarItemKind.Table);
        Assert.AreEqual(new Vector3Int(2000, 750, 1000), it.dims);
    }

    [Test]
    public void FurnitureGroup_HasChair()
    {
        var groups = SidebarCatalog.Build();
        var it = groups[3].items.Find(i => i.name == "Стул");
        Assert.IsNotNull(it, "стул обязан быть в сайдбаре: иначе завести его можно только "
            + "через MCP");
        Assert.IsTrue(it.kind == SidebarItemKind.Chair,
            "и именно видом «стул»: SidebarSpawnRouter ветвится по kind, и стул с видом "
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
        Assert.IsTrue(it.kind == SidebarItemKind.Sofa,
            "и именно видом «диван»: SidebarSpawnRouter ветвится по kind, и диван с видом "
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
        Assert.IsTrue(it.kind == SidebarItemKind.Bed,
            "и именно видом «кровать»: SidebarSpawnRouter ветвится по kind, и кровать с чужим "
            + "видом завелась бы другим типом — без матраса, подушек и спинки");
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
        Assert.IsTrue(it.kind == SidebarItemKind.Pouffe,
            "и именно своим видом: SidebarSpawnRouter ветвится по kind, и пуфик с чужим "
            + "видом завёлся бы табуреткой — на ножках и с жёстким сиденьем");
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
        Assert.IsTrue(it.kind == SidebarItemKind.RadiusTable);
        Assert.AreEqual(new Vector3Int(2000, 750, 1000), it.dims);
    }

    [Test]
    public void FurnitureGroup_HasStool()
    {
        var groups = SidebarCatalog.Build();
        var it = groups[3].items.Find(i => i.name == "Табуретка");
        Assert.IsNotNull(it);
        Assert.IsTrue(it.kind == SidebarItemKind.Stool);
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
        Assert.IsTrue(it.kind == SidebarItemKind.Pillar);
        Assert.AreEqual(PillarElement.MidHeightMM_Default, it.pillarMidHeightMM);
    }

    [Test]
    public void FurnitureGroup_HasSink()
    {
        var groups = SidebarCatalog.Build();
        var it = groups[3].items.Find(i => i.name == "Мойка");
        Assert.IsTrue(it.kind == SidebarItemKind.Sink, "элемент «Мойка» помечен как мойка");
        Assert.AreEqual(new Vector3Int(
            SinkElement.OUTER_WIDTH_MM, SinkElement.TotalHeightMM, SinkElement.OUTER_DEPTH_MM), it.dims);
    }

    [Test]
    public void SanitaryGroup_ShortLabelIsS()
    {
        var groups = SidebarCatalog.Build();
        Assert.AreEqual("С", groups[SanitaryIndex].shortLabel,
            "свёрнутый сайдбар подписывает группу одной буквой, и «С» не занята: "
            + "Д, Ф, Я, М, Т, П");
    }

    [Test]
    public void SanitaryGroup_HasBothToilets_WithTheirOwnKinds()
    {
        var items = SidebarCatalog.Build()[SanitaryIndex].items;

        Assert.IsTrue(items.Count >= 2,
            "оба унитаза обязаны быть в группе. Пересчёта ВСЕЙ группы здесь нет намеренно: "
            + "он краснел на каждом новом предмете сантехники (ванна сломала его первой), "
            + "и такой тест перестаёт значить что-либо, потому что его чинят цифрой");

        var compact = items.Find(i => i.name == "Унитаз");
        Assert.IsTrue(compact.kind == SidebarItemKind.Toilet,
            "напольный унитаз обязан нести свой вид: с чужим он завёлся бы другим объектом, "
            + "а маршрутизатор не отказывает, он просто зовёт другой спаун");
        Assert.AreEqual(ToiletElement.ModelDimensionsMM, compact.dims,
            "габарит в каталоге обязан совпадать с собственным габаритом типа, иначе "
            + "сайдбар и MCP заводят разные унитазы");

        var wallHung = items.Find(i => i.name == "Инсталляция");
        Assert.IsTrue(wallHung.kind == SidebarItemKind.WallHungToilet,
            "подвесной унитаз обязан нести свой вид, а не вид напольного: у них разные "
            + "габариты и разное поведение у стены");
        Assert.AreEqual(WallHungToiletElement.ModelDimensionsMM, wallHung.dims,
            "то же для подвесного");
    }

    [Test]
    public void SanitaryGroup_GivesEveryItemItsOwnKind()
    {
        var items = SidebarCatalog.Build()[SanitaryIndex].items;
        var seen = new Dictionary<SidebarItemKind, string>();

        foreach (var item in items)
        {
            string clash = seen.TryGetValue(item.kind, out var other) ? other : "";
            Assert.IsEmpty(clash,
                "два предмета сантехники с одним видом — «" + item.name + "» и «" + clash
                + "». Маршрутизатор на это не отказывает, он просто зовёт спаун чужого "
                + "вида, и кнопка заводит не тот объект. Это и есть свойство, ради "
                + "которого тест существует, и оно не про КОЛИЧЕСТВО предметов в группе");
            seen[item.kind] = item.name;
        }
    }

    [Test]
    public void SanitaryGroup_HasTheWallMixerAndTheShowerColumn()
    {
        var items = SidebarCatalog.Build()[SanitaryIndex].items;

        var mixer = items.Find(i => i.name == "Смеситель");
        Assert.IsTrue(mixer.kind == SidebarItemKind.BathMixer, "смеситель обязан нести свой вид");
        Assert.AreEqual(BathMixerLayout.DimensionsMM(BathMixerSpec.Default), mixer.dims,
            "габарит в каталоге обязан быть ВЫЧИСЛЕННЫМ из умолчательной раскладки: "
            + "у смесителя размер выводится из формы, и вписанное руками число разошлось "
            + "бы с тем, что реально заводит фабрика, при первой же правке умолчаний");

        var column = items.Find(i => i.name == "Душевая стойка");
        Assert.IsTrue(column.kind == SidebarItemKind.ShowerColumn, "душевая стойка обязана нести свой вид");
        Assert.AreEqual(ShowerColumnLayout.DimensionsMM(ShowerColumnSpec.Default), column.dims,
            "то же для стойки, и тут расхождение было бы особенно грубым: её габарит на "
            + "четверть метра выше самой стойки из-за петли шланга");
    }

    [Test]
    public void SanitaryGroup_DoesNotStealTheSinkFromTheFurnitureGroup()
    {
        var groups = SidebarCatalog.Build();

        Assert.IsFalse(groups[SanitaryIndex].items.Exists(i => i.name == "Мойка"),
            "мойка осталась в «Мебели» намеренно: она кухонная, а не сантехника этой "
            + "группы, и её переезд сдвинул бы чужие кнопки без единой просьбы");
        Assert.IsTrue(groups[3].items.Exists(i => i.name == "Мойка"),
            "и она обязана остаться там, где была");
    }

    [Test]
    public void Room_ContainsKorob_600Cube()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[RoomIndex];
        var korob = room.items[0];

        Assert.AreEqual("Короб", korob.name);
        Assert.AreEqual(new Vector3Int(600, 600, 600), korob.dims);
        Assert.IsFalse(korob.kind == SidebarItemKind.Wall);
    }

    [Test]
    public void Room_ContainsWall_MarkedAsWall()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[RoomIndex];
        var wall = room.items.Find(it => it.name == "Стена");

        Assert.IsTrue(wall.kind == SidebarItemKind.Wall, "элемент «Стена» помечен как стена");
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

        Assert.IsTrue(floor.kind == SidebarItemKind.Floor, "элемент «Пол» помечен как пол");
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

        Assert.IsTrue(lamp.kind == SidebarItemKind.LightSource, "элемент «Источник света» помечен как источник света");
    }

    /// <summary>Розетка и выключатель — электрика, и живут они в «Помещении»
    /// рядом с источником света, а не в сантехнике. Габарит обязан быть
    /// ВЫЧИСЛЕННЫМ из умолчательной спецификации: размер у обоих выводится из
    /// формы, и вписанное руками число разошлось бы с тем, что реально заводит
    /// фабрика, при первой же правке умолчаний.</summary>
    [Test]
    public void Room_ContainsTheSocketAndTheSwitch_EachWithItsOwnKind()
    {
        var room = SidebarCatalog.Build()[RoomIndex];

        var socket = room.items.Find(it => it.name == "Розетка");
        var lightSwitch = room.items.Find(it => it.name == "Выключатель");

        Assert.IsTrue(socket.kind == SidebarItemKind.Socket, "розетка обязана нести свой вид");
        Assert.IsTrue(lightSwitch.kind == SidebarItemKind.LightSwitch, "выключатель обязан нести свой вид");
        Assert.AreNotEqual(socket.kind, lightSwitch.kind,
            "два вида на одну кнопку маршрутизатор не отвергает — он просто зовёт чужой "
            + "спаун, и кнопка заводит не тот объект");
        Assert.AreEqual(WallDeviceSpec.Default.DimensionsMM, socket.dims,
            "габарит розетки в каталоге обязан быть вычисленным из умолчаний");
        Assert.AreEqual(WallDeviceSpec.Default.DimensionsMM, lightSwitch.dims,
            "габарит выключателя — тем же порядком");
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
        Assert.IsTrue(items[0].kind == SidebarItemKind.Cooktop);
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

    /// <summary>Страж против возврата 30 булевых свойств `is*` (`SidebarCatalog.cs`
    /// держал их до правки). Ловушка была в том, что новый вид
    /// (`SidebarItemKind`) требовал ещё одной строки-свойства, и найти это
    /// перечислением по прошлому реестру не удавалось: свойства называются со
    /// строчной буквы и не попадают в обычный поиск по типам.
    ///
    /// Это МЕХАНИЗМ, а не список имён: рефлексия перебирает ВСЕ публичные
    /// свойства <see cref="SidebarCatalog.Item"/> и ловит любое, чьё имя начинается
    /// с «is» + заглавная буква — тем же шаблоном, каким были названы все 30
    /// удалённых (`isWall`, `isPipe`, …). Новое свойство такой формы падает
    /// само, без правки этого теста. Свойства другой формы (например, вычисляемая
    /// `Category` с заглавной буквы) тест не трогает: описанная в TODO ловушка —
    /// именно в лестнице `is*`, а не в любом производном свойстве вообще.</summary>
    [Test]
    public void ItemType_NeverGrowsALowercaseIsProperty()
    {
        var suspects = typeof(SidebarCatalog.Item)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(name => name.Length > 2 && name.StartsWith("is") && char.IsUpper(name[2]))
            .ToList();

        Assert.IsEmpty(suspects,
            "SidebarCatalog.Item снова обзавёлся булевым свойством `is*` — "
            + "это ровно та лестница типов, которую убрали: каждый новый "
            + "SidebarItemKind требовал ещё одной такой строки. Вид отвечает за себя "
            + "через kind (и, где нужно, через собственное вычисляемое свойство без "
            + "префикса is), а не через накопление флагов. Свойства: "
            + string.Join(", ", suspects));
    }
}
