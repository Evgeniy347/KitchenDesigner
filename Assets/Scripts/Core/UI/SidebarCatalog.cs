using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.UI
{
    public static class SidebarCatalog
    {
        public const string DefaultDrawerType = "A";
        public const int DefaultDrawerLengthMM = 350;
        public const string DefaultDrawerColor = "Anthracite";
        public const int DefaultDrawerWidthMM = 400;
        public const string DefaultDrawerSystem = SidebarPresetResolution.DefaultDrawerSystem;
        public const string MoventoDrawerSystem = SidebarPresetResolution.MoventoDrawerSystem;

        public struct Item
        {
            public string name;
            public Vector3Int dims;
            public SidebarItemKind kind;
            public string applianceModel;
            public int pillarMidHeightMM;
            public string drawerType;
            public int drawerLength;
            public string drawerColor;
            public int drawerWidth;
            public string drawerSystem;
            public PipeNodeKind fittingKind;

            public Item(string name, Vector3Int dims,
                SidebarItemKind kind = SidebarItemKind.Board)
            {
                this.name = name; this.dims = dims; this.kind = kind;
                applianceModel = "";
                pillarMidHeightMM = PillarElement.MidHeightMM_Default;
                drawerType = DefaultDrawerType;
                drawerLength = DefaultDrawerLengthMM;
                drawerColor = DefaultDrawerColor;
                drawerWidth = DefaultDrawerWidthMM;
                drawerSystem = DefaultDrawerSystem;
                fittingKind = PipeNodeKind.Coupling;
            }

            public string DisplayName => !string.IsNullOrEmpty(applianceModel) && name.EndsWith(applianceModel)
                ? name.Substring(0, name.Length - applianceModel.Length).TrimEnd()
                : name;

            public EditModeManager.Category Category => kind switch
            {
                SidebarItemKind.Window => EditModeManager.Category.Always,
                SidebarItemKind.Door => EditModeManager.Category.Always,
                SidebarItemKind.Wall => EditModeManager.Category.Room,
                SidebarItemKind.Floor => EditModeManager.Category.Room,
                _ => EditModeManager.Category.Regular,
            };
        }

        public struct Group
        {
            public string title;
            public string shortLabel;
            public Sprite icon;
            public List<Item> items;
        }

        private readonly struct GroupMeta
        {
            public readonly SidebarGroupKey key;
            public readonly string title;
            public readonly string shortLabel;
            public readonly Sprite icon;

            public GroupMeta(SidebarGroupKey key, string title, string shortLabel, Sprite icon)
            {
                this.key = key; this.title = title; this.shortLabel = shortLabel; this.icon = icon;
            }
        }

        private static IEnumerable<GroupMeta> GroupTable() => new[]
        {
            new GroupMeta(SidebarGroupKey.Board, "Детали", "Д", IconFactory.Shelf),
            new GroupMeta(SidebarGroupKey.Facade, "Фасады", "Ф", IconFactory.Facade),
            new GroupMeta(SidebarGroupKey.Drawer, "Ящики", "Я", IconFactory.Drawer),
            new GroupMeta(SidebarGroupKey.Furniture, "Мебель", "М", IconFactory.Furniture),
            new GroupMeta(SidebarGroupKey.Appliance, "Техника", "Т", IconFactory.Appliance),
            new GroupMeta(SidebarGroupKey.Sanitary, "Сантехника", "С", IconFactory.Faucet),
            new GroupMeta(SidebarGroupKey.Room, "Помещение", "П", IconFactory.Room),
        };

        public static List<Group> Build()
        {
            var rows = Rows().ToList();
            return GroupTable().Select(meta => new Group
            {
                title = meta.title,
                shortLabel = meta.shortLabel,
                icon = meta.icon,
                items = rows.Where(r => r.Group == meta.key).Select(r => r.Item).ToList(),
            }).ToList();
        }

        private static IEnumerable<(SidebarGroupKey Group, Item Item)> Rows()
        {
            yield return (SidebarGroupKey.Board,
                new Item("Полка", new Vector3Int(600, 400, 16)));
            yield return (SidebarGroupKey.Board,
                new Item("Радиусная полка", new Vector3Int(600, 400, 16), SidebarItemKind.RadialShelf));
            yield return (SidebarGroupKey.Board,
                new Item("ДВП/ХДФ", new Vector3Int(600, 400, 3), SidebarItemKind.Panel));

            yield return (SidebarGroupKey.Facade,
                new Item("Фасад щитовой", new Vector3Int(600, 716, 18), SidebarItemKind.Facade));
            yield return (SidebarGroupKey.Facade,
                new Item("Фасад сборный", new Vector3Int(600, 716, 18), SidebarItemKind.AssembledFacade));

            yield return (SidebarGroupKey.Drawer,
                DrawerItem("Ящик GTV", DefaultDrawerType, DefaultDrawerLengthMM, DefaultDrawerSystem));
            yield return (SidebarGroupKey.Drawer,
                DrawerItem("Ящик Movento", DefaultDrawerType, 500, MoventoDrawerSystem));

            yield return (SidebarGroupKey.Furniture,
                new Item("Прямоугольный стол", new Vector3Int(2000, 750, 1000), SidebarItemKind.Table));
            yield return (SidebarGroupKey.Furniture,
                new Item("Радиусный стол", new Vector3Int(2000, 750, 1000), SidebarItemKind.RadiusTable));
            yield return (SidebarGroupKey.Furniture,
                new Item("Табуретка", new Vector3Int(StoolElement.DefaultWidthMM,
                    StoolElement.DefaultHeightMM, StoolElement.DefaultDepthMM), SidebarItemKind.Stool));
            yield return (SidebarGroupKey.Furniture,
                new Item("Стул", new Vector3Int(ChairElement.DefaultWidthMM,
                    ChairElement.DefaultHeightMM, ChairElement.DefaultDepthMM), SidebarItemKind.Chair));
            yield return (SidebarGroupKey.Furniture,
                new Item("Диван", new Vector3Int(SofaElement.DefaultWidthMM,
                    SofaElement.DefaultHeightMM, SofaElement.DefaultDepthMM), SidebarItemKind.Sofa));
            yield return (SidebarGroupKey.Furniture,
                new Item("Пуфик", new Vector3Int(PouffeElement.DefaultWidthMM,
                    PouffeElement.DefaultHeightMM, PouffeElement.DefaultDepthMM), SidebarItemKind.Pouffe));
            yield return (SidebarGroupKey.Furniture,
                new Item("Кровать", new Vector3Int(BedElement.DefaultWidthMM,
                    BedElement.DefaultHeightMM, BedElement.DefaultDepthMM), SidebarItemKind.Bed));
            yield return (SidebarGroupKey.Furniture, PillarItem("Ножка", PillarElement.MidHeightMM_Default));
            yield return (SidebarGroupKey.Furniture, ScrewLegItem("Винтовая опора"));
            yield return (SidebarGroupKey.Furniture, SinkItem("Мойка"));

            yield return (SidebarGroupKey.Appliance, CooktopItem("Варочная поверхность"));
            yield return (SidebarGroupKey.Appliance,
                CooktopModelItem("Варочная " + CooktopElement.MODEL_BOSCH_PUE611BB5E,
                    CooktopElement.MODEL_BOSCH_PUE611BB5E));
            yield return (SidebarGroupKey.Appliance, OvenItem("Духовка " + OvenElement.MODEL));
            yield return (SidebarGroupKey.Appliance,
                DishwasherItem("Посудомойка " + DishwasherElement.MODEL));

            yield return (SidebarGroupKey.Sanitary, ToiletItem("Унитаз"));
            yield return (SidebarGroupKey.Sanitary, WallHungToiletItem("Инсталляция"));
            yield return (SidebarGroupKey.Sanitary, BathtubItem("Ванна"));
            yield return (SidebarGroupKey.Sanitary, BathMixerItem("Смеситель"));
            yield return (SidebarGroupKey.Sanitary, ShowerColumnItem("Душевая стойка"));
            yield return (SidebarGroupKey.Sanitary, PipeItem("Труба"));
            yield return (SidebarGroupKey.Sanitary, FittingItem(PipeNodeKind.Elbow));
            yield return (SidebarGroupKey.Sanitary, FittingItem(PipeNodeKind.Coupling));
            yield return (SidebarGroupKey.Sanitary, FittingItem(PipeNodeKind.Tee));
            yield return (SidebarGroupKey.Sanitary, FittingItem(PipeNodeKind.Cap));
            yield return (SidebarGroupKey.Sanitary, FittingItem(PipeNodeKind.Supply));
            yield return (SidebarGroupKey.Sanitary, FittingItem(PipeNodeKind.Return));

            yield return (SidebarGroupKey.Room, new Item("Короб", new Vector3Int(600, 600, 600)));
            yield return (SidebarGroupKey.Room,
                new Item("Стена", new Vector3Int(2000, 2500, 100), SidebarItemKind.Wall));
            yield return (SidebarGroupKey.Room,
                new Item("Окно", new Vector3Int(900, 1200, 100), SidebarItemKind.Window));
            yield return (SidebarGroupKey.Room,
                new Item("Дверь", new Vector3Int(900, 2000, 100), SidebarItemKind.Door));
            yield return (SidebarGroupKey.Room,
                new Item("Пол", new Vector3Int(
                    FloorElement.DEFAULT_SIZE_MM,
                    FloorElement.DEFAULT_THICKNESS_MM,
                    FloorElement.DEFAULT_SIZE_MM), SidebarItemKind.Floor));
            yield return (SidebarGroupKey.Room, LightSourceItem("Источник света"));
            yield return (SidebarGroupKey.Room, SocketItem("Розетка"));
            yield return (SidebarGroupKey.Room, LightSwitchItem("Выключатель"));
        }

        private static Item DrawerItem(string name, string drawerType, int length,
            string system = DefaultDrawerSystem)
        {
            int height = drawerType switch { "A" => 86, "B" => 120, "C" => 168, _ => 200 };
            var item = new Item(name, new Vector3Int(DefaultDrawerWidthMM, height, length),
                SidebarItemKind.Drawer);
            item.drawerType = drawerType;
            item.drawerLength = length;
            item.drawerColor = DefaultDrawerColor;
            item.drawerWidth = DefaultDrawerWidthMM;
            item.drawerSystem = system;
            return item;
        }

        private static Item FittingItem(PipeNodeKind kind)
        {
            string sizeId = PipeSpec.DEFAULT_SIZE;
            var item = new Item(PipeFittingNames.Title(kind), new Vector3Int(
                PipeFittingSpec.RoundedMm(PipeFittingSpec.WidthMm(kind, sizeId)),
                PipeFittingSpec.RoundedMm(PipeFittingSpec.HeightMm(kind, sizeId)),
                PipeFittingSpec.RoundedMm(PipeFittingSpec.DepthMm(kind, sizeId))),
                SidebarItemKind.PipeFitting);
            item.fittingKind = kind;
            return item;
        }

        private static Item PipeItem(string name)
        {
            int section = PipeElementSpec.SectionMM(PipeSpec.DEFAULT_SIZE);
            return new Item(name,
                new Vector3Int(section, PipeElementSpec.DEFAULT_LENGTH_MM, section),
                SidebarItemKind.Pipe);
        }

        private static Item ToiletItem(string name)
            => new Item(name, ToiletElement.ModelDimensionsMM, SidebarItemKind.Toilet);

        private static Item WallHungToiletItem(string name)
            => new Item(name, WallHungToiletElement.ModelDimensionsMM,
                SidebarItemKind.WallHungToilet);

        private static Item BathtubItem(string name)
            => new Item(name, new Vector3Int(BathtubElement.DefaultWidthMM,
                BathtubElement.DefaultHeightMM, BathtubElement.DefaultDepthMM),
                SidebarItemKind.Bathtub);

        private static Item BathMixerItem(string name)
            => new Item(name, BathMixerLayout.DimensionsMM(BathMixerSpec.Default),
                SidebarItemKind.BathMixer);

        private static Item ShowerColumnItem(string name)
            => new Item(name, ShowerColumnLayout.DimensionsMM(ShowerColumnSpec.Default),
                SidebarItemKind.ShowerColumn);

        private static Item DishwasherItem(string name)
        {
            var item = new Item(name, DishwasherElement.ModelDimensionsMM,
                SidebarItemKind.Dishwasher);
            item.applianceModel = DishwasherElement.MODEL;
            return item;
        }

        private static Item OvenItem(string name)
        {
            var item = new Item(name, OvenElement.ModelDimensionsMM, SidebarItemKind.Oven);
            item.applianceModel = OvenElement.MODEL;
            return item;
        }

        private static Item CooktopModelItem(string name, string model)
        {
            var item = new Item(name, CooktopElement.ModelDimensionsMM(model),
                SidebarItemKind.Cooktop);
            item.applianceModel = model;
            return item;
        }

        private static Item CooktopItem(string name)
        {
            return new Item(name, new Vector3Int(
                CooktopElement.DEFAULT_WIDTH_MM, CooktopElement.DEFAULT_HEIGHT_MM,
                CooktopElement.DEFAULT_DEPTH_MM), SidebarItemKind.Cooktop);
        }

        private static Item SinkItem(string name)
        {
            return new Item(name, new Vector3Int(
                SinkElement.OUTER_WIDTH_MM, SinkElement.TotalHeightMM,
                SinkElement.OUTER_DEPTH_MM), SidebarItemKind.Sink);
        }

        private static Item PillarItem(string name, int midHeightMM)
        {
            int totalH = PillarElement.TopHeightMM + midHeightMM + PillarElement.BottomHeightMM;
            var item = new Item(name, new Vector3Int(PillarElement.DiameterMM_Default, totalH,
                PillarElement.DiameterMM_Default), SidebarItemKind.Pillar);
            item.pillarMidHeightMM = midHeightMM;
            return item;
        }

        private static Item ScrewLegItem(string name)
        {
            int totalH = ScrewLegSpec.BodyHeightMM(ScrewLegSpec.DEFAULT_THREAD_LENGTH_MM,
                ScrewLegSpec.DEFAULT_BASE_HEIGHT_MM);
            return new Item(name, new Vector3Int(
                ScrewLegSpec.DEFAULT_BASE_DIAMETER_MM, totalH,
                ScrewLegSpec.DEFAULT_BASE_DIAMETER_MM), SidebarItemKind.ScrewLeg);
        }

        private static Item SocketItem(string name)
            => new Item(name, WallDeviceSpec.Default.DimensionsMM, SidebarItemKind.Socket);

        private static Item LightSwitchItem(string name)
            => new Item(name, WallDeviceSpec.Default.DimensionsMM, SidebarItemKind.LightSwitch);

        private static Item LightSourceItem(string name)
        {
            return new Item(name, new Vector3Int(
                LampSpec.DEFAULT_SIZE_MM,
                LampSpec.DEFAULT_SIZE_MM,
                LampSpec.DEFAULT_SIZE_MM), SidebarItemKind.LightSource);
        }
    }
}
