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
            public Item(string name, Vector3Int dims, bool isWall = false)
            {
                this.name = name; this.dims = dims; this.isWall = isWall;
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
                ThicknessGroup("16 мм", "16", 16),
                ThicknessGroup("18 мм", "18", 18),
                new Group
                {
                    title = "Помещение",
                    shortLabel = "П",
                    items = new List<Item>
                    {
                        new Item("Короб", new Vector3Int(600, 600, 600)),
                        new Item("Стена", new Vector3Int(2000, 2500, 100), true),
                    }
                },
            };
        }

        private static Group ThicknessGroup(string title, string shortLabel, int thickness)
        {
            var items = new List<Item>();
            foreach (var s in BoardSizes)
                items.Add(new Item($"{s.x}×{s.y}", new Vector3Int(s.x, s.y, thickness)));
            return new Group { title = title, shortLabel = shortLabel, items = items };
        }
    }
}
