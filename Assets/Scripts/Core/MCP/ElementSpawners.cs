using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    internal static class ElementSpawners
    {
        public const int PANEL_DEFAULT_WIDTH_MM = 600;
        public const int PANEL_DEFAULT_HEIGHT_MM = 400;
        public const int PANEL_DEFAULT_THICKNESS_MM = 3;
        public const int ASSEMBLED_FACADE_DEFAULT_WIDTH_MM = 450;
        public const int ASSEMBLED_FACADE_DEFAULT_HEIGHT_MM = 700;
        public const int RADIAL_SHELF_DEFAULT_WIDTH_MM = 600;
        public const int RADIAL_SHELF_DEFAULT_DEPTH_MM = 400;
        public const int GTV_DRAWER_DEFAULT_LENGTH_MM = 350;
        public const int GTV_DRAWER_DEFAULT_INTERNAL_WIDTH_MM = 400;
        public const int MOVENTO_DRAWER_DEFAULT_LENGTH_MM = 500;
        public const int MOVENTO_DRAWER_DEFAULT_INTERNAL_WIDTH_MM = 568;
        public const int TABLE_DEFAULT_WIDTH_MM = 2000;
        public const int TABLE_DEFAULT_HEIGHT_MM = 750;
        public const int TABLE_DEFAULT_DEPTH_MM = 1000;
        public const int WINDOW_DEFAULT_WIDTH_MM = 900;
        public const int WINDOW_DEFAULT_HEIGHT_MM = 1200;
        public const int OPENING_DEFAULT_WALL_DEPTH_MM = 100;
        public const int WINDOW_DEFAULT_SILL_PROTRUSION_MM = 50;
        public const int DOOR_DEFAULT_WIDTH_MM = 900;
        public const int DOOR_DEFAULT_HEIGHT_MM = 2000;
        public const int BOARD_DEFAULT_WIDTH_MM = 800;
        public const int BOARD_DEFAULT_HEIGHT_MM = 400;
        public const int BOARD_DEFAULT_THICKNESS_MM = 18;

        private static readonly Dictionary<string, Func<CreateItem, Vector3, GameObject>> ByType =
            new Dictionary<string, Func<CreateItem, Vector3, GameObject>>(StringComparer.Ordinal)
            {
                ["floor"] = (item, pos) => ElementFactory.CreateFloor(new Vector3Int(
                    item.width ?? FloorElement.DEFAULT_SIZE_MM,
                    item.height ?? FloorElement.DEFAULT_THICKNESS_MM,
                    item.depth ?? FloorElement.DEFAULT_SIZE_MM), item.name, pos),

                ["assembled_facade"] = (item, pos) => ElementFactory.CreateAssembledFacade(
                    new Vector3Int(item.width ?? ASSEMBLED_FACADE_DEFAULT_WIDTH_MM,
                        item.height ?? ASSEMBLED_FACADE_DEFAULT_HEIGHT_MM,
                        item.depth ?? BOARD_DEFAULT_THICKNESS_MM),
                    item.name, pos, AssembledFill.Blind),

                ["radial_shelf"] = (item, pos) => ElementFactory.CreateRadialShelf(
                    item.width ?? RADIAL_SHELF_DEFAULT_WIDTH_MM,
                    item.depth ?? RADIAL_SHELF_DEFAULT_DEPTH_MM,
                    item.height ?? AppConstants.BOARD_THICKNESS_DEFAULT,
                    AppConstants.RADIAL_CORNER_RADIUS_DEFAULT, item.name, pos),

                ["panel"] = (item, pos) => ElementFactory.Instance.CreatePanel(
                    new Vector3Int(item.width ?? PANEL_DEFAULT_WIDTH_MM,
                        item.height ?? PANEL_DEFAULT_HEIGHT_MM,
                        item.depth ?? PANEL_DEFAULT_THICKNESS_MM),
                    item.name, pos),

                ["drawer"] = (item, pos) => ElementFactory.CreateDrawer(DrawerType.A,
                    GTV_DRAWER_DEFAULT_LENGTH_MM, DrawerColor.Anthracite,
                    GTV_DRAWER_DEFAULT_INTERNAL_WIDTH_MM, item.name, pos, DrawerSystem.Gtv),

                ["movento_drawer"] = (item, pos) => ElementFactory.CreateDrawer(DrawerType.A,
                    MOVENTO_DRAWER_DEFAULT_LENGTH_MM, DrawerColor.Anthracite,
                    item.width ?? MOVENTO_DRAWER_DEFAULT_INTERNAL_WIDTH_MM, item.name, pos, DrawerSystem.Movento),

                ["table"] = (item, pos) => ElementFactory.CreateTable(TableDims(item), item.name, pos),

                ["radius_table"] = (item, pos) => ElementFactory.CreateRadiusTable(TableDims(item), item.name, pos),

                ["pillar"] = (item, pos) => ElementFactory.CreatePillar(
                    item.height ?? PillarElement.MidHeightMM_Default, item.name, pos),

                ["sink"] = (item, pos) => ElementFactory.CreateSink(item.name, pos),

                ["cooktop"] = (item, pos) => ElementFactory.CreateCooktop(item.name, pos, item.model ?? ""),

                ["oven"] = (item, pos) => ElementFactory.CreateOven(item.name, pos),

                ["dishwasher"] = (item, pos) => ElementFactory.CreateDishwasher(item.name, pos),

                ["window"] = (item, pos) => ElementFactory.CreateWindow(
                    new Vector3Int(item.width ?? WINDOW_DEFAULT_WIDTH_MM,
                        item.height ?? WINDOW_DEFAULT_HEIGHT_MM,
                        item.depth ?? OPENING_DEFAULT_WALL_DEPTH_MM),
                    item.name, pos, GlassTint.Clear, WINDOW_DEFAULT_SILL_PROTRUSION_MM),

                ["door"] = (item, pos) => ElementFactory.CreateDoor(
                    new Vector3Int(item.width ?? DOOR_DEFAULT_WIDTH_MM,
                        item.height ?? DOOR_DEFAULT_HEIGHT_MM,
                        item.depth ?? OPENING_DEFAULT_WALL_DEPTH_MM),
                    item.name, pos, DoorSashType.Glass),
            };

        private static readonly string[] PlainCubeTypes = { "board", "wall", "facade" };

        internal static readonly IReadOnlyList<string> SpawnableTypes = CollectSpawnableTypes();

        private static readonly HashSet<string> SpawnableTypeSet =
            new HashSet<string>(SpawnableTypes, StringComparer.Ordinal);

        internal static bool CanSpawn(string elementType) => SpawnableTypeSet.Contains(elementType);

        private static List<string> CollectSpawnableTypes()
        {
            var types = new List<string>(PlainCubeTypes);
            types.AddRange(ByType.Keys);
            return types;
        }

        public static GameObject Spawn(string elementType, CreateItem item, Vector3 pos)
            => ByType.TryGetValue(elementType, out var spawn)
                ? spawn(item, pos)
                : SpawnPlainCube(elementType, item, pos);

        public static bool ModelBelongsToType(string elementType, string model) => elementType switch
        {
            "cooktop" => CooktopElement.IsKnownModel(model),
            "oven" => model == OvenElement.MODEL,
            "dishwasher" => model == DishwasherElement.MODEL,
            _ => false,
        };

        private static Vector3Int TableDims(CreateItem item) => new Vector3Int(
            item.width ?? TABLE_DEFAULT_WIDTH_MM,
            item.height ?? TABLE_DEFAULT_HEIGHT_MM,
            item.depth ?? TABLE_DEFAULT_DEPTH_MM);

        private static GameObject SpawnPlainCube(string elementType, CreateItem item, Vector3 pos)
        {
            var dims = new Vector3Int(
                item.width ?? BOARD_DEFAULT_WIDTH_MM,
                item.height ?? BOARD_DEFAULT_HEIGHT_MM,
                item.depth ?? BOARD_DEFAULT_THICKNESS_MM);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = item.name;
            KitchenElement element = elementType == "facade"
                ? go.AddComponent<FacadeElement>()
                : go.AddComponent<KitchenElement>();
            element.PartName = item.name;
            element.DimensionsMM = dims;
            if (elementType == "wall") go.AddComponent<Wall>();
            go.transform.position = pos;
            return go;
        }
    }
}
