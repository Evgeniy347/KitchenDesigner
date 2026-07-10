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
            public bool isAssembled; // сборный (рамочный) фасад
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
            }
        }

        public struct Group
        {
            public string title;
            public string shortLabel; // подпись в свёрнутом режиме
            public List<Item> items;
        }

        // Базовые типоразмеры (ширина×высота); толщина задаётся группой.
        private static readonly Vector2Int[] BoardSizes =
        {
            new Vector2Int(800, 400),
            new Vector2Int(600, 400),
            new Vector2Int(400, 400),
            new Vector2Int(1200, 600),
            new Vector2Int(600, 600),
        };

        public static List<Group> Build()
        {
            return new List<Group>
            {
                BoardGroup(),
                FacadeGroup(),
                new Group
                {
                    title = "Помещение",
                    shortLabel = "П",
                    items = new List<Item>
                    {
                        new Item("Короб", new Vector3Int(600, 600, 600)),
                        new Item("Стена", new Vector3Int(2000, 2500, 100), true),
                        new Item("Размеры помещения", Vector3Int.zero),
                    }
                },
            };
        }

        private static Group BoardGroup()
        {
            var items = new List<Item>();
            foreach (var s in BoardSizes)
            {
                items.Add(new Item($"{s.x}×{s.y}×16", new Vector3Int(s.x, s.y, 16)));
                items.Add(new Item($"{s.x}×{s.y}×18", new Vector3Int(s.x, s.y, 18)));
            }
            return new Group { title = "Доски", shortLabel = "Д", items = items };
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
    }
}
