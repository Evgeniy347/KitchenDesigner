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

        public struct Preset
        {
            public string applianceModel;
            public int pillarMidHeightMM;
            public string drawerType;
            public int drawerLength;
            public string drawerColor;
            public int drawerWidth;
            public string drawerSystem;
            public PipeNodeKind fittingKind;
            public bool facadeAssembled;

            public static Preset Default() => new Preset
            {
                applianceModel = "",
                pillarMidHeightMM = PillarElement.MidHeightMM_Default,
                drawerType = DefaultDrawerType,
                drawerLength = DefaultDrawerLengthMM,
                drawerColor = DefaultDrawerColor,
                drawerWidth = DefaultDrawerWidthMM,
                drawerSystem = DefaultDrawerSystem,
                fittingKind = PipeNodeKind.Coupling,
                facadeAssembled = false,
            };
        }

        public struct Item
        {
            public string name;
            public Vector3Int dims;
            public SidebarItemKind kind;
            public Preset preset;
            internal string tileTitle;

            public Item(string name, Vector3Int dims,
                SidebarItemKind kind = SidebarItemKind.Board)
            {
                this.name = name; this.dims = dims; this.kind = kind;
                preset = Preset.Default();
                tileTitle = name;
            }

            public string DisplayName =>
                !string.IsNullOrEmpty(preset.applianceModel) && name.EndsWith(preset.applianceModel)
                ? name.Substring(0, name.Length - preset.applianceModel.Length).TrimEnd()
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
            public Sprite icon;
            public List<Item> items;
        }

        private readonly struct GroupMeta
        {
            public readonly SidebarGroupKey key;
            public readonly string title;
            public readonly Sprite icon;

            public GroupMeta(SidebarGroupKey key, string title, Sprite icon)
            {
                this.key = key; this.title = title; this.icon = icon;
            }
        }

        private static IEnumerable<GroupMeta> GroupTable() => new[]
        {
            new GroupMeta(SidebarGroupKey.Board, "Детали", IconFactory.Shelf),
            new GroupMeta(SidebarGroupKey.Facade, "Фасады", IconFactory.Facade),
            new GroupMeta(SidebarGroupKey.Drawer, "Ящики", IconFactory.Drawer),
            new GroupMeta(SidebarGroupKey.Furniture, "Мебель", IconFactory.Furniture),
            new GroupMeta(SidebarGroupKey.Appliance, "Техника", IconFactory.Appliance),
            new GroupMeta(SidebarGroupKey.Sanitary, "Сантехника", IconFactory.Faucet),
            new GroupMeta(SidebarGroupKey.Room, "Помещение", IconFactory.Room),
            new GroupMeta(SidebarGroupKey.Construction, "Конструкции", IconFactory.Brickwork),
        };

        private static List<Group>? _cache;

        public static List<Group> Build()
        {
            if (_cache != null) return _cache;

            var rows = RowsWithTileTitleInherited().ToList();
            _cache = GroupTable().Select(meta => new Group
            {
                title = meta.title,
                icon = meta.icon,
                items = rows.Where(r => r.Group == meta.key).Select(r => r.Item).ToList(),
            }).ToList();
            return _cache;
        }

        private static IEnumerable<SidebarCatalogRow> TypesAndPresets()
        {
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Board, "Полка",
                new Item("Полка", new Vector3Int(600, 400, 16)));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Board, "Радиусная полка",
                new Item("Радиусная полка", new Vector3Int(600, 400, 16), SidebarItemKind.RadialShelf));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Board, "ДВП/ХДФ",
                new Item("ДВП/ХДФ", new Vector3Int(600, 400, 3), SidebarItemKind.Panel));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Facade, "Фасад",
                new Item("Фасад щитовой", new Vector3Int(600, 716, 18), SidebarItemKind.Facade));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Facade,
                AssembledFacadeItem("Фасад сборный"));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Drawer, "Ящик",
                DrawerItem("Ящик GTV", DefaultDrawerType, DefaultDrawerLengthMM, DefaultDrawerSystem));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Drawer,
                MoventoDrawerItem("Ящик Movento"));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, "Прямоугольный стол",
                new Item("Прямоугольный стол", new Vector3Int(2000, 750, 1000), SidebarItemKind.Table));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, "Радиусный стол",
                new Item("Радиусный стол", new Vector3Int(2000, 750, 1000), SidebarItemKind.RadiusTable));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, "Табуретка",
                new Item("Табуретка", new Vector3Int(StoolElement.DefaultWidthMM,
                    StoolElement.DefaultHeightMM, StoolElement.DefaultDepthMM), SidebarItemKind.Stool));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, "Стул",
                new Item("Стул", new Vector3Int(ChairElement.DefaultWidthMM,
                    ChairElement.DefaultHeightMM, ChairElement.DefaultDepthMM), SidebarItemKind.Chair));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, "Диван",
                new Item("Диван", new Vector3Int(SofaElement.DefaultWidthMM,
                    SofaElement.DefaultHeightMM, SofaElement.DefaultDepthMM), SidebarItemKind.Sofa));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, "Пуфик",
                new Item("Пуфик", new Vector3Int(PouffeElement.DefaultWidthMM,
                    PouffeElement.DefaultHeightMM, PouffeElement.DefaultDepthMM), SidebarItemKind.Pouffe));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, "Кровать",
                new Item("Кровать", new Vector3Int(BedElement.DefaultWidthMM,
                    BedElement.DefaultHeightMM, BedElement.DefaultDepthMM), SidebarItemKind.Bed));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, "Ножка",
                PillarItem("Ножка", PillarElement.MidHeightMM_Default));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, "Винтовая опора",
                ScrewLegItem("Винтовая опора"));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, "Мойка", SinkItem("Мойка"));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Appliance, "Варочная",
                CooktopItem("Варочная поверхность"));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Appliance,
                CooktopModelItem("Варочная " + CooktopElement.MODEL_BOSCH_PUE611BB5E,
                    CooktopElement.MODEL_BOSCH_PUE611BB5E));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Appliance, "Духовка",
                OvenItem("Духовка " + OvenElement.MODEL));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Appliance, "Посудомойка",
                DishwasherItem("Посудомойка " + DishwasherElement.MODEL));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, "Унитаз",
                ToiletItem("Унитаз"));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, "Инсталляция",
                WallHungToiletItem("Инсталляция"));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, "Ванна",
                BathtubItem("Ванна"));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, "Смеситель",
                BathMixerItem("Смеситель"));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, "Душевая стойка",
                ShowerColumnItem("Душевая стойка"));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, "Труба",
                PipeItem("Труба"));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, "Фитинг",
                FittingItem(PipeNodeKind.Elbow));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Sanitary,
                FittingItem(PipeNodeKind.Coupling));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Sanitary,
                FittingItem(PipeNodeKind.Tee));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Sanitary,
                FittingItem(PipeNodeKind.Cap));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Sanitary,
                FittingItem(PipeNodeKind.Supply));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Sanitary,
                FittingItem(PipeNodeKind.Return));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, "Короб",
                new Item("Короб", new Vector3Int(600, 600, 600)));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, "Стена",
                new Item("Стена", new Vector3Int(2000, 2500, 100), SidebarItemKind.Wall));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, "Окно",
                new Item("Окно", new Vector3Int(900, 1200, 100), SidebarItemKind.Window));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, "Дверь",
                new Item("Дверь", new Vector3Int(900, 2000, 100), SidebarItemKind.Door));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, "Пол",
                new Item("Пол", new Vector3Int(
                    FloorElement.DEFAULT_SIZE_MM,
                    FloorElement.DEFAULT_THICKNESS_MM,
                    FloorElement.DEFAULT_SIZE_MM), SidebarItemKind.Floor));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, "Источник света",
                LightSourceItem("Источник света"));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, "Розетка",
                SocketItem("Розетка"));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, "Выключатель",
                LightSwitchItem("Выключатель"));
        }

        private static IEnumerable<(SidebarGroupKey Group, Item Item)> RowsWithTileTitleInherited()
        {
            string currentTileTitle = "";
            foreach (var row in TypesAndPresets())
            {
                if (row.IsTypeRow) currentTileTitle = row.TileTitle!;
                var item = row.Item;
                item.tileTitle = currentTileTitle;
                yield return (row.Group, item);
            }
        }

        private static Item DrawerItem(string name, string drawerType, int length,
            string system = DefaultDrawerSystem)
        {
            int height = DrawerConstants.GetMinOpeningHeight(SidebarPresetResolution.DrawerTypeOf(drawerType));
            var item = new Item(name, new Vector3Int(DefaultDrawerWidthMM, height, length),
                SidebarItemKind.Drawer);
            item.preset.drawerType = drawerType;
            item.preset.drawerLength = length;
            item.preset.drawerColor = DefaultDrawerColor;
            item.preset.drawerWidth = DefaultDrawerWidthMM;
            item.preset.drawerSystem = system;
            return item;
        }

        private static Item MoventoDrawerItem(string name)
        {
            var item = DrawerItem(name, "B", 500, MoventoDrawerSystem);
            item.preset.drawerColor = "White";
            return item;
        }

        private static Item AssembledFacadeItem(string name)
        {
            var item = new Item(name, new Vector3Int(600, 716, 18), SidebarItemKind.Facade);
            item.preset.facadeAssembled = true;
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
            item.preset.fittingKind = kind;
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
            item.preset.applianceModel = DishwasherElement.MODEL;
            return item;
        }

        private static Item OvenItem(string name)
        {
            var item = new Item(name, OvenElement.ModelDimensionsMM, SidebarItemKind.Oven);
            item.preset.applianceModel = OvenElement.MODEL;
            return item;
        }

        private static Item CooktopModelItem(string name, string model)
        {
            var item = new Item(name, CooktopElement.ModelDimensionsMM(model),
                SidebarItemKind.Cooktop);
            item.preset.applianceModel = model;
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
            item.preset.pillarMidHeightMM = midHeightMM;
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
