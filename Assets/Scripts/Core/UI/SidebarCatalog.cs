using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Каталог объектов для сайдбара: группы и их элементы (имя + размеры).
    /// Отделён от UI, чтобы покрываться юнит-тестами.</summary>
    public static class SidebarCatalog
    {
        public struct Item
        {
            public string name;
            public Vector3Int dims;
            public bool isWall;
            public bool isFacade;
            public bool isAssembled;
            public bool isDrawer;
            public bool isRadialShelf;
            public bool isFurniture;
            public bool isRadiusTable;
            public bool isStool;
            public bool isChair;
            public bool isWindow;
            public bool isDoor;
            public bool isPillar;
            public bool isFloor;
            public bool isLightSource;
            public bool isSink;           // врезная мойка (садится на деталь-столешницу)
            public bool isCooktop;        // варочная поверхность (садится на деталь-столешницу без выреза)
            public bool isOven;           // духовой шкаф (отдельно стоящий, встраивается в колонну)
            public bool isDishwasher;     // посудомоечная машина (фасад пристёгивается отдельно)
            public bool isPanel;          // ДВП/ХДФ — вкладная панель с зазорами
            /// <summary>Готовая модель встраиваемой техники (группа «Техника»):
            /// габариты берутся у производителя и не редактируются. Пусто —
            /// свободный элемент.</summary>
            public string applianceModel;
            public int pillarMidHeightMM;
            public string drawerType;
            public int drawerLength;
            public string drawerColor;
            public int drawerWidth;
            public string drawerSystem;   // "gtv" | "movento"
            public int gapLeft;
            public int gapRight;
            public int gapTop;
            public int gapBottom;
            public Item(string name, Vector3Int dims, bool isWall = false, bool isFacade = false,
                int gapLeft = 2, int gapRight = 2, int gapTop = 2, int gapBottom = 2,
                bool isAssembled = false)
            {
                this.name = name; this.dims = dims; this.isWall = isWall;
                this.isFacade = isFacade; this.isAssembled = isAssembled;
                this.gapLeft = gapLeft; this.gapRight = gapRight;
                this.gapTop = gapTop; this.gapBottom = gapBottom;
                isDrawer = false; isRadialShelf = false; isFurniture = false; isRadiusTable = false; isStool = false; isChair = false; isWindow = false; isDoor = false;
                isPillar = false; isFloor = false; isLightSource = false; isSink = false; isCooktop = false;
                isOven = false; isDishwasher = false;
                isPanel = false; applianceModel = ""; pillarMidHeightMM = 75;
                drawerType = "A"; drawerLength = 350;
                drawerColor = "Anthracite"; drawerWidth = 400; drawerSystem = "gtv";
            }
        }

        public struct Group
        {
            public string title;
            public string shortLabel; // подпись в свёрнутом режиме
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
                new Group
                {
                    title = "Помещение",
                    shortLabel = "П",
                    items = new List<Item>
                    {
                        new Item("Короб", new Vector3Int(600, 600, 600)),
                        new Item("Стена", new Vector3Int(2000, 2500, 100), true),
                        WindowItem("Окно", new Vector3Int(900, 1200, 100)),
                        DoorItem("Дверь", new Vector3Int(900, 2000, 100)),
                        FloorItem("Пол", new Vector3Int(
                            FloorElement.DEFAULT_SIZE_MM,
                            FloorElement.DEFAULT_THICKNESS_MM,
                            FloorElement.DEFAULT_SIZE_MM)),
                        LightSourceItem("Источник света"),
                    }
                },
            };
        }

        private static Group BoardGroup()
        {
            var regular = new Item("Полка", new Vector3Int(600, 400, 16));
            var radial = new Item("Радиусная полка", new Vector3Int(600, 400, 16));
            radial.isRadialShelf = true;
            // ДВП/ХДФ — вкладная панель: зазоры входят в габарит, поэтому в паз
            // заходит номинал, а 1 мм остаётся технологическим зазором.
            var panel = new Item("ДВП/ХДФ", new Vector3Int(600, 400, 3),
                gapLeft: PanelElement.DEFAULT_GAP_MM, gapRight: PanelElement.DEFAULT_GAP_MM,
                gapTop: PanelElement.DEFAULT_GAP_MM, gapBottom: PanelElement.DEFAULT_GAP_MM);
            panel.isPanel = true;
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
                    new Item("Фасад щитовой", new Vector3Int(600, 716, 18), false, true, 2, 2, 2, 2),
                    new Item("Фасад сборный", new Vector3Int(600, 716, 18), false, true,
                        0, 0, 0, 0, isAssembled: true),
                }
            };
        }

        private static Group DrawerGroup()
        {
            var gtv = DrawerItem("Ящик GTV", "A", 350, "gtv");
            var movento = DrawerItem("Ящик Movento", "A", 500, "movento");
            return new Group { title = "Ящики", shortLabel = "Я", items = new List<Item> { gtv, movento } };
        }

        private static Item DrawerItem(string name, string drawerType, int length, string system = "gtv")
        {
            int height = drawerType switch { "A" => 86, "B" => 120, "C" => 168, _ => 200 };
            var item = new Item(name, new Vector3Int(400, height, length));
            item.isDrawer = true;
            item.drawerType = drawerType;
            item.drawerLength = length;
            item.drawerColor = "Anthracite";
            item.drawerWidth = 400;
            item.drawerSystem = system;
            return item;
        }

        private static Group FurnitureGroup()
        {
            var table = new Item("Прямоугольный стол", new Vector3Int(2000, 750, 1000));
            table.isFurniture = true;
            var radiusTable = new Item("Радиусный стол", new Vector3Int(2000, 750, 1000));
            radiusTable.isRadiusTable = true;
            var stool = new Item("Табуретка", new Vector3Int(StoolElement.DefaultWidthMM,
                StoolElement.DefaultHeightMM, StoolElement.DefaultDepthMM));
            stool.isStool = true;
            var chair = new Item("Стул", new Vector3Int(ChairElement.DefaultWidthMM,
                ChairElement.DefaultHeightMM, ChairElement.DefaultDepthMM));
            chair.isChair = true;
            var pillar = PillarItem("Ножка", PillarElement.MidHeightMM_Default);
            var sink = SinkItem("Мойка");
            return new Group { title = "Мебель", shortLabel = "М", items = new List<Item> { table, radiusTable, stool, chair, pillar, sink } };
        }

        /// <summary>Встраиваемая техника — готовые модели производителя. Габариты
        /// у пунктов этой группы фиксированы (<see cref="IFixedSizeElement"/>):
        /// поля Ш/В/Г в окне свойств серые, ручек ресайза нет.
        ///
        /// Как добавить прибор: положить сюда ещё один Item со своим флагом типа
        /// (по образцу <see cref="CooktopModelItem"/> или <see cref="OvenItem"/>),
        /// развести его в SidebarUI.Spawn и дописать модель в
        /// <see cref="ApplianceModels.All"/>. Порядок пунктов — варочная
        /// свободного размера, варочная модельная, духовка, посудомойка.</summary>
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

        /// <summary>Посудомоечная машина: размеры берутся из DishwasherElement.
        /// Фасад в каталоге не заводится — его пользователь ставит сам и
        /// пристёгивает к машине в окне свойств.</summary>
        private static Item DishwasherItem(string name)
        {
            var item = new Item(name, DishwasherElement.ModelDimensionsMM);
            item.isDishwasher = true;
            item.applianceModel = DishwasherElement.MODEL;
            return item;
        }

        /// <summary>Духовой шкаф: размеры берутся из OvenElement, а не из
        /// каталога, — модель у него одна и правке не подлежит.</summary>
        private static Item OvenItem(string name)
        {
            var item = new Item(name, OvenElement.ModelDimensionsMM);
            item.isOven = true;
            item.applianceModel = OvenElement.MODEL;
            return item;
        }

        /// <summary>Варочная поверхность готовой модели: размеры берутся из её
        /// таблицы в CooktopElement, а не из каталога.</summary>
        private static Item CooktopModelItem(string name, string model)
        {
            var item = new Item(name, CooktopElement.ModelDimensionsMM(model));
            item.isCooktop = true;
            item.applianceModel = model;
            return item;
        }

        private static Item CooktopItem(string name)
        {
            var item = new Item(name, new Vector3Int(
                CooktopElement.DEFAULT_WIDTH_MM, CooktopElement.DEFAULT_HEIGHT_MM, CooktopElement.DEFAULT_DEPTH_MM));
            item.isCooktop = true;
            return item;
        }

        private static Item SinkItem(string name)
        {
            var item = new Item(name, new Vector3Int(
                SinkElement.OUTER_WIDTH_MM, SinkElement.TotalHeightMM, SinkElement.OUTER_DEPTH_MM));
            item.isSink = true;
            return item;
        }

        private static Item PillarItem(string name, int midHeightMM)
        {
            int totalH = PillarElement.TopHeightMM + midHeightMM + PillarElement.BottomHeightMM;
            var item = new Item(name, new Vector3Int(PillarElement.DiameterMM_Default, totalH, PillarElement.DiameterMM_Default));
            item.isPillar = true;
            item.pillarMidHeightMM = midHeightMM;
            return item;
        }

        private static Item FloorItem(string name, Vector3Int dims)
        {
            var item = new Item(name, dims);
            item.isFloor = true;
            return item;
        }

        private static Item LightSourceItem(string name)
        {
            var item = new Item(name, new Vector3Int(
                LampSpec.DEFAULT_SIZE_MM,
                LampSpec.DEFAULT_SIZE_MM,
                LampSpec.DEFAULT_SIZE_MM));
            item.isLightSource = true;
            return item;
        }

        private static Item WindowItem(string name, Vector3Int dims)
        {
            var item = new Item(name, dims);
            item.isWindow = true;
            return item;
        }

        private static Item DoorItem(string name, Vector3Int dims)
        {
            var item = new Item(name, dims);
            item.isDoor = true;
            return item;
        }
    }
}
