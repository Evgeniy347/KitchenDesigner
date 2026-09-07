using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.UI
{
    public static class SidebarCatalog
    {
        public const int DefaultGapMM = FacadeElement.DEFAULT_GAP_MM;
        public const string DefaultDrawerType = "A";
        public const int DefaultDrawerLengthMM = 350;
        public const string DefaultDrawerColor = "Anthracite";
        public const int DefaultDrawerWidthMM = 400;
        public const string DefaultDrawerSystem = "gtv";
        public const string MoventoDrawerSystem = "movento";

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
            public int gapLeft;
            public int gapRight;
            public int gapTop;
            public int gapBottom;

            public Item(string name, Vector3Int dims,
                SidebarItemKind kind = SidebarItemKind.Board,
                int gapLeft = DefaultGapMM, int gapRight = DefaultGapMM,
                int gapTop = DefaultGapMM, int gapBottom = DefaultGapMM)
            {
                this.name = name; this.dims = dims; this.kind = kind;
                this.gapLeft = gapLeft; this.gapRight = gapRight;
                this.gapTop = gapTop; this.gapBottom = gapBottom;
                applianceModel = "";
                pillarMidHeightMM = PillarElement.MidHeightMM_Default;
                drawerType = DefaultDrawerType;
                drawerLength = DefaultDrawerLengthMM;
                drawerColor = DefaultDrawerColor;
                drawerWidth = DefaultDrawerWidthMM;
                drawerSystem = DefaultDrawerSystem;
            }

            public bool isWall => kind == SidebarItemKind.Wall;

            public bool isFacade => kind == SidebarItemKind.Facade
                                    || kind == SidebarItemKind.AssembledFacade;

            public bool isAssembled => kind == SidebarItemKind.AssembledFacade;

            public bool isPanel => kind == SidebarItemKind.Panel;

            public bool isDrawer => kind == SidebarItemKind.Drawer;

            public bool isRadialShelf => kind == SidebarItemKind.RadialShelf;

            public bool isFurniture => kind == SidebarItemKind.Table;

            public bool isRadiusTable => kind == SidebarItemKind.RadiusTable;

            public bool isStool => kind == SidebarItemKind.Stool;

            public bool isChair => kind == SidebarItemKind.Chair;

            public bool isSofa => kind == SidebarItemKind.Sofa;

            public bool isBed => kind == SidebarItemKind.Bed;

            public bool isPouffe => kind == SidebarItemKind.Pouffe;

            public bool isPillar => kind == SidebarItemKind.Pillar;

            public bool isScrewLeg => kind == SidebarItemKind.ScrewLeg;

            public bool isSink => kind == SidebarItemKind.Sink;

            public bool isCooktop => kind == SidebarItemKind.Cooktop;

            public bool isOven => kind == SidebarItemKind.Oven;

            public bool isDishwasher => kind == SidebarItemKind.Dishwasher;

            public bool isToilet => kind == SidebarItemKind.Toilet;

            public bool isWallHungToilet => kind == SidebarItemKind.WallHungToilet;

            public bool isBathtub => kind == SidebarItemKind.Bathtub;

            public bool isBathMixer => kind == SidebarItemKind.BathMixer;

            public bool isShowerColumn => kind == SidebarItemKind.ShowerColumn;

            public bool isSocket => kind == SidebarItemKind.Socket;

            public bool isLightSwitch => kind == SidebarItemKind.LightSwitch;

            public bool isFloor => kind == SidebarItemKind.Floor;

            public bool isLightSource => kind == SidebarItemKind.LightSource;

            public bool isWindow => kind == SidebarItemKind.Window;

            public bool isDoor => kind == SidebarItemKind.Door;
        }

        public struct Group
        {
            public string title;
            public string shortLabel;
            public List<Item> items;
        }

        public static List<Group> Build()
        {
            return new List<Group>
            {
                BoardGroup(),
                FacadeGroup(),
                DrawerGroup(),
                FurnitureGroup(),
                ApplianceGroup(),
                SanitaryGroup(),
                new Group
                {
                    title = "Помещение",
                    shortLabel = "П",
                    items = new List<Item>
                    {
                        new Item("Короб", new Vector3Int(600, 600, 600)),
                        new Item("Стена", new Vector3Int(2000, 2500, 100),
                            SidebarItemKind.Wall),
                        new Item("Окно", new Vector3Int(900, 1200, 100),
                            SidebarItemKind.Window),
                        new Item("Дверь", new Vector3Int(900, 2000, 100),
                            SidebarItemKind.Door),
                        new Item("Пол", new Vector3Int(
                            FloorElement.DEFAULT_SIZE_MM,
                            FloorElement.DEFAULT_THICKNESS_MM,
                            FloorElement.DEFAULT_SIZE_MM), SidebarItemKind.Floor),
                        LightSourceItem("Источник света"),
                        SocketItem("Розетка"),
                        LightSwitchItem("Выключатель"),
                    }
                },
            };
        }

        private static Group BoardGroup()
        {
            var regular = new Item("Полка", new Vector3Int(600, 400, 16));
            var radial = new Item("Радиусная полка", new Vector3Int(600, 400, 16),
                SidebarItemKind.RadialShelf);
            var panel = new Item("ДВП/ХДФ", new Vector3Int(600, 400, 3), SidebarItemKind.Panel,
                gapLeft: PanelElement.DEFAULT_GAP_MM, gapRight: PanelElement.DEFAULT_GAP_MM,
                gapTop: PanelElement.DEFAULT_GAP_MM, gapBottom: PanelElement.DEFAULT_GAP_MM);
            return new Group { title = "детали", shortLabel = "Д", items = new List<Item> { regular, radial, panel } };
        }

        private static Group FacadeGroup()
        {
            return new Group
            {
                title = "Фасады",
                shortLabel = "Ф",
                items = new List<Item>
                {
                    new Item("Фасад щитовой", new Vector3Int(600, 716, 18),
                        SidebarItemKind.Facade, DefaultGapMM, DefaultGapMM,
                        DefaultGapMM, DefaultGapMM),
                    new Item("Фасад сборный", new Vector3Int(600, 716, 18),
                        SidebarItemKind.AssembledFacade, DefaultGapMM, DefaultGapMM,
                        DefaultGapMM, DefaultGapMM),
                }
            };
        }

        private static Group DrawerGroup()
        {
            var gtv = DrawerItem("Ящик GTV", DefaultDrawerType, DefaultDrawerLengthMM,
                DefaultDrawerSystem);
            var movento = DrawerItem("Ящик Movento", DefaultDrawerType, 500,
                MoventoDrawerSystem);
            return new Group { title = "Ящики", shortLabel = "Я", items = new List<Item> { gtv, movento } };
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

        private static Group FurnitureGroup()
        {
            var table = new Item("Прямоугольный стол", new Vector3Int(2000, 750, 1000),
                SidebarItemKind.Table);
            var radiusTable = new Item("Радиусный стол", new Vector3Int(2000, 750, 1000),
                SidebarItemKind.RadiusTable);
            var stool = new Item("Табуретка", new Vector3Int(StoolElement.DefaultWidthMM,
                StoolElement.DefaultHeightMM, StoolElement.DefaultDepthMM),
                SidebarItemKind.Stool);
            var chair = new Item("Стул", new Vector3Int(ChairElement.DefaultWidthMM,
                ChairElement.DefaultHeightMM, ChairElement.DefaultDepthMM),
                SidebarItemKind.Chair);
            var sofa = new Item("Диван", new Vector3Int(SofaElement.DefaultWidthMM,
                SofaElement.DefaultHeightMM, SofaElement.DefaultDepthMM),
                SidebarItemKind.Sofa);
            var bed = new Item("Кровать", new Vector3Int(BedElement.DefaultWidthMM,
                BedElement.DefaultHeightMM, BedElement.DefaultDepthMM),
                SidebarItemKind.Bed);
            var pouffe = new Item("Пуфик", new Vector3Int(PouffeElement.DefaultWidthMM,
                PouffeElement.DefaultHeightMM, PouffeElement.DefaultDepthMM),
                SidebarItemKind.Pouffe);
            var pillar = PillarItem("Ножка", PillarElement.MidHeightMM_Default);
            var screwLeg = ScrewLegItem("Винтовая опора");
            var sink = SinkItem("Мойка");
            return new Group { title = "Мебель", shortLabel = "М", items = new List<Item> { table, radiusTable, stool, chair, sofa, pouffe, bed, pillar, screwLeg, sink } };
        }

        private static Group ApplianceGroup()
        {
            var genericCooktop = CooktopItem("Варочная поверхность");
            var modelCooktop = CooktopModelItem("Варочная " + CooktopElement.MODEL_BOSCH_PUE611BB5E,
                CooktopElement.MODEL_BOSCH_PUE611BB5E);
            var oven = OvenItem("Духовка " + OvenElement.MODEL);
            var dishwasher = DishwasherItem("Посудомойка " + DishwasherElement.MODEL);
            return new Group
            {
                title = "Техника", shortLabel = "Т",
                items = new List<Item> { genericCooktop, modelCooktop, oven, dishwasher },
            };
        }

        private static Group SanitaryGroup()
        {
            return new Group
            {
                title = "Сантехника",
                shortLabel = "С",
                items = new List<Item>
                {
                    ToiletItem("Унитаз"), WallHungToiletItem("Инсталляция"), BathtubItem("Ванна"),
                    BathMixerItem("Смеситель"), ShowerColumnItem("Душевая стойка"),
                    PipeItem("Труба"),
                    FittingItem(PipeNodeKind.Elbow, SidebarItemKind.PipeElbow),
                    FittingItem(PipeNodeKind.Coupling, SidebarItemKind.PipeCoupling),
                    FittingItem(PipeNodeKind.Tee, SidebarItemKind.PipeTee),
                    FittingItem(PipeNodeKind.Cap, SidebarItemKind.PipeCap),
                    FittingItem(PipeNodeKind.Supply, SidebarItemKind.PipeSupply),
                    FittingItem(PipeNodeKind.Return, SidebarItemKind.PipeReturn),
                },
            };
        }

        private static Item FittingItem(PipeNodeKind kind, SidebarItemKind itemKind)
        {
            string sizeId = PipeSpec.DEFAULT_SIZE;
            return new Item(PipeFittingNames.Title(kind), new Vector3Int(
                PipeFittingSpec.RoundedMm(PipeFittingSpec.WidthMm(kind, sizeId)),
                PipeFittingSpec.RoundedMm(PipeFittingSpec.HeightMm(kind, sizeId)),
                PipeFittingSpec.RoundedMm(PipeFittingSpec.DepthMm(kind, sizeId))), itemKind);
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
