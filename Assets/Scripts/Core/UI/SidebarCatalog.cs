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
            public bool isWindow;
            public string drawerType;
            public int drawerLength;
            public string drawerColor;
            public int drawerWidth;
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
                isDrawer = false; isRadialShelf = false; isFurniture = false; isRadiusTable = false; isWindow = false;
                drawerType = "A"; drawerLength = 350;
                drawerColor = "Anthracite"; drawerWidth = 400;
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
                new Group
                {
                    title = "Помещение",
                    shortLabel = "П",
                    items = new List<Item>
                    {
                        new Item("Короб", new Vector3Int(600, 600, 600)),
                        new Item("Стена", new Vector3Int(2000, 2500, 100), true),
                        WindowItem("Окно", new Vector3Int(900, 1200, 100)),
                        new Item("Размеры помещения", Vector3Int.zero),
                    }
                },
            };
        }

        private static Group BoardGroup()
        {
            var regular = new Item("600×400×16", new Vector3Int(600, 400, 16));
            var radial = new Item("600×400×16 (радиусная)", new Vector3Int(600, 400, 16));
            radial.isRadialShelf = true;
            return new Group { title = "детали", shortLabel = "Д", items = new List<Item> { regular, radial } };
        }

        private static Group FacadeGroup()
        {
            return new Group
            {
                title = "Фасады",
                shortLabel = "Ф",
                items = new List<Item>
                {
                    new Item("Фасад 800×400×18", new Vector3Int(800, 400, 18), false, true, 2, 2, 2, 2),
                    new Item("Фасад 600×400×18", new Vector3Int(600, 400, 18), false, true, 2, 2, 2, 2),
                    new Item("Сборный 450×700×18", new Vector3Int(450, 700, 18), false, true,
                        0, 0, 0, 0, isAssembled: true),
                    new Item("Сборный 600×716×18", new Vector3Int(600, 716, 18), false, true,
                        0, 0, 0, 0, isAssembled: true),
                }
            };
        }

        private static Group DrawerGroup()
        {
            var item = DrawerItem("Ящик GTV", "A", 350);
            return new Group { title = "Ящики GTV", shortLabel = "Я", items = new List<Item> { item } };
        }

        private static Item DrawerItem(string name, string drawerType, int length)
        {
            int height = drawerType switch { "A" => 86, "B" => 120, "C" => 168, _ => 200 };
            var item = new Item(name, new Vector3Int(400, height, length));
            item.isDrawer = true;
            item.drawerType = drawerType;
            item.drawerLength = length;
            item.drawerColor = "Anthracite";
            item.drawerWidth = 400;
            return item;
        }

        private static Group FurnitureGroup()
        {
            var table = new Item("Прямоугольный стол", new Vector3Int(2000, 750, 1000));
            table.isFurniture = true;
            var radiusTable = new Item("Радиусный стол", new Vector3Int(2000, 750, 1000));
            radiusTable.isRadiusTable = true;
            return new Group { title = "Мебель", shortLabel = "М", items = new List<Item> { table, radiusTable } };
        }

        private static Item WindowItem(string name, Vector3Int dims)
        {
            var item = new Item(name, dims);
            item.isWindow = true;
            return item;
        }
    }
}
